using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Types;
using TIA_LIB.Xml;

namespace TIA_LIB.Generation
{
    // Portal access belongs here, outside all models/renderers. Preflight and backups
    // finish before tags/imports; each prerequisite is compiled before its callers.
    internal sealed class ModularGenerationRuntime
    {
        internal static ModularGenerationRuntime Current;
        readonly PlcProject project;
        readonly string context, ledgerDirectory;
        readonly List<Artifact> artifacts = new List<Artifact>();
        readonly Dictionary<XmlBlock, XElement> shadows = new Dictionary<XmlBlock, XElement>();
        string runDirectory, preparedHash;
        bool prepared, uploaded, preparationFailed;
        Dictionary<string, PlcBlock> blocks;
        Dictionary<string, PlcType> types;
        readonly Dictionary<string, XElement> blockExports = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, XElement> typeExports = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
        sealed class Artifact
        {
            public string Id, Name, Kind, InstanceOf;
            public XElement Document, Existing;
            public XmlBlock Block;
            public PlcBlockUserGroup Group;
            public string[] GroupPath;
            public HashSet<string> Requires = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        internal ModularGenerationRuntime(PlcProject project)
        {
            this.project = project;
            var portal = SiemensPortal.Current;
            if (portal.OpenProject.Path == null || string.IsNullOrWhiteSpace(portal.GeneratorCpuIdentity))
                throw new InvalidOperationException("Modular generation needs a saved project path and an unambiguous selected PLC identity for its ownership ledger.");
            context = portal.OpenProject.Path.FullName + "|" + portal.GeneratorCpuIdentity;
            ledgerDirectory = Path.Combine(Directory.GetCurrentDirectory(), "GeneratedModels", Hash(context));
            // A model belongs to one selected project/CPU execution. Reusing the process
            // must not import cached blocks or helper instances from an earlier project.
            XmlBlock.Blocks = new List<XmlBlock>(); XmlUdt.Udts = new List<XmlUdt>(); XmlDatablock.Datablocks = new List<XmlDatablock>();
            PlcProject.fbTPs = null; PlcProject.fbRPs = null; PlcProject.TPs.Clear(); PlcProject.RPs.Clear();
            Current = this;
        }
        static string Hash(string value)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "");
        }
        string Ledger(string name) { return Path.Combine(ledgerDirectory, Hash(name) + ".xml"); }
        string Owner(string name) { return context + "|" + name; }
        bool OwnsType(string name)
        {
            string file = Ledger("type/" + name);
            if (!File.Exists(file)) return false;
            var record = XElement.Load(file);
            return record.Name == "EtuOwnedType" && (string)record.Attribute("Version") == "1" &&
                (string)record.Attribute("Name") == name && (string)record.Attribute("Owner") == Owner(name);
        }
        internal PlcBlockUserGroup FindGroup(IEnumerable<string> path)
        {
            PlcBlockUserGroup group = null;
            foreach (string name in path)
            {
                group = group == null ? SiemensPortal.Current.PlcSoftware.BlockGroup.Groups.Find(name) : group.Groups.Find(name);
                if (group == null) return null;
            }
            return group;
        }
        internal static string[] ParentPath(PlcBlockUserGroup group)
        {
            var path = new List<string>();
            while (group != null) { path.Add(group.Name); group = group.Parent as PlcBlockUserGroup; }
            path.Reverse(); return path.ToArray();
        }
        PlcBlockUserGroup CreateGroup(IEnumerable<string> path)
        {
            PlcBlockUserGroup group = null;
            foreach (string name in path)
            {
                var composition = group == null ? SiemensPortal.Current.PlcSoftware.BlockGroup.Groups : group.Groups;
                group = composition.Find(name) ?? composition.Create(name);
            }
            return group;
        }
        internal XElement Editable(XmlBlock block, string name, XElement exported)
        {
            if (exported == null || block is XmlPlant) return exported;
            string ledger = Ledger(name);
            if (File.Exists(ledger))
            {
                var record = XElement.Load(ledger);
                if ((string)record.Attribute("Staging") != PlcProject.StagingMode.ToString())
                    throw new InvalidOperationException("Existing modular block '" + name + "' uses a different/unrecorded hardware staging mode. Reconcile its IO mappings explicitly before changing modes.");
                return ManagedArtifactOwnership.Read(record).Restore(Owner(name), SelectedLanguage, exported);
            }
            string language = (string)exported.Element("SW.Blocks.FB")?.Element("AttributeList")?.Element("ProgrammingLanguage");
            if (language == "FBD") return exported; // Inventory only; preflight will refuse unowned replacement.
            string directory = NewRunDirectory();
            exported.Save(Path.Combine(directory, Hash(name) + "-existing.xml"));
            new XElement("ReconciliationRequired", new XAttribute("Block", name), new XAttribute("ExistingLanguage", language ?? "unknown"),
                new XElement("Reason", "No managed ownership ledger. Existing SCL/manual bodies are preserved; automatic conversion/adoption is disabled."),
                new XElement("OnlineValues", "Not captured; no download is performed.")).Save(Path.Combine(directory, "reconciliation.xml"));
            throw new InvalidOperationException("Existing block '" + name + "' is not owned by modular generation. Backup/reconciliation details: " + directory + ". Preserve its logic and reconcile it explicitly, or use a fresh Unit/operation name.");
        }
        string SelectedLanguage { get { return PlcProject.Language == GenerationLanguage.Scl ? "SCL" : "FBD"; } }
        string NewRunDirectory()
        {
            if (runDirectory == null)
            {
                runDirectory = Path.Combine(ledgerDirectory, "Runs", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffff") + "-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(runDirectory);
            }
            return runDirectory;
        }
        static IEnumerable<PlcBlock> AllBlocks(IEnumerable<PlcBlock> direct, IEnumerable<PlcBlockUserGroup> groups)
        {
            foreach (var block in direct) yield return block;
            foreach (var group in groups) foreach (var block in AllBlocks(group.Blocks, group.Groups)) yield return block;
        }
        static IEnumerable<PlcType> AllTypes(IEnumerable<PlcType> direct, IEnumerable<PlcTypeUserGroup> groups)
        {
            foreach (var type in direct) yield return type;
            foreach (var group in groups) foreach (var type in AllTypes(group.Types, group.Groups)) yield return type;
        }
        XElement ExportBlock(string name)
        {
            XElement xml;
            if (!blockExports.TryGetValue(name, out xml))
            {
                PlcBlock block;
                if (!blocks.TryGetValue(name, out block)) return null;
                string file = Path.Combine(NewRunDirectory(), Hash("block/" + name) + "-backup.xml");
                block.Export(new FileInfo(file), ExportOptions.WithDefaults); xml = XElement.Load(file); blockExports.Add(name, xml);
            }
            return new XElement(xml);
        }
        XElement ExportType(string name)
        {
            XElement xml;
            if (!typeExports.TryGetValue(name, out xml))
            {
                PlcType type;
                if (!types.TryGetValue(name, out type)) return null;
                string file = Path.Combine(NewRunDirectory(), Hash("type/" + name) + "-backup.xml");
                type.Export(new FileInfo(file), ExportOptions.WithDefaults); xml = XElement.Load(file); typeExports.Add(name, xml);
            }
            return new XElement(xml);
        }
        static XElement Section(XElement document)
        {
            return document?.Descendants().FirstOrDefault(e => e.Name.LocalName == "Sections")?.Elements().FirstOrDefault(e => e.Name.LocalName == "Section");
        }
        IEnumerable<XmlBlock> OwnedBlocks()
        {
            foreach (var unit in OrderedUnits())
            {
                yield return unit;
                var unitNetworks = unit.Xml.Descendants("SW.Blocks.CompileUnit").ToArray();
                foreach (var operation in unit.Operations.Values.OrderBy(o => Array.FindIndex(unitNetworks, n => n == unit.FindNetwork(o.Name)?.Xml)).ThenBy(o => o.Name, StringComparer.Ordinal))
                {
                    yield return operation;
                    foreach (string phase in operation.PhaseOrder) yield return operation.Phases[phase];
                }
            }
            foreach (var interlock in (XmlBlock.Blocks ?? new List<XmlBlock>()).OfType<XmlInterlock>()) yield return interlock;
            if (PlcProject.fbTPs != null) yield return PlcProject.fbTPs;
            if (PlcProject.fbRPs != null) yield return PlcProject.fbRPs;
        }
        IEnumerable<XmlUnit> OrderedUnits()
        {
            var networks = project.Plant.Xml.Descendants("SW.Blocks.CompileUnit").ToArray();
            return project.Plant.Units.Values.OrderBy(u => Array.FindIndex(networks, n => n == project.Plant.FindUnitCallNetwork(u.Name)?.Xml)).ThenBy(u => u.Name, StringComparer.Ordinal);
        }
        static string BlockName(XmlBlock block) { return (string)block.Xml.Element("SW.Blocks.FB").Element("AttributeList").Element("Name"); }
        Artifact Add(string kind, string name, XElement document, PlcBlockUserGroup group = null, XmlBlock block = null)
        {
            var artifact = new Artifact { Id = kind + "/" + name, Kind = kind, Name = name, Document = document == null ? null : new XElement(document), Group = group, Block = block,
                GroupPath = block?.TargetGroupPath ?? (kind == "block" ? new[] { "Datablocks" } : null) };
            if (artifacts.Any(a => string.Equals(a.Id, artifact.Id, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Generated artifact collision: " + artifact.Id);
            artifacts.Add(artifact); return artifact;
        }
        XElement Render(XmlBlock block)
        {
            var shadow = new XElement(block.Xml);
            shadows.Add(block, shadow);
            var rendered = new XElement(block.Xml);
            var body = rendered.Element("SW.Blocks.FB");
            var list = body.Element("ObjectList");
            var oldBodies = list.Elements("SW.Blocks.CompileUnit").ToArray();
            IEnumerable<XElement> networks;
            GeneratedStructuredBody structured = null;
            if (block is XmlPhase)
            {
                GeneratedPhaseModel.ValidateFreshTemplate(shadow);
                structured = new GeneratedPhaseModel();
            }
            else if (block is XmlInterlock)
                structured = new GeneratedInterlockModel(((XmlInterlock)block).FinalizedDescriptor());
            if (structured != null)
            {
                if (SelectedLanguage == "FBD") networks = new PhaseFbdRenderer().Render(structured);
                else networks = new[] { SclNetwork(new StructuredTextBodyRenderer().Render(structured)) };
                if (block is XmlInterlock)
                {
                    // Interlock templates have no initial compile units. Persist a real
                    // editable FBD body for subsequent owned regeneration.
                    var editableList = shadow.Element("SW.Blocks.FB").Element("ObjectList");
                    editableList.Elements("SW.Blocks.CompileUnit").Remove(); editableList.Add(new PhaseFbdRenderer().Render(structured));
                }
            }
            else
            {
                var logical = new List<GeneratedLogicalNetwork>();
                var unit = block as XmlUnit;
                foreach (var network in block.Xml.Element("SW.Blocks.FB").Element("ObjectList").Elements("SW.Blocks.CompileUnit"))
                {
                    var modelNetwork = block.Networks.Values.SingleOrDefault(n => n.Xml == network);
                    if (modelNetwork == null) throw new InvalidOperationException("Unregistered/manual network in " + BlockName(block) + "; reconcile it explicitly.");
                    foreach (var call in modelNetwork.Calls.Values.Where(c => c.InstType == "LocalVariable"))
                    {
                        XmlLocalInstance declaration;
                        if (!block.LocalInstance.TryGetValue(call.InstName, out declaration) || declaration.Datatype.Trim('"') != call.BlockName)
                            throw new InvalidOperationException("Generated call/declaration contract differs for " + BlockName(block) + "/" + call.InstName);
                    }
                    var allowed = block.LocalInstance.Values.Select(i => i.Datatype.Trim('"')).Concat(new[] { "fbTP", "fbRP" });
                    var model = GeneratedNetworkModel.FromGeneratedFbd(BlockName(block) + "/" + modelNetwork.Name, modelNetwork.Name, network, allowed);
                    DeviceCategory? category = null;
                    if (unit != null && unit.Devices.ContainsKey(modelNetwork.Name)) category = GeneratedBlockBody.CategoryFor(modelNetwork.Calls[modelNetwork.Name].BlockName);
                    logical.Add(new GeneratedLogicalNetwork(model, category));
                }
                var plan = new GeneratedBlockBody(logical);
                networks = SelectedLanguage == "FBD" ? (IEnumerable<XElement>)plan.RenderFbd() : new[] { SclNetwork(new StructuredTextBodyRenderer().Render(plan)) };
            }
            foreach (var old in oldBodies) old.Remove();
            list.Add(networks); body.Element("AttributeList").Element("ProgrammingLanguage").Value = SelectedLanguage;
            NormalizeObjectIds(rendered);
            block.PruneUnsupportedMultilingualTextItems(rendered);
            block.PruneUnsupportedMultilingualTextItems(shadow);
            return rendered;
        }
        static XElement SclNetwork(XElement source)
        {
            return new XElement("SW.Blocks.CompileUnit", new XAttribute("ID", "1"), new XAttribute("CompositionName", "CompileUnits"),
                new XElement("AttributeList", new XElement("NetworkSource", source), new XElement("ProgrammingLanguage", "SCL")));
        }
        static void NormalizeObjectIds(XElement document)
        {
            int id = 0;
            foreach (var attribute in document.DescendantsAndSelf().Attributes("ID")) attribute.Value = (id++).ToString("X");
        }
        void PlanConfiguration()
        {
            XmlDatablock db;
            if (!SiemensPortal.Current.GlobalDatablocks.TryGetValue("cfgData", out db))
            {
                if (!project.Plant.Units.Values.Any(u => u.Devices.Values.SelectMany(d => d.ConfiguredDeviceCalls()).Any(c => c.Parameters.ContainsKey("CFG")))) return;
                db = new XmlDatablock("cfgData");
            }
            var original = Section(db.OriginalDocument);
            var proposal = new XElement(original ?? db.SectionList);
            var configurations = new List<UnitConfigurationPlan>();
            var flatAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var migration = new XElement("ConfigurationMigration", new XAttribute("Context", context),
                new XElement("OriginalCfgDataHash", db.OriginalDocument == null ? "new" : ManagedArtifactOwnership.Fingerprint(db.OriginalDocument)),
                new XElement("OnlineActualValues", "Not captured. Offline start values are distinct; no download is performed."));
            bool requiresReview = false;
            foreach (var unit in OrderedUnits())
            {
                string udtName = "udtCfg_" + SignalStaging.SignalStagingInventory.SafeName(unit.Name);
                var type = ExportType(udtName);
                if (type != null)
                {
                    if (!OwnsType(udtName))
                        throw new InvalidOperationException("Existing configuration UDT '" + udtName + "' has no verified Unit ownership. Backup: " + runDirectory + ". Reconcile explicitly.");
                }
                var inspected = ConfigurationMigrationPlan.Inspect(unit, original, type);
                configurations.Add(inspected.Configuration);
                foreach (var leaf in inspected.Configuration.Leaves) flatAliases.Add(leaf.OldPath.Components[1]);
                var entry = new XElement("Unit", new XAttribute("Name", unit.Name), new XAttribute("Udt", udtName));
                foreach (var leaf in inspected.Configuration.Leaves)
                    entry.Add(new XElement("Path", new XAttribute("Old", string.Join("|", leaf.OldPath.Components)), new XAttribute("New", string.Join("|", leaf.NewPath.Components)), new XAttribute("Type", leaf.Datatype)));
                foreach (string issue in inspected.Preconditions(false, false, false)) entry.Add(new XElement("RequiredReview", issue));
                migration.Add(entry); requiresReview |= inspected.RequiresMigration;
                if (!inspected.Configuration.HasConfiguration) continue;
                if (original == null || !original.Elements().Any(m => string.Equals((string)m.Attribute("Name"), inspected.Configuration.SafeUnit, StringComparison.OrdinalIgnoreCase)))
                    if (db.SectionList.Elements().Any(m => string.Equals((string)m.Attribute("Name"), inspected.Configuration.SafeUnit, StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidOperationException("A newly declared/manual cfgData member conflicts with Unit " + inspected.Configuration.SafeUnit + ".");
                var priorMember = type == null ? null : original.Elements().Single(e => e.Name.LocalName == "Member" && (string)e.Attribute("Name") == inspected.Configuration.SafeUnit);
                var configArtifacts = new ConfigurationArtifacts(inspected.Configuration, type, priorMember);
                proposal = configArtifacts.ProposeDatablockSection(proposal);
                // Constructors' fresh flat aliases are inventory artifacts, not persistent
                // legacy members. Remove only aliases absent from the original DB.
                foreach (var leaf in inspected.Configuration.Leaves)
                    if (original == null || !original.Elements().Any(e => (string)e.Attribute("Name") == leaf.OldPath.Components[1]))
                        proposal.Elements().Where(e => (string)e.Attribute("Name") == leaf.OldPath.Components[1]).Remove();
                Add("type", udtName, configArtifacts.Udt);
                ConfigurationArtifacts.Bind(project.Plant, unit, inspected.Configuration);
            }
            UnitConfigurationPlan.ValidateUnitNames(configurations);
            migration.Save(Path.Combine(NewRunDirectory(), "configuration-plan.xml"));
            if (requiresReview)
            {
                ConfigurationMigrationApproval.CreateTemplate(migration).Save(Path.Combine(runDirectory, "configuration-approval.template.xml"));
                if (string.IsNullOrWhiteSpace(PlcProject.ConfigurationMigrationApprovalPath))
                    throw new InvalidOperationException("Legacy configuration migration requires reviewed external consumers, verified offline backups and separate online-value protection. No PLC artifacts were imported. Review " + Path.Combine(runDirectory, "configuration-plan.xml") + " and its approval template. Opt in at execution with --configuration-migration-approval <reviewed-approval.xml>. Existing block ownership/language protections still apply.");
                ConfigurationMigrationApproval.Validate(migration, XElement.Load(PlcProject.ConfigurationMigrationApprovalPath));
            }
            // Retain unrelated newly declared manual fields as well as the original DB.
            if (original != null)
                foreach (var field in db.SectionList.Elements().Where(m => !flatAliases.Contains((string)m.Attribute("Name"))))
                    if (!proposal.Elements().Any(m => string.Equals((string)m.Attribute("Name"), (string)field.Attribute("Name"), StringComparison.OrdinalIgnoreCase)))
                        proposal.Add(new XElement(field));
            db.SectionList.ReplaceNodes(proposal.Nodes());
            db.HasChanged = original == null || !XNode.DeepEquals(original, db.SectionList);
        }
        void Dependencies()
        {
            var generatedBlocks = artifacts.Where(a => a.Kind == "block" || a.Kind == "instance").ToDictionary(a => a.Name, StringComparer.OrdinalIgnoreCase);
            var generatedTypes = artifacts.Where(a => a.Kind == "type").ToDictionary(a => a.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var artifact in artifacts)
            {
                if (artifact.Kind == "instance")
                {
                    Artifact dependency;
                    if (generatedBlocks.TryGetValue(artifact.InstanceOf, out dependency)) artifact.Requires.Add(dependency.Id);
                    continue;
                }
                foreach (var member in artifact.Document.Descendants().Where(e => e.Name.LocalName == "Member"))
                {
                    string datatype = ((string)member.Attribute("Datatype"))?.Trim('"'); Artifact dependency;
                    if (datatype != null && generatedTypes.TryGetValue(datatype, out dependency)) artifact.Requires.Add(dependency.Id);
                    if (datatype != null && generatedBlocks.TryGetValue(datatype, out dependency) && dependency != artifact) artifact.Requires.Add(dependency.Id);
                }
                foreach (var access in artifact.Document.Descendants().Where(e => e.Name.LocalName == "Access" && (string)e.Attribute("Scope") == "GlobalVariable"))
                {
                    string name = (string)access.Descendants().FirstOrDefault(e => e.Name.LocalName == "Component")?.Attribute("Name"); Artifact dependency;
                    if (name != null && generatedBlocks.TryGetValue(name, out dependency) && dependency != artifact) artifact.Requires.Add(dependency.Id);
                }
                foreach (var instance in artifact.Document.Descendants().Where(e => e.Name.LocalName == "Instance" && (string)e.Attribute("Scope") == "GlobalVariable"))
                {
                    string name = (string)instance.Elements().First(e => e.Name.LocalName == "Component").Attribute("Name"); Artifact dependency;
                    if (generatedBlocks.TryGetValue(name, out dependency)) artifact.Requires.Add(dependency.Id);
                }
                foreach (var call in artifact.Document.Descendants().Where(e => e.Name.LocalName == "CallInfo"))
                {
                    Artifact dependency;
                    if (generatedBlocks.TryGetValue((string)call.Attribute("Name"), out dependency)) artifact.Requires.Add(dependency.Id);
                }
            }
        }
        void PreserveStagingOverrides()
        {
            XmlDatablock db;
            if (!SiemensPortal.Current.GlobalDatablocks.TryGetValue("dbIO", out db) || db.OriginalDocument == null) return;
            var original = Section(db.OriginalDocument);
            foreach (var unit in project.StagingInventory.Units)
            {
                var previous = original.Elements().SingleOrDefault(m => (string)m.Attribute("Name") == unit.SafeName);
                if (previous == null) continue;
                var current = db.SectionList.Elements().Single(m => (string)m.Attribute("Name") == unit.SafeName);
                if ((string)previous.Attribute("Datatype") != (string)current.Attribute("Datatype")) throw new InvalidOperationException("dbIO Unit field type collision: " + unit.SafeName);
                var merged = new XElement(previous);
                foreach (var field in current.Elements().Where(m => m.Name.LocalName == "Member"))
                {
                    string name = (string)field.Attribute("Name");
                    var oldField = merged.Elements().SingleOrDefault(m => (string)m.Attribute("Name") == name);
                    if (oldField == null) merged.Add(new XElement(field));
                    else if (((string)oldField.Attribute("Datatype"))?.Trim('"') != ((string)field.Attribute("Datatype"))?.Trim('"'))
                        throw new InvalidOperationException("dbIO staging field type collision: " + unit.SafeName + "." + name);
                }
                current.ReplaceWith(merged);
            }
            db.HasChanged = !XNode.DeepEquals(original, db.SectionList);
        }
        string ModelHash()
        {
            return Hash(string.Join("|", OwnedBlocks().Select(b => ManagedArtifactOwnership.Fingerprint(b.Xml))) + "|" +
                ManagedArtifactOwnership.Fingerprint(project.Plant.Xml) + "|" + string.Join("|", (XmlDatablock.Datablocks ?? new List<XmlDatablock>()).Select(d => ManagedArtifactOwnership.Fingerprint(d.Xml))) + "|" +
                string.Join("|", (XmlUdt.Udts ?? new List<XmlUdt>()).Select(t => ManagedArtifactOwnership.Fingerprint(t.Xml))));
        }
        internal void Prepare()
        {
            if (preparationFailed) throw new InvalidOperationException("Modular preparation failed earlier in this execution. Review its retained plan/diagnostics and start a new execution after reconciliation.");
            try { PrepareCore(); }
            catch (Exception error)
            {
                preparationFailed = true;
                File.WriteAllText(Path.Combine(NewRunDirectory(), "diagnostics.txt"), error.ToString());
                throw;
            }
        }
        void PrepareCore()
        {
            if (prepared) { if (ModelHash() != preparedHash) throw new InvalidOperationException("Model changed after modular preflight. Finalize declarations before CreateTags/Upload."); return; }
            var software = SiemensPortal.Current.PlcSoftware;
            blocks = AllBlocks(software.BlockGroup.Blocks, software.BlockGroup.Groups).ToDictionary(b => b.Name, StringComparer.OrdinalIgnoreCase);
            types = AllTypes(software.TypeGroup.Types, software.TypeGroup.Groups).ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
            NewRunDirectory();
            var existingTempTags = software.TagTableGroup.TagTables.Find("TEMP");
            if (existingTempTags != null) existingTempTags.Export(new FileInfo(Path.Combine(runDirectory, "TEMP-tags-backup.xml")), ExportOptions.WithDefaults);
            // Capture the original blocks before any review gate can stop planning.
            ExportBlock("cfgData"); ExportBlock(BlockName(project.Plant));
            foreach (var block in OwnedBlocks()) ExportBlock(BlockName(block));
            ModularControlRouting.Prepare(project.Plant);
            PlanConfiguration();
            PreserveStagingOverrides();
            foreach (var udt in XmlUdt.Udts ?? new List<XmlUdt>()) Add("type", udt.Name, udt.Xml);
            foreach (var db in XmlDatablock.Datablocks ?? new List<XmlDatablock>()) if (db.HasChanged)
            {
                var document = new XElement(db.Xml); db.PruneUnsupportedMultilingualTextItems(document); Add("block", db.Name, document, db.UserGroup);
            }
            var owned = OwnedBlocks().ToArray();
            foreach (var block in owned) Add("block", BlockName(block), Render(block), block.UserGroup, block);
            foreach (var block in owned)
            foreach (string entry in block.Instances)
            {
                var fields = entry.Split('|');
                if (!string.Equals(fields[0], "true", StringComparison.OrdinalIgnoreCase) || fields[1].Length == 0) continue;
                var artifact = Add("instance", fields[1], null, block.UserGroup); artifact.InstanceOf = fields[2]; artifact.GroupPath = block.TargetGroupPath;
            }
            var plantDocument = new XElement(project.Plant.Xml); project.Plant.PruneUnsupportedMultilingualTextItems(plantDocument);
            NormalizeObjectIds(plantDocument);
            Add("block", BlockName(project.Plant), plantDocument, project.Plant.UserGroup, project.Plant);
            Preflight();
            Dependencies();
            var ordered = ArtifactSchedule.Order(artifacts.Select(a => new ArtifactDependency(a.Id, a.Requires.ToArray())));
            var byId = artifacts.ToDictionary(a => a.Id);
            var sorted = ordered.Select(a => byId[a.Identity]).ToArray(); artifacts.Clear(); artifacts.AddRange(sorted);
            var plan = new XElement("ModularGeneration", new XAttribute("Context", context), new XAttribute("Language", SelectedLanguage), new XAttribute("Staging", PlcProject.StagingMode),
                new XElement("OnlineActualValues", "Not captured; no download is performed."));
            foreach (var artifact in artifacts)
            {
                if (artifact.Document != null) artifact.Document.Save(Path.Combine(runDirectory, Hash(artifact.Id) + "-generated.xml"));
                plan.Add(new XElement("Artifact", new XAttribute("Id", artifact.Id), new XAttribute("Existing", artifact.Existing != null),
                    new XAttribute("Folder", artifact.GroupPath == null ? "PLC data types" : string.Join("/", artifact.GroupPath)),
                    artifact.Requires.OrderBy(r => r, StringComparer.Ordinal).Select(r => new XElement("Requires", r))));
            }
            plan.Save(Path.Combine(runDirectory, "plan.xml"));
            prepared = true; preparedHash = ModelHash();
            Console.WriteLine("Modular generation preflight/backups: " + runDirectory);
        }
        void Preflight()
        {
            var repeatedName = artifacts.GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
            if (repeatedName != null) throw new InvalidOperationException("CPU-global generated name collision: " + string.Join(", ", repeatedName.Select(a => a.Id)));
            foreach (var artifact in artifacts)
            {
                if (artifact.Kind == "type")
                {
                    if (blocks.ContainsKey(artifact.Name)) throw new InvalidOperationException("Generated UDT conflicts with a CPU block: " + artifact.Name);
                    artifact.Existing = ExportType(artifact.Name);
                    if (artifact.Existing != null)
                    {
                        if (!OwnsType(artifact.Name) ||
                            !Equals(types[artifact.Name].Parent, SiemensPortal.Current.PlcSoftware.TypeGroup))
                            throw new InvalidOperationException("Existing UDT '" + artifact.Name + "' requires explicit ownership/folder review before replacement. Backup: " + runDirectory);
                        if (!artifact.Name.StartsWith("udtCfg_", StringComparison.Ordinal))
                        {
                            var merged = new XElement(artifact.Existing);
                            var section = Section(merged);
                            foreach (var field in Section(artifact.Document).Elements())
                            {
                                string name = (string)field.Attribute("Name");
                                var previous = section.Elements().SingleOrDefault(e => string.Equals((string)e.Attribute("Name"), name, StringComparison.OrdinalIgnoreCase));
                                if (previous == null) section.Add(new XElement(field));
                                else if ((string)previous.Attribute("Name") != name || (string)previous.Attribute("Datatype") != (string)field.Attribute("Datatype"))
                                    throw new InvalidOperationException("Owned hardware UDT field type/spelling changed: " + artifact.Name + "." + name);
                            }
                            artifact.Document = merged;
                        }
                    }
                    continue;
                }
                if (types.ContainsKey(artifact.Name)) throw new InvalidOperationException("Generated block/instance conflicts with a PLC datatype: " + artifact.Name);
                if (artifact.Kind == "block" && artifact.Block == null && artifact.Document.Element("SW.Blocks.GlobalDB") == null)
                    throw new InvalidOperationException("Expected a global DB artifact for " + artifact.Name + ". Preserve the conflicting block and reconcile it explicitly.");
                PlcBlock existing;
                if (!blocks.TryGetValue(artifact.Name, out existing)) continue;
                artifact.Existing = ExportBlock(artifact.Name);
                if (!Equals(existing.Parent, artifact.Group)) throw new InvalidOperationException("CPU-global block collision: " + artifact.Name + " exists outside the target folder. Backup: " + runDirectory);
                if (artifact.Kind == "instance")
                {
                    var instance = existing as InstanceDB;
                    if (instance == null || instance.InstanceOfName != artifact.InstanceOf) throw new InvalidOperationException("Instance DB contract differs for " + artifact.Name);
                }
                else if (artifact.Block != null && !(artifact.Block is XmlPlant))
                {
                    if (!File.Exists(Ledger(artifact.Name))) throw new InvalidOperationException("Existing block '" + artifact.Name + "' has no managed ownership. Preserve its manual logic and review backup " + runDirectory + ". No automatic adoption/conversion.");
                    ManagedArtifactOwnership.Read(XElement.Load(Ledger(artifact.Name))).Restore(Owner(artifact.Name), SelectedLanguage, artifact.Existing);
                }
            }
            ValidateLibraryContracts();
        }
        void ValidateLibraryContracts()
        {
            foreach (var block in shadows.Keys)
            foreach (var call in block.Networks.Values.SelectMany(n => n.Calls.Values))
            {
                if (artifacts.Any(a => a.Kind == "block" && a.Name == call.BlockName)) continue;
                var definition = ExportBlock(call.BlockName);
                if (definition == null) throw new InvalidOperationException("Missing external library FB " + call.BlockName + ".");
                var sections = definition.Descendants().First(e => e.Name.LocalName == "Sections");
                foreach (var parameter in call.Parameters.Values.Where(p => !string.Equals(p.Name, "EN", StringComparison.OrdinalIgnoreCase)))
                {
                    var pin = sections.Elements().Where(s => (string)s.Attribute("Name") == parameter.Section).SelectMany(s => s.Elements()).SingleOrDefault(m => (string)m.Attribute("Name") == parameter.Name);
                    if (pin == null || !string.Equals(((string)pin.Attribute("Datatype"))?.Trim('"'), parameter.Type.Trim('"'), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Library pin contract differs: " + call.BlockName + "." + parameter.Section + "." + parameter.Name + " : " + parameter.Type + ".");
                }
            }
            foreach (string name in new[] { "HMI", "F_Safe" })
            {
                var definition = ExportBlock(name);
                var section = Section(definition);
                var required = name == "HMI" ? new[] { "RESET", "RESET_DELAYED", "MODE_OFF", "MODE_SERV" } : new[] { "HSS" };
                foreach (string member in required)
                    if (!section?.Elements().Any(m => (string)m.Attribute("Name") == member && (string)m.Attribute("Datatype") == "Bool") ?? true)
                        throw new InvalidOperationException("Required existing global Bool is missing: " + name + "." + member + ". The generator does not create it.");
            }
            // Phase helper/type dependencies are external definitions. No SCL fallback.
            if (shadows.Keys.OfType<XmlPhase>().Any())
            {
                foreach (string name in new[] { "fbOpMsg", "fbTransfer", "fbTimer" })
                    if (ExportBlock(name) == null) throw new InvalidOperationException("Missing phase helper dependency " + name + ".");
                foreach (var statement in new GeneratedPhaseModel().Regions.SelectMany(Flatten).SelectMany(r => r.Statements).Where(s => s.Call != null && !s.Instruction))
                {
                    var sections = ExportBlock(statement.Call.BlockType).Descendants().First(e => e.Name.LocalName == "Sections");
                    foreach (var argument in statement.Call.Arguments)
                    {
                        var pin = sections.Elements().Where(s => (string)s.Attribute("Name") == argument.Direction.ToString()).SelectMany(s => s.Elements()).SingleOrDefault(m => (string)m.Attribute("Name") == argument.Name);
                        if (pin == null || !string.Equals(((string)pin.Attribute("Datatype"))?.Trim('"'), argument.Datatype, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("Phase helper pin contract differs: " + statement.Call.BlockType + "." + argument.Name + " : " + argument.Datatype);
                    }
                }
                if (ExportType("AlarmBlock") == null) throw new InvalidOperationException("Missing phase AlarmBlock configuration type.");
            }
            foreach (var artifact in artifacts.Where(a => a.Kind == "type" && a.Name.StartsWith("udtCfg_", StringComparison.Ordinal)))
            foreach (var member in Section(artifact.Document).Elements())
            {
                string datatype = ((string)member.Attribute("Datatype"))?.Trim('"');
                if (datatype != null && datatype.StartsWith("type", StringComparison.Ordinal) && ExportType(datatype) == null)
                    throw new InvalidOperationException("Missing configuration type dependency " + datatype + ".");
            }
        }
        static IEnumerable<GeneratedRegion> Flatten(GeneratedRegion region)
        {
            yield return region;
            foreach (var child in region.Children) foreach (var descendant in Flatten(child)) yield return descendant;
        }
        static void AppendMessages(IEnumerable<CompilerResultMessage> messages, StringBuilder diagnostics)
        {
            foreach (var message in messages)
            {
                diagnostics.AppendLine(message.State + " " + message.Path + ": " + message.Description);
                AppendMessages(message.Messages, diagnostics);
            }
        }
        static void Compile(PlcBlock block, StringBuilder diagnostics)
        {
            var result = block.GetService<ICompilable>().Compile();
            AppendMessages(result.Messages, diagnostics);
            if (result.ErrorCount != 0 || result.State == CompilerResultState.Error) throw new InvalidOperationException("PLC compilation failed for " + block.Name + ".");
        }
        internal void Upload()
        {
            if (uploaded) return;
            Prepare();
            var byId = artifacts.ToDictionary(a => a.Id);
            var log = new StringBuilder();
            try
            {
                ArtifactBatchExecution.Execute(artifacts.Select(a => new ArtifactDependency(a.Id, a.Requires.ToArray())), Recheck,
                    identity => Import(byId[identity], log));
            }
            catch (Exception error)
            {
                log.AppendLine(error.ToString());
                File.WriteAllText(Path.Combine(runDirectory, "diagnostics.txt"), log.ToString());
                throw;
            }
            uploaded = true;
        }
        void Recheck()
        {
            // Detect changes since preflight before the first import, including name races.
            var currentBlocks = AllBlocks(SiemensPortal.Current.PlcSoftware.BlockGroup.Blocks, SiemensPortal.Current.PlcSoftware.BlockGroup.Groups).ToDictionary(b => b.Name, StringComparer.OrdinalIgnoreCase);
            var currentTypes = AllTypes(SiemensPortal.Current.PlcSoftware.TypeGroup.Types, SiemensPortal.Current.PlcSoftware.TypeGroup.Groups).ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var artifact in artifacts.Where(a => a.Kind == "type"))
            {
                if (currentBlocks.ContainsKey(artifact.Name)) throw new InvalidOperationException("Type name conflicts with a block after preflight: " + artifact.Name);
                PlcType current;
                if (currentTypes.TryGetValue(artifact.Name, out current))
                {
                    if (artifact.Existing == null) throw new InvalidOperationException("Type appeared after preflight: " + artifact.Name);
                    if (!Equals(current.Parent, SiemensPortal.Current.PlcSoftware.TypeGroup)) throw new InvalidOperationException("Type moved after preflight: " + artifact.Name);
                    string file = Path.Combine(runDirectory, Hash(artifact.Id) + "-recheck.xml"); current.Export(new FileInfo(file), ExportOptions.WithDefaults);
                    if (ManagedArtifactOwnership.Fingerprint(XElement.Load(file)) != ManagedArtifactOwnership.Fingerprint(artifact.Existing)) throw new InvalidOperationException("Type changed after preflight: " + artifact.Name);
                }
                else if (artifact.Existing != null) throw new InvalidOperationException("Type disappeared after preflight: " + artifact.Name);
            }
            foreach (var artifact in artifacts.Where(a => a.Kind != "type"))
            {
                if (currentTypes.ContainsKey(artifact.Name)) throw new InvalidOperationException("Block name conflicts with a type after preflight: " + artifact.Name);
                PlcBlock current;
                if (currentBlocks.TryGetValue(artifact.Name, out current))
                {
                    if (artifact.Existing == null) throw new InvalidOperationException("Block appeared after preflight: " + artifact.Name);
                    if (!Equals(current.Parent, artifact.Group)) throw new InvalidOperationException("Block moved after preflight: " + artifact.Name);
                    string file = Path.Combine(runDirectory, Hash(artifact.Id) + "-recheck.xml"); current.Export(new FileInfo(file), ExportOptions.WithDefaults);
                    if (ManagedArtifactOwnership.Fingerprint(XElement.Load(file)) != ManagedArtifactOwnership.Fingerprint(artifact.Existing)) throw new InvalidOperationException("Block changed after preflight: " + artifact.Name);
                }
                else if (artifact.Existing != null) throw new InvalidOperationException("Block disappeared after preflight: " + artifact.Name);
            }
            // Library/control/configuration definitions were part of preflight too.
            // Recheck their snapshots so a concurrent edit cannot invalidate its pin
            // and value assumptions between preparation and the first import.
            foreach (var snapshot in blockExports.Where(e => !artifacts.Any(a => a.Kind != "type" && string.Equals(a.Name, e.Key, StringComparison.OrdinalIgnoreCase))))
            {
                PlcBlock current;
                if (!currentBlocks.TryGetValue(snapshot.Key, out current) || !Equals(current.Parent, blocks[snapshot.Key].Parent))
                    throw new InvalidOperationException("External block disappeared/moved after preflight: " + snapshot.Key);
                string file = Path.Combine(runDirectory, Hash("external/block/" + snapshot.Key) + "-recheck.xml");
                current.Export(new FileInfo(file), ExportOptions.WithDefaults);
                if (ManagedArtifactOwnership.Fingerprint(XElement.Load(file)) != ManagedArtifactOwnership.Fingerprint(snapshot.Value))
                    throw new InvalidOperationException("External block changed after preflight: " + snapshot.Key);
            }
            foreach (var snapshot in typeExports.Where(e => !artifacts.Any(a => a.Kind == "type" && string.Equals(a.Name, e.Key, StringComparison.OrdinalIgnoreCase))))
            {
                PlcType current;
                if (!currentTypes.TryGetValue(snapshot.Key, out current) || !Equals(current.Parent, types[snapshot.Key].Parent))
                    throw new InvalidOperationException("External type disappeared/moved after preflight: " + snapshot.Key);
                string file = Path.Combine(runDirectory, Hash("external/type/" + snapshot.Key) + "-recheck.xml");
                current.Export(new FileInfo(file), ExportOptions.WithDefaults);
                if (ManagedArtifactOwnership.Fingerprint(XElement.Load(file)) != ManagedArtifactOwnership.Fingerprint(snapshot.Value))
                    throw new InvalidOperationException("External type changed after preflight: " + snapshot.Key);
            }
            Directory.CreateDirectory(ledgerDirectory);
            // Folder creation is deferred until every read-only check/backup/recheck
            // has succeeded. Constructors never create target PLC folders in this mode.
            foreach (var artifact in artifacts.Where(a => a.Kind != "type"))
            {
                artifact.Group = artifact.Group ?? CreateGroup(artifact.GroupPath);
                if (artifact.Block != null) artifact.Block.UserGroup = artifact.Group;
            }
        }
        void Import(Artifact artifact, StringBuilder log)
        {
            var currentBlock = artifact.Kind == "instance" ? artifact.Group.Blocks.Find(artifact.Name) : null;
            {
                try
                {
                    log.AppendLine("Import " + artifact.Id);
                    string file = Path.Combine(runDirectory, Hash(artifact.Id) + "-generated.xml");
                    if (artifact.Kind == "type")
                    {
                        SiemensPortal.Current.PlcSoftware.TypeGroup.Types.Import(new FileInfo(file), ImportOptions.Override);
                        var typeResult = SiemensPortal.Current.PlcSoftware.TypeGroup.Types.Find(artifact.Name).GetService<ICompilable>().Compile();
                        // Installed Openness topic 58295603339 lists PlcType as ICompilable.
                        AppendMessages(typeResult.Messages, log);
                        log.AppendLine("Type compile " + artifact.Name + ": " + typeResult.ErrorCount + " errors, " + typeResult.WarningCount + " warnings.");
                        if (typeResult.ErrorCount != 0 || typeResult.State == CompilerResultState.Error) throw new InvalidOperationException("PLC datatype compilation failed for " + artifact.Name);
                        new XElement("EtuOwnedType", new XAttribute("Version", "1"), new XAttribute("Owner", Owner(artifact.Name)), new XAttribute("Name", artifact.Name)).Save(Ledger("type/" + artifact.Name));
                    }
                    else if (artifact.Kind == "instance")
                    {
                        if (currentBlock == null) artifact.Group.Blocks.CreateInstanceDB(artifact.Name, true, 1, artifact.InstanceOf);
                        else
                        {
                            // Preserve existing offline instance overrides when the FB's
                            // generated interface grows. No online values are inferred.
                            artifact.Existing.Save(file); artifact.Group.Blocks.Import(new FileInfo(file), ImportOptions.Override);
                        }
                        Compile(artifact.Group.Blocks.Find(artifact.Name), log);
                    }
                    else
                    {
                        artifact.Group.Blocks.Import(new FileInfo(file), ImportOptions.Override);
                        var imported = artifact.Group.Blocks.Find(artifact.Name); Compile(imported, log);
                        if (artifact.Block != null && !(artifact.Block is XmlPlant))
                        {
                            string export = Path.Combine(runDirectory, Hash(artifact.Id) + "-published.xml"); imported.Export(new FileInfo(export), ExportOptions.WithDefaults);
                            var ownership = ManagedArtifactOwnership.Capture(Owner(artifact.Name), SelectedLanguage, XElement.Load(export), shadows[artifact.Block]).ToXml();
                            ownership.SetAttributeValue("Staging", PlcProject.StagingMode); ownership.Save(Ledger(artifact.Name));
                        }
                    }
                    File.WriteAllText(Path.Combine(runDirectory, "diagnostics.txt"), log.ToString());
                }
                catch (Exception error)
                {
                    log.AppendLine(error.ToString()); File.WriteAllText(Path.Combine(runDirectory, "diagnostics.txt"), log.ToString());
                    throw new InvalidOperationException("Modular generation stopped at " + artifact.Id + ". Dependent callers were not imported. Sources, backups and diagnostics: " + runDirectory, error);
                }
            }
        }
    }
}
