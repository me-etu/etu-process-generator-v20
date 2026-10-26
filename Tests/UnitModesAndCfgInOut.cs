using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Text.RegularExpressions;
using TIA_LIB;
using TIA_LIB.Generation;
using TIA_LIB.SignalStaging;
using TIA_LIB.Xml;
using TIA_LIB.Xml.Network;

public static class UnitModesAndCfgInOutTests
{
    static readonly BindingFlags Hidden = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic;
    static string Root;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static object Invoke(Type type, string method, object target, params object[] args)
    {
        try { return type.GetMethod(method, Hidden).Invoke(target, args); }
        catch (TargetInvocationException error) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    static void Reject(Action action, string text)
    {
        try { action(); throw new Exception("Accepted invalid " + text); }
        catch (InvalidOperationException error) { Check(error.Message.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0, "Wrong diagnostic: " + error.Message); }
    }
    static T Bare<T>() { return (T)FormatterServices.GetUninitializedObject(typeof(T)); }
    static PlcProject Project()
    {
        var p = (PlcProject)Invoke(typeof(DeviceStagingInputTests), "Project", null);
        p.Plant.GetUnit("A2").Operations = new Dictionary<string, XmlOperation>();
        typeof(XmlBlock).GetField("_Name", Hidden).SetValue(p.Plant, "Plant"); return p;
    }
    static XElement Sections(XmlBlock block) { return block.Xml.Element("SW.Blocks.FB").Element("AttributeList").Element("Interface").Elements().Single(); }
    static XElement Cfg(XmlBlock block) { return Sections(block).Elements().SelectMany(s => s.Elements()).SingleOrDefault(m => (string)m.Attribute("Name") == "CFG"); }
    static void Bind(PlcProject project, UnitConfigurationPlan plan)
    { Invoke(typeof(ConfigurationArtifacts), "Bind", null, project.Plant, project.Plant.GetUnit("A2"), plan); }
    static UnitConfigurationPlan Configuration(PlcProject project)
    { return ConfigurationMigrationPlan.Inspect(project.Plant.GetUnit("A2"), SiemensPortal.Current.GlobalDatablocks["cfgData"].SectionList).Configuration; }
    static XElement Render(XmlBlock block)
    {
        var type = typeof(PlcProject).Assembly.GetType("TIA_LIB.Generation.ModularGenerationRuntime");
        var runtime = FormatterServices.GetUninitializedObject(type);
        type.GetField("shadows", Hidden).SetValue(runtime, new Dictionary<XmlBlock, XElement>());
        return (XElement)Invoke(type, "Render", runtime, block);
    }
    static string Title(XElement network)
    { return network.Descendants("MultilingualText").Single(e => (string)e.Attribute("CompositionName") == "Title").Descendants("Text").First().Value; }
    static string PathOf(XElement access)
    { return string.Join(".", access.Descendants().Where(e => e.Name.LocalName == "Component").Select(e => (string)e.Attribute("Name"))); }
    static void Reload(XmlBlock block, string version)
    {
        var networks = block.Networks.Values.ToArray(); block.Networks.Clear();
        foreach (var network in networks)
        {
            var copy = new XElement(network.Xml);
            var graph = copy.Descendants().Single(e => e.Name.LocalName == "FlgNet");
            XNamespace ns = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/" + version;
            foreach (var e in graph.DescendantsAndSelf()) { e.Attributes().Where(a => a.IsNamespaceDeclaration).Remove(); e.Name = ns + e.Name.LocalName; }
            network.Xml.ReplaceWith(copy); new XmlNetwork(block, copy);
        }
    }
    static void InterfaceFixtures()
    {
        PlcProject.Language = GenerationLanguage.Fbd;
        foreach (string version in new[] { "v4", "v5" })
        {
            var project = Project(); var unit = project.Plant.GetUnit("A2"); project.AddValve("A2", "V-1");
            var configuration = Configuration(project);
            Bind(project, configuration);
            Check((string)Cfg(unit).Parent.Attribute("Name") == "InOut", "Unit CFG still uses a private Input copy instead of the persistent InOut alias.");
            var plantCall = project.Plant.FindNetwork("A2").Calls["A2"];
            Check(plantCall.Parameters["CFG"].Section == "InOut", "Plant CFG pin direction differs from Unit interface.");
            var member = Cfg(unit);
            member.Add(new XElement(member.Name.Namespace + "Comment", new XElement(member.Name.Namespace + "MultiLanguageText", new XAttribute("Lang", "en-US"), "Keep metadata")),
                new XElement(member.Name.Namespace + "Sections", new XElement(member.Name.Namespace + "Section", new XAttribute("Name", "None"),
                    new XElement(member.Name.Namespace + "Member", new XAttribute("Name", "Override"), new XAttribute("Datatype", "Bool"), new XElement(member.Name.Namespace + "StartValue", "true")))));
            var expected = new XElement(member);
            // Independent previous-version Input fixture with a recorded old export.
            member.Remove(); Sections(unit).Elements().Single(s => (string)s.Attribute("Name") == "Input").Add(member);
            plantCall.Parameters["CFG"].Section = "Input"; plantCall.Parameters["CFG"].Xml.SetAttributeValue("Section", "Input");
            Reload(project.Plant, version); Reload(unit, version);
            string unchanged = project.Plant.Xml.ToString() + unit.Xml.ToString();
            Reject(() => Bind(project, configuration), "InOut");
            Check(unchanged == project.Plant.Xml.ToString() + unit.Xml.ToString(), "Unowned interface attempt mutated XML.");
            // The same whole-block fingerprint guard used by runtime must pass before
            // granting its targeted direction-upgrade capability.
            var old = new XElement(unit.Xml); old.Element("SW.Blocks.FB").Element("AttributeList").Element("Name").Value = "fbA2";
            var record = ManagedArtifactOwnership.Capture("project|cpu|fbA2", "FBD", old, old);
            ManagedArtifactOwnership.Read(record.ToXml()).Restore("project|cpu|fbA2", "FBD", old);
            var runtimeType = typeof(PlcProject).Assembly.GetType("TIA_LIB.Generation.ModularGenerationRuntime");
            var runtime = FormatterServices.GetUninitializedObject(runtimeType);
            string ledgerDirectory = Path.Combine(Path.GetTempPath(), "G019-ledger-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(ledgerDirectory);
            runtimeType.GetField("context", Hidden).SetValue(runtime, "project|cpu"); runtimeType.GetField("ledgerDirectory", Hidden).SetValue(runtime, ledgerDirectory);
            var ledger = record.ToXml(); ledger.SetAttributeValue("Staging", PlcProject.StagingMode.ToString());
            ledger.Save((string)Invoke(runtimeType, "Ledger", runtime, "fbA2"));
            typeof(XmlBlock).GetField("OriginalDocument", Hidden).SetValue(unit, new XElement(old));
            Invoke(runtimeType, "Editable", runtime, unit, "fbA2", old);
            Check((bool)typeof(XmlBlock).GetField("ManagedBodyVerified", Hidden).GetValue(unit), "Runtime did not grant the verified targeted upgrade capability.");
            Bind(project, configuration);
            Check(XNode.DeepEquals(Cfg(unit), expected) && (string)Cfg(unit).Parent.Attribute("Name") == "InOut", "Upgrade lost metadata/nested overrides or copied the member.");
            Check(unit.LocalInstance.Values.Count(i => i.Name == "CFG") == 0, "CFG was turned into Static shadow storage.");
            foreach (var call in unit.Devices.Values.SelectMany(d => unit.FindNetwork(d.TagName).Calls.Values).Where(c => c.Parameters.ContainsKey("CFG")))
                Check(call.Parameters["CFG"].Section == "Input", "Internal device CFG direction changed.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                string snapshot = project.Plant.Xml.ToString() + unit.Xml.ToString();
                project.Plant.HasChanged = unit.HasChanged = false;
                Bind(project, configuration);
                Check(snapshot == project.Plant.Xml.ToString() + unit.Xml.ToString() && !project.Plant.HasChanged && !unit.HasChanged, "CFG binding caches/direction are not idempotent.");
                Reload(project.Plant, version); Reload(unit, version);
            }
            var duplicate = new XElement(Cfg(unit)); Sections(unit).Elements().Single(s => (string)s.Attribute("Name") == "Input").Add(duplicate);
            string duplicates = unit.Xml.ToString(); Reject(() => Bind(project, configuration), "collision"); Check(unit.Xml.ToString() == duplicates, "Duplicate collision mutated declarations."); duplicate.Remove();
            Cfg(unit).SetAttributeValue("Datatype", "WrongType"); Reject(() => Bind(project, configuration), "collision"); Cfg(unit).SetAttributeValue("Datatype", "\"udtCfg_A2\"");
            var endpoint = project.Plant.FindNetwork("A2").Xml.Descendants().Single(e => e.Name.LocalName == "NameCon" && (string)e.Attribute("Name") == "CFG");
            string uid = (string)endpoint.Parent.Elements().Single(e => e.Name.LocalName == "IdentCon").Attribute("UId");
            var source = project.Plant.FindNetwork("A2").Access[int.Parse(uid)]; source.Xml.Descendants().First(e => e.Name.LocalName == "Component").SetAttributeValue("Name", "ManualDB");
            string wrong = project.Plant.Xml.ToString() + unit.Xml.ToString(); Reject(() => Bind(project, configuration), "writable alias"); Check(wrong == project.Plant.Xml.ToString() + unit.Xml.ToString(), "Manual Plant operand was silently reconciled.");
            Reload(project.Plant, version); Reject(() => Bind(project, configuration), "writable alias");
            var changed = new XElement(old); changed.Element("SW.Blocks.FB").Element("AttributeList").Add(new XElement("Comment", "Manual"));
            Reject(() => record.Restore("project|cpu|fbA2", "FBD", changed), "changed after");
            Reject(() => Invoke(runtimeType, "Editable", runtime, unit, "fbA2", changed), "changed after");
        }
    }
    static Dictionary<string, XElement> Definitions(UnitModePlan plan)
    {
        var documents = new Dictionary<string, XElement>();
        foreach (var target in plan.Targets)
        {
            documents[target.BlockType] = XElement.Parse("<Document><Sections><Section Name='InOut'><Member Name='OFF' Datatype='Bool'/></Section></Sections></Document>");
            documents[target.ConfigurationType] = XElement.Parse("<Document><Sections><Section Name='None'><Member Name='FEATURE' Datatype='Struct'><Sections><Section Name='None'><Member Name='IsAutoModeDefault' Datatype='Bool'/><Member Name='IsInterlockOff' Datatype='Bool'/></Section></Sections></Member></Section></Sections></Document>");
        }
        return documents;
    }
    static bool ModelValue(GeneratedExpression expression, IDictionary<string, bool> state)
    {
        if (expression.Kind == GeneratedExpressionKind.Symbol) return state[string.Join(".", expression.Symbol.Components)];
        if (expression.Kind == GeneratedExpressionKind.Not) return !ModelValue(expression.Operands[0], state);
        throw new Exception("Unsupported model expression in fixture.");
    }
    // Independent evaluator over coil/gate wiring, without using model assignments.
    static void Fbd(XElement network, Dictionary<string, bool> state)
    {
        var nodes = network.Descendants().Where(e => e.Parent != null && e.Parent.Name.LocalName == "Parts").ToDictionary(e => (string)e.Attribute("UId"));
        var wires = network.Descendants().Where(e => e.Name.LocalName == "Wire").ToArray();
        Func<string, string, bool> input = null; Func<XElement, bool> value = null;
        input = (id, pin) => {
            var wire = wires.Single(w => w.Elements().Any(e => e.Name.LocalName == "NameCon" && (string)e.Attribute("UId") == id && (string)e.Attribute("Name") == pin));
            var access = wire.Elements().SingleOrDefault(e => e.Name.LocalName == "IdentCon");
            if (access != null) return value(nodes[(string)access.Attribute("UId")]);
            var source = wire.Elements().Single(e => e.Name.LocalName == "NameCon" && (string)e.Attribute("Name") == "out");
            return value(nodes[(string)source.Attribute("UId")]);
        };
        value = node => {
            if (node.Name.LocalName == "Access") return (string)node.Attribute("Scope") == "LiteralConstant" ?
                node.Descendants().Single(e => e.Name.LocalName == "ConstantValue").Value.ToUpperInvariant() == "TRUE" : state[PathOf(node)];
            Check((string)node.Attribute("Name") == "A", "Unexpected FBD instruction");
            int count = int.Parse(node.Elements().Single(e => e.Name.LocalName == "TemplateValue").Value);
            return Enumerable.Range(1, count).All(i => input((string)node.Attribute("UId"), "in" + i) ^ node.Elements().Any(e => e.Name.LocalName == "Negated" && (string)e.Attribute("Name") == "in" + i));
        };
        foreach (var coil in nodes.Values.Where(e => e.Name.LocalName == "Part" && (string)e.Attribute("Name") == "Coil"))
        {
            string id = (string)coil.Attribute("UId");
            var target = wires.Single(w => w.Elements().Any(e => e.Name.LocalName == "NameCon" && (string)e.Attribute("UId") == id && (string)e.Attribute("Name") == "operand"));
            var destination = nodes[(string)target.Elements().Single(e => e.Name.LocalName == "IdentCon").Attribute("UId")];
            state[PathOf(destination)] = input(id, "in");
        }
    }
    static List<Action<Dictionary<string, bool>>> SclAssignments(string text)
    {
        var result = new List<Action<Dictionary<string, bool>>>();
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim(); if (!line.StartsWith("#") || !line.Contains(":=")) continue;
            var fields = line.Split(new[] { ":=" }, StringSplitOptions.None); Check(fields.Length == 2, "Unexpected chained assignment");
            string destination = fields[0].Replace("#", "").Replace("\"", "").Trim();
            string source = fields[1].Replace("#", "").Replace("\"", "").Replace("(", "").Replace(")", "").Trim().TrimEnd(';');
            bool invert = source.StartsWith("NOT "); if (invert) source = source.Substring(4).Trim();
            result.Add(state => state[destination] = invert ? !state[source] : state[source]);
        }
        return result;
    }
    static void Scans(UnitModePlan plan, GeneratedBlockBody body)
    {
        var modeNetworks = body.Networks.Where(n => n.Role == GeneratedNetworkRole.ModeOff || n.Role == GeneratedNetworkRole.ModeService).ToArray();
        var scl = new StructuredTextBodyRenderer().Render(new GeneratedBlockBody(modeNetworks));
        var statements = SclAssignments(StructuredTextBodyRenderer.ReadSource(scl));
        var a = new Dictionary<string, bool> { { "iMode_Off", false }, { "iMode_Service", false }, { "CFG.Unrelated", true } };
        foreach (var target in plan.Targets)
        {
            a[target.Instance + ".OFF"] = false;
            if (target.Service) { a["CFG." + target.Instance + ".FEATURE.IsAutoModeDefault"] = true; a["CFG." + target.Instance + ".FEATURE.IsInterlockOff"] = false; }
        }
        var b = new Dictionary<string, bool>(a); var model = new Dictionary<string, bool>(a);
        bool previousOff = false, previousService = false; int scans = 0;
        foreach (int bits in new[] { 0, 1, 3, 2, 0, 3, 1, 2 })
        for (int repeat = 0; repeat < 2; repeat++)
        {
            foreach (var state in new[] { a, b, model }) { state["iMode_Off"] = (bits & 1) != 0; state["iMode_Service"] = (bits & 2) != 0; }
            foreach (var target in plan.Targets)
            {
                // A device consumes the last scan's values, then consumes OFF (as
                // the actual supplied FB implementations do). Operations run later.
                foreach (var state in new[] { a, b, model })
                {
                    Check(state[target.Instance + ".OFF"] == previousOff, "Mode write was hoisted ahead of the current device call.");
                    if (target.Service) Check(state["CFG." + target.Instance + ".FEATURE.IsInterlockOff"] == previousService, "CFG timing differs from next-call behavior.");
                    state[target.Instance + ".OFF"] = false;
                }
            }
            foreach (var assignment in plan.Off.Concat(plan.Service)) model[string.Join(".", assignment.Destination.Symbol.Components)] = ModelValue(assignment.Value, model);
            foreach (var statement in statements) statement(a);
            foreach (var network in modeNetworks) Fbd(network.Body.RenderFbd(), b);
            Check(model.All(v => a[v.Key] == v.Value && b[v.Key] == v.Value), "Independent SCL/FBD scan state traces differ.");
            foreach (var target in plan.Targets)
            {
                Check(a[target.Instance + ".OFF"] == ((bits & 1) != 0), "OFF truth table differs at Operations.");
                if (target.Service) Check(a["CFG." + target.Instance + ".FEATURE.IsAutoModeDefault"] == ((bits & 2) == 0) && a["CFG." + target.Instance + ".FEATURE.IsInterlockOff"] == ((bits & 2) != 0), "Service truth table differs at Operations.");
            }
            Check(a["CFG.Unrelated"] && b["CFG.Unrelated"], "Unrelated persistent configuration was overwritten.");
            previousOff = (bits & 1) != 0; previousService = (bits & 2) != 0; scans++;
        }
        Check(scans == 16 && statements.Count == plan.Off.Count + plan.Service.Count, "Lost/duplicated scan assignments.");
        if (plan.Service.Count != 0)
        {
            var persistent = new Dictionary<string, bool>(a); var inputCopy = new Dictionary<string, bool>(persistent); inputCopy["iMode_Service"] = !previousService;
            foreach (var statement in statements) statement(inputCopy);
            string path = "CFG." + plan.Targets.First(t => t.Service).Instance + ".FEATURE.IsInterlockOff";
            Check(persistent[path] != inputCopy[path], "Copied-input counterexample failed to demonstrate the need for aliasing.");
            string automatic = "CFG." + plan.Targets.First(t => t.Service).Instance + ".FEATURE.IsAutoModeDefault";
            persistent["iMode_Service"] = false; persistent[automatic] = false; persistent[path] = true;
            foreach (var statement in statements) statement(persistent);
            Check(persistent[automatic] && !persistent[path], "Leaving Service restored manual/HMI values instead of cyclic ownership.");
        }
    }
    static void Matrix()
    {
        foreach (SignalStagingMode mode in Enum.GetValues(typeof(SignalStagingMode)))
        foreach (string version in new[] { "v4", "v5" })
        foreach (string inventory in new[] { "empty", "sensors", "pid", "mixed" })
        {
            PlcProject.StagingMode = mode; var project = Project(); var unit = project.Plant.GetUnit("A2");
            if (inventory == "sensors" || inventory == "mixed") { project.AddAnalog("A2", "AI-1", instanceCount: 2); project.AddDigital("A2", "DI-1", instanceCount: 3); }
            if (inventory == "mixed")
            {
                project.AddValve("A2", "V-1", interlockCount: 2, interlockSafeCount: 1);
                project.AddMotor("A2", "M-1", interlockCount: 1, interlockSafeCount: 2);
                project.AddValveControl("A2", "CV-1", interlockCount: 1);
                project.AddMotorControl("A2", "MC-1", interlockSafeCount: 2);
                Check(object.ReferenceEquals(project.AddValve("A2", "V-1"), unit.Devices["V-1"]), "Cached Add changed instance ownership.");
            }
            if (inventory == "pid" || inventory == "mixed") project.AddPidControl("A2", "PID-1");
            var operation = Bare<XmlOperation>(); typeof(XmlBlock).GetField("_Name", Hidden).SetValue(operation, "OP-Test");
            unit.Operations.Add("OP-Test", operation); unit.GetNetwork("OP-Test").GetCall("OP-Test", "FB", "LocalVariable", "OP-Test");
            unit.GetLocalInstance("ManualLookalike", "fbMotS");
            Reload(unit, version); Reload(project.Plant, version);
            Bind(project, Configuration(project));
            var plan = new UnitModePlan(unit);
            var expected = inventory == "mixed" ? new[] { "V-1", "M-1", "CV-1", "MC-1", "PID-1" } : inventory == "pid" ? new[] { "PID-1" } : new string[0];
            Check(plan.Targets.Select(t => t.Instance).SequenceEqual(expected), "Mode inventory includes a sensor/helper/manual lookalike or loses declaration order.");
            Check(plan.Off.Count == expected.Length && plan.Service.Count == (inventory == "mixed" ? 8 : 0), "Wrong mode target family/count.");
            var docs = Definitions(plan); Func<string, XElement> find = n => { XElement doc; return docs.TryGetValue(n, out doc) ? doc : null; };
            plan.ValidateDefinitions(find, find);
            var logical = unit.Xml.Descendants("SW.Blocks.CompileUnit").Select(xml => {
                var network = unit.Networks.Values.Single(n => n.Xml == xml);
                return new GeneratedLogicalNetwork(GeneratedNetworkModel.FromGeneratedFbd(network.Name, network.Name, xml, unit.LocalInstance.Values.Select(i => i.Datatype.Trim('"'))),
                    unit.Devices.ContainsKey(network.Name) ? (DeviceCategory?)GeneratedBlockBody.CategoryFor(network.Calls[network.Name].BlockName) : null,
                    network.Name == "OP-Test" ? GeneratedNetworkRole.OperationCall : GeneratedNetworkRole.None);
            }).ToArray();
            var body = plan.Apply(new GeneratedBlockBody(logical));
            if (inventory == "mixed")
            {
                string styled = GeneratedNetworkModelTests.ReadStructuredText(new StructuredTextBodyRenderer().Render(new GeneratedBlockBody(
                    body.Networks.Where(n => n.Role == GeneratedNetworkRole.ModeOff || n.Role == GeneratedNetworkRole.ModeService))));
                Check(styled == File.ReadAllText(Path.Combine(Root, "Tests/Fixtures/G019-UnitModes.scl")).Replace("\r\n", "\n"), "Styled OFF/Service independent-assignment golden differs.");
            }
            if (expected.Length != 0) Reject(() => plan.Apply(body), "Conflicting MODE");
            var ordered = body.Networks.ToList();
            foreach (var network in ordered.Where(n => n.Role == GeneratedNetworkRole.ModeOff || n.Role == GeneratedNetworkRole.ModeService))
                Check(ordered.IndexOf(network) > ordered.FindLastIndex(n => n.Category.HasValue) && ordered.IndexOf(network) < ordered.FindIndex(n => n.Role == GeneratedNetworkRole.OperationCall), "Mode plan position differs from after-Devices/before-Operations.");
            if (inventory == "mixed")
            {
                var schedule = ordered.Select(n => new ScheduledNetwork(n.Body.Identity, n.Body.Title, n.Category ?? DeviceCategory.Digital_Sensors, n.Body.Reads, n.Body.Writes));
                var moved = ordered.Where(n => n.Role == GeneratedNetworkRole.ModeService).Concat(ordered.Where(n => n.Role != GeneratedNetworkRole.ModeService)).Select(n => n.Body.Identity);
                Reject(() => DeviceSchedule.ValidateOrder(schedule, moved), "dependencies");
            }
            Scans(plan, body);
            string snapshot = unit.Xml.ToString() + project.Plant.Xml.ToString();
            foreach (GenerationLanguage language in Enum.GetValues(typeof(GenerationLanguage)))
            {
                PlcProject.Language = language;
                var rendered = Render(unit);
                Check(snapshot == unit.Xml.ToString() + project.Plant.Xml.ToString(), "Mode rendering mutated source/interface or original FBD.");
                Check(XNode.DeepEquals(rendered, Render(unit)), "Repeated mode render is not deterministic.");
                Check(rendered.Descendants().Count(e => e.Name.LocalName == "CallInfo") == unit.Networks.Values.Sum(n => n.Calls.Count), "Mode generation changed call counts.");
                if (language == GenerationLanguage.Scl)
                {
                    string source = StructuredTextBodyRenderer.ReadSource(rendered.Descendants().Single(e => e.Name.LocalName == "StructuredText"));
                    Check(source.Contains("REGION MODE Off") == (expected.Length != 0) && source.Contains("REGION MODE Service") == (inventory == "mixed"), "Empty mode region or missing PID OFF group.");
                    if (inventory == "mixed") Check(source.Contains("Setting actuators to Service mode") && source.Contains("IsInterlockOff\" := #\"iMode_Service\";"), "SCL mode style/operands differ.");
                    if (expected.Length != 0) Check(source.IndexOf("REGION MODE Off", StringComparison.Ordinal) < source.IndexOf("REGION Operations", StringComparison.Ordinal), "SCL mode groups follow Operations.");
                }
                else
                {
                    var networks = rendered.Descendants("SW.Blocks.CompileUnit").ToArray();
                    Check(networks.Count(n => Title(n) == "MODE Off") == (expected.Length == 0 ? 0 : 1) && networks.Count(n => Title(n) == "MODE Service") == (inventory == "mixed" ? 1 : 0), "Duplicate/empty FBD mode groups.");
                    foreach (var network in networks.Where(n => Title(n).StartsWith("MODE ")))
                    {
                        var graph = network.Descendants().Single(e => e.Name.LocalName == "FlgNet");
                        Check(graph.Name.NamespaceName.EndsWith("/" + version), "Mode rendering discarded existing FBD namespace.");
                        var duplicate = graph.Descendants().Where(e => e.Name.LocalName == "NameCon").GroupBy(e => (string)e.Attribute("UId") + "/" + (string)e.Attribute("Name")).FirstOrDefault(g => g.Count() > 1);
                        Check(duplicate == null, "FBD mode target has multiple drivers.");
                        foreach (var operandWire in graph.Descendants().Where(e => e.Name.LocalName == "Wire" && e.Elements().Any(c => c.Name.LocalName == "IdentCon")))
                            Check(operandWire.Elements().Count() == 2 && operandWire.Elements().Count(e => e.Name.LocalName == "NameCon") == 1,
                                "TIA rejects operand fan-out to multiple gate inputs as a power rail; keep each IdentCon connection point-to-point.");
                        Check(graph.Descendants().Where(e => e.Name.LocalName == "IdentCon").GroupBy(e => (string)e.Attribute("UId")).All(g => g.Count() == 1),
                            "Gate operand connections must use separate Access occurrences rather than reuse one operand across wires.");
                        var passthroughs = graph.Descendants().Where(e => e.Name.LocalName == "Part" && (string)e.Attribute("Name") == "A" &&
                            e.Elements().Any(t => t.Name.LocalName == "TemplateValue" && (string)t.Attribute("Name") == "Card" && t.Value == "1")).ToArray();
                        int expectedPositive = Title(network) == "MODE Off" ? expected.Length : plan.Targets.Count(t => t.Service);
                        Check(passthroughs.Length == (Title(network) == "MODE Off" ? 1 : expectedPositive),
                            "Only consecutive positive assignments may share a gate; Service pairs must retain their order.");
                        var executable = graph.Descendants().Where(e => e.Name.LocalName == "Part").ToArray();
                        var positions = executable.Select((e, i) => new { Id = (string)e.Attribute("UId"), Index = i }).ToDictionary(e => e.Id, e => e.Index);
                        foreach (var signal in graph.Descendants().Where(e => e.Name.LocalName == "Wire" && !e.Elements().Any(c => c.Name.LocalName == "IdentCon")))
                        {
                            var producer = signal.Elements().Single(e => (string)e.Attribute("Name") == "out");
                            var consumers = signal.Elements().Where(e => (string)e.Attribute("Name") == "in").ToArray();
                            Check(consumers.All(e => positions[(string)producer.Attribute("UId")] < positions[(string)e.Attribute("UId")]),
                                "FBD executable elements must be serialized in power-flow order, with producers before coils.");
                            var indices = consumers.Select(e => positions[(string)e.Attribute("UId")]).OrderBy(i => i).ToArray();
                            Check(indices.Last() - indices.First() + 1 == indices.Length,
                                "A shared output branch must not span an unrelated assignment branch.");
                        }
                        int positiveCoils = 0;
                        foreach (var passthrough in passthroughs)
                        {
                            var output = graph.Descendants().Single(w => w.Name.LocalName == "Wire" && w.Elements().Any(e => e.Name.LocalName == "NameCon" &&
                                (string)e.Attribute("UId") == (string)passthrough.Attribute("UId") && (string)e.Attribute("Name") == "out"));
                            positiveCoils += output.Elements().Count(e => e.Name.LocalName == "NameCon" && (string)e.Attribute("Name") == "in");
                        }
                        Check(positiveCoils == expectedPositive, "Pass-through outputs lost a positive mode assignment.");
                        foreach (var coil in graph.Descendants().Where(e => e.Name.LocalName == "Part" && (string)e.Attribute("Name") == "Coil"))
                        {
                            var signal = graph.Descendants().Single(e => e.Name.LocalName == "Wire" && e.Elements().Any(pin =>
                                pin.Name.LocalName == "NameCon" && (string)pin.Attribute("UId") == (string)coil.Attribute("UId") && (string)pin.Attribute("Name") == "in"));
                            Check(!signal.Elements().Any(e => e.Name.LocalName == "IdentCon" || e.Name.LocalName == "OpenCon") &&
                                signal.Elements().Count(e => e.Name.LocalName == "NameCon" && (string)e.Attribute("Name") == "out") == 1,
                                "TIA rejects an IdentCon/OpenCon directly driving a coil signal input; use a Boolean gate output.");
                        }
                        Check(Array.IndexOf(networks, network) < Array.FindIndex(networks, n => Title(n) == "OP-Test"), "FBD mode network follows Operations.");
                        var schemas = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
                        string schema = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Siemens/Automation/Portal V21/PublicAPI/V21/Schemas/SW.PlcBlocks.LADFBD_v5.xsd");
                        schemas.Add(graph.Name.NamespaceName, schema);
                        new XDocument(new XElement(graph)).Validate(schemas, (sender, error) => { throw new Exception("Mode FBD schema: " + error.Message); });
                    }
                }
            }
            if (inventory == "pid" || inventory == "empty") Check(!project.StagingInventory.Units.Any(), "Mode planning invented PID/empty hardware IO.");
            if (expected.Length != 0)
            {
                docs[plan.Targets[0].BlockType].Descendants("Member").Single().SetAttributeValue("Datatype", "Int");
                Reject(() => plan.ValidateDefinitions(find, find), "OFF");
                docs = Definitions(plan);
                if (inventory == "mixed")
                {
                    docs[plan.Targets[0].ConfigurationType].Descendants("Member").Single(e => (string)e.Attribute("Name") == "IsInterlockOff").Remove();
                    Reject(() => plan.ValidateDefinitions(find, find), "IsInterlockOff");
                }
            }
        }
    }
    static void Callers()
    {
        foreach (string xml in new[] { "<CallInfo Name='fbA2'/>", "<Member Datatype='&quot;fbA2&quot;'/>", "<InstanceOfName>fbA2</InstanceOfName>", "<Access Scope='GlobalVariable'><Symbol><Component Name='A2'/></Symbol></Access>", "<CallInfo Name='FBA2'/>" })
            Check(UnitConfigurationCallAudit.References(XElement.Parse("<Document>" + xml + "</Document>"), "fbA2", "A2"), "Unknown external Unit consumer escaped direction-upgrade audit.");
        Check(!UnitConfigurationCallAudit.References(XElement.Parse("<Document><Member Datatype='&quot;fbOther&quot;'/></Document>"), "fbA2", "A2"), "Unrelated block was treated as an external caller.");
        var known = XElement.Parse("<Document><CallInfo Name='fbA2'><Instance Scope='GlobalVariable'><Component Name='A2'/></Instance></CallInfo></Document>");
        Check(UnitConfigurationCallAudit.ValidateCaller(known, "Plant", "Plant", "fbA2", "A2"), "Known Plant call rejected.");
        Reject(() => UnitConfigurationCallAudit.ValidateCaller(known, "ManualFB", "Plant", "fbA2", "A2"), "External/manual");
        var duplicate = new XElement(known); duplicate.Add(new XElement(known.Elements().Single()));
        Reject(() => UnitConfigurationCallAudit.ValidateCaller(duplicate, "Plant", "Plant", "fbA2", "A2"), "Ambiguous");
        Reject(() => UnitConfigurationCallAudit.ValidateCaller(new XElement("Document"), "OtherInstanceDB", "Plant", "fbA2", "A2", true), "External/manual");
        known.Descendants("Component").Single().SetAttributeValue("Name", "OtherA2");
        Reject(() => UnitConfigurationCallAudit.ValidateCaller(known, "Plant", "Plant", "fbA2", "A2"), "Ambiguous");
    }
    static void ExportedStructContracts()
    {
        var project = Project(); project.AddValve("A2", "V-1");
        var plan = new UnitModePlan(project.Plant.GetUnit("A2"));
        var docs = Definitions(plan);
        XNamespace ns = "http://www.siemens.com/automation/Openness/SW/Interface/v5";
        // Minimal real-export shape from the failed run: inline Struct children
        // are direct Members, following metadata, without a Sections wrapper.
        var type = XElement.Parse("<Document><SW.Types.PlcStruct><AttributeList><Interface><Sections><Section Name='None'><Member Name='FEATURE' Datatype='Struct'><AttributeList><BooleanAttribute Name='ExternalWritable' SystemDefined='true'>true</BooleanAttribute></AttributeList><Member Name='IsAutoModeDefault' Datatype='Bool'><StartValue>true</StartValue></Member><Member Name='IsInterlockOff' Datatype='Bool'><StartValue>false</StartValue></Member></Member></Section></Sections></Interface></AttributeList></SW.Types.PlcStruct></Document>");
        foreach (var element in type.Descendants("Interface").Single().Descendants()) element.Name = ns + element.Name.LocalName;
        docs["typeValveCfg"] = type;
        Func<string, XElement> find = name => { XElement doc; return docs.TryGetValue(name, out doc) ? doc : null; };
        string original = type.ToString();
        plan.ValidateDefinitions(find, find);
        Check(type.ToString() == original, "Contract validation mutated exported Struct metadata/values.");
        var flag = type.Descendants().Single(e => e.Name.LocalName == "Member" && (string)e.Attribute("Name") == "IsAutoModeDefault");
        flag.SetAttributeValue("Datatype", "Int"); Reject(() => plan.ValidateDefinitions(find, find), "Bool"); flag.SetAttributeValue("Datatype", "Bool");
        var duplicate = new XElement(flag); flag.AddAfterSelf(duplicate); Reject(() => plan.ValidateDefinitions(find, find), "ambiguous"); duplicate.Remove();
        var feature = flag.Parent;
        feature.Add(new XElement(ns + "Sections", new XElement(ns + "Section", new XAttribute("Name", "None"), new XElement(flag))));
        Reject(() => plan.ValidateDefinitions(find, find), "ambiguous"); feature.Elements(ns + "Sections").Remove();
        flag.Remove(); Reject(() => plan.ValidateDefinitions(find, find), "IsAutoModeDefault");
    }
    public static void ValidateRetainedValveContracts(string runDirectory)
    {
        var previousPortal = SiemensPortal.Current; var previousPlant = XmlPlant.Current;
        try
        {
            var documents = new Dictionary<string, XElement>();
            foreach (string file in Directory.GetFiles(runDirectory, "*-backup.xml"))
            {
                var document = XElement.Load(file);
                foreach (var item in document.Elements())
                {
                    string name = (string)item.Element("AttributeList")?.Element("Name");
                    if (name != null) documents[name] = document;
                }
            }
            var project = Project(); project.AddValve("A2", "Q11");
            var plan = new UnitModePlan(project.Plant.GetUnit("A2"));
            Func<string, XElement> find = name => { XElement document; return documents.TryGetValue(name, out document) ? document : null; };
            plan.ValidateDefinitions(find, find);
            Console.WriteLine("PASS: replayed retained fbVlv2Out/typeValveCfg contract exports without Portal access or export mutation.");
        }
        finally { SiemensPortal.Current = previousPortal; XmlPlant.Current = previousPlant; }
    }
    public static void Run(string root)
    {
        Root = root;
        var portal = SiemensPortal.Current; var plant = XmlPlant.Current; var mode = PlcProject.StagingMode; var language = PlcProject.Language;
        try { ExportedStructContracts(); InterfaceFixtures(); Matrix(); Callers(); }
        finally { SiemensPortal.Current = portal; XmlPlant.Current = plant; PlcProject.StagingMode = mode; PlcProject.Language = language; }
        Console.WriteLine("PASS: Unit mode target inventory, InOut upgrade/alias contracts, independent SCL/FBD multi-scan oracle, caches/namespaces, manual-content/caller guards and family matrix. No Portal execution.");
    }
}
