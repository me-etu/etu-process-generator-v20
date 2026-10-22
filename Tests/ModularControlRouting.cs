using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Linq;
using TIA_LIB;
using TIA_LIB.SignalStaging;
using TIA_LIB.Xml;
using TIA_LIB.Xml.Network;
using TIA_LIB.Generation;

public static class ModularControlRoutingTests
{
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static T Bare<T>() { return (T)FormatterServices.GetUninitializedObject(typeof(T)); }
    static void Reference(XmlCall call, string pin, string value, bool global = false)
    {
        var endpoint = call.Network.Xml.Descendants().Single(e => e.Name.LocalName == "NameCon" &&
            (string)e.Attribute("UId") == call.UId.ToString() && (string)e.Attribute("Name") == pin);
        var id = (int)endpoint.Parent.Elements().Single(e => e.Name.LocalName == "IdentCon").Attribute("UId");
        var source = call.Network.Access[id] as XmlGlobal;
        Check(source != null && source.Value == value && (string)source.Xml.Attribute("Scope") == (global ? "GlobalVariable" : "LocalVariable"), "Wrong scoped operand: " + call.InstName + "." + pin);
    }
    static XmlCall Selector(XmlBlock block, string title, string instance)
    {
        object[] args = { title, instance, false };
        return (XmlCall)typeof(XmlBlock).GetMethod("ConfigureSfcControl", Private).Invoke(block, args);
    }
    static void Reload(XmlBlock block, string version)
    {
        var networks = block.Networks.Values.ToArray();
        block.Networks.Clear();
        foreach (var network in networks)
        {
            var xml = new XElement(network.Xml);
            var flg = xml.Descendants().Single(e => e.Name.LocalName == "FlgNet");
            XNamespace ns = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/" + version;
            foreach (var element in flg.DescendantsAndSelf())
            {
                element.Attributes().Where(a => a.IsNamespaceDeclaration).Remove();
                element.Name = ns + element.Name.LocalName;
            }
            network.Xml.ReplaceWith(xml);
            new XmlNetwork(block, xml);
        }
    }
    static void Prepare(XmlPlant plant)
    {
        try { typeof(PlcProject).Assembly.GetType("TIA_LIB.Generation.ModularControlRouting").GetMethod("Prepare", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { plant }); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
    }
    public static void Run(string root)
    {
        string previous = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(Path.Combine(root, "TIAOpenness/TIA_Lib/TIA_LIB"));
            foreach (SignalStagingMode mode in Enum.GetValues(typeof(SignalStagingMode)))
            foreach (OperationInterlockLanguage language in Enum.GetValues(typeof(OperationInterlockLanguage)))
            foreach (string version in new[] { "v4", "v5" })
            {
                PlcProject.StagingMode = mode;
                PlcProject.InterlockLanguage = language;
                var project = (PlcProject)typeof(DeviceStagingInputTests).GetMethod("Project", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                var unit = project.Plant.GetUnit("A2");
                unit.Operations = new Dictionary<string, XmlOperation>();
                project.AddAnalog("A2", "AI-1", instanceCount: 2);
                project.AddDigital("A2", "DI-1", instanceCount: 2);
                project.AddValve("A2", "V-1");
                project.AddValveControl("A2", "CV-1");
                project.AddMotor("A2", "M-1");
                project.AddMotorControl("A2", "MC-1");
                project.AddPidControl("A2", "PID-1");
                var selector = Selector(unit, "Unit sfc control", "sfc_A2");
                selector.Network.SetParameter(selector, "Reset", "Input", "Bool", "HMI|RESET", true);
                var op = (XmlOperation)typeof(SfcHssTests).GetMethod("Block", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { false });
                typeof(XmlBlock).GetField("_Namespace", Private).SetValue(op, (XNamespace)"http://www.siemens.com/automation/Openness/SW/Interface/v5");
                op.Unit = unit;
                op.Phases = new Dictionary<string, XmlPhase>();
                unit.Operations.Add("OP1", op);
                unit.GetNetwork("OP1").GetCall("OP1", "FB", "LocalVariable", "OP1");
                Selector(op, "OP1", "sfc_OP1");
                foreach (string name in new[] { "PH1", "PH2" })
                {
                    var phase = Bare<XmlPhase>();
                    phase.Op = op;
                    op.Phases.Add(name, phase);
                    var controller = Selector(op, "Sfc control " + name, "sfc_" + name);
                    controller.Network.SetParameter(controller, "Reset", "Input", "Bool", "HMI|RESET_DELAYED", true);
                }
                var manual = project.Plant.GetNetwork("Manual");
                manual.SetComment("Keep external Plant logic");
                var manualXml = manual.Xml.ToString();
                var dbs = SiemensPortal.Current.GlobalDatablocks.ToDictionary(p => p.Key, p => p.Value.SectionList.ToString());
                var originalCfg = new XElement(SiemensPortal.Current.GlobalDatablocks["cfgData"].SectionList);
                var pidCfg = originalCfg.Elements().Single(e => (string)e.Attribute("Name") == "A2_PID-1");
                pidCfg.SetAttributeValue("Version", "0.1");
                pidCfg.Add(new XElement("Comment", new XElement("MultiLanguageText", new XAttribute("Lang", "de-DE"), "Keep PID configuration")));
                pidCfg.Add(new XElement("Sections", new XElement("Section", new XAttribute("Name", "None"),
                    new XElement("Member", new XAttribute("Name", "FEATURE"), new XAttribute("Datatype", "Struct"),
                        new XElement("Member", new XAttribute("Name", "IsMeldSystemOff"), new XAttribute("Datatype", "Bool"), new XElement("StartValue", "false"))))));
                string cfgSnapshot = originalCfg.ToString();
                var migration = ConfigurationMigrationPlan.Inspect(unit, originalCfg);
                Check(migration.Configuration.Leaves.Count == 11 && migration.ExistingLegacyMembers.Count == 11 && migration.InterfaceDirection == "Input", "Configuration inventory differs for seven families plus monitors.");
                Check(migration.Preconditions(false, false, false).Count == 3 && migration.Preconditions(true, true, true).Count == 0, "Migration protections are missing.");
                Check(!ConfigurationMigrationPlan.Inspect(unit, null).RequiresMigration, "Fresh declarations were mistaken for legacy data.");
                var plannedPid = migration.Configuration.Leaves.Single(l => l.Instance == "PID-1").Member;
                Check((string)plannedPid.Attribute("Version") == "0.1" && plannedPid.Descendants("StartValue").Single().Value == "false" &&
                    plannedPid.Descendants("MultiLanguageText").Single().Value == "Keep PID configuration", "Migration lost metadata or offline start values.");
                Check(cfgSnapshot == originalCfg.ToString(), "Read-only migration planning changed the original DB snapshot.");
                var collisionCfg = new XElement(originalCfg);
                collisionCfg.Elements().Single(e => (string)e.Attribute("Name") == "A2_PID-1").SetAttributeValue("Datatype", "typeAnalogCfg");
                try { ConfigurationMigrationPlan.Inspect(unit, collisionCfg); throw new Exception("CFG type collision accepted."); }
                catch (InvalidOperationException ex) { Check(ex.Message.Contains("collision"), "Missing CFG type collision context."); }
                collisionCfg = new XElement(originalCfg);
                collisionCfg.Add(new XElement("Member", new XAttribute("Name", "A2"), new XAttribute("Datatype", "ManualCfg")));
                try { ConfigurationMigrationPlan.Inspect(unit, collisionCfg); throw new Exception("Existing CFG unit collision accepted."); }
                catch (InvalidOperationException ex) { Check(ex.Message.Contains("reconciliation"), "Missing existing CFG member context."); }
                Reload(project.Plant, version); Reload(unit, version); Reload(op, version);
                manualXml = project.Plant.FindNetwork("Manual").Xml.ToString();
                Prepare(project.Plant);
                foreach (var pair in new[] { "iReset=HMI|RESET", "iReset_Delayed=HMI|RESET_DELAYED", "iMode_Off=HMI|MODE_OFF", "iMode_Service=HMI|MODE_SERV", "iHSS=F_Safe|HSS" })
                {
                    var split = pair.IndexOf('=');
                    Reference(project.Plant.FindNetwork("A2").Calls["A2"], pair.Substring(0, split), pair.Substring(split + 1), true);
                }
                Reference(unit.FindNetwork("Unit sfc control").Calls["sfc_A2"], "HSS", "iHSS");
                Reference(unit.FindNetwork("Unit sfc control").Calls["sfc_A2"], "Reset", "iReset");
                foreach (var device in unit.Devices.Values)
                {
                    Reference(unit.FindNetwork(device.TagName).Calls[device.TagName], "RESET", "iReset");
                    if (device.TagName == "AI-1" || device.TagName == "DI-1")
                        foreach (int index in new[] { 1, 2 }) Reference(unit.FindNetwork(device.TagName).Calls[device.TagName + "_Instance" + index], "RESET", "RESET_CYCL", true);
                }
                foreach (string name in new[] { "V-1", "CV-1", "M-1", "MC-1" }) Reference(unit.FindNetwork(name).Calls[name], "PROT", "iHSS");
                Reference(unit.FindNetwork("OP1").Calls["OP1"], "HSS", "iHSS");
                Reference(unit.FindNetwork("OP1").Calls["OP1"], "Delayed_Reset", "iReset_Delayed");
                Reference(op.FindNetwork("OP1").Calls["sfc_OP1"], "HSS", "HSS");
                Check(!op.FindNetwork("OP1").Calls["sfc_OP1"].Parameters.ContainsKey("Reset"), "Invented operation selector reset behavior.");
                foreach (string name in op.Phases.Keys)
                {
                    Reference(op.FindNetwork("Sfc control " + name).Calls["sfc_" + name], "HSS", "HSS");
                    Reference(op.FindNetwork("Sfc control " + name).Calls["sfc_" + name], "Reset", "Delayed_Reset");
                }
                foreach (var block in new XmlBlock[] { unit, op })
                    Check(!block.Networks.Values.SelectMany(n => n.Access.Values.OfType<XmlGlobal>()).Any(a =>
                        (string)a.Xml.Attribute("Scope") == "GlobalVariable" && new[] { "HMI|RESET", "HMI|RESET_DELAYED", "HMI|MODE_OFF", "HMI|MODE_SERV", "F_Safe|HSS" }.Contains(a.Value)), "Owned block retained a hidden control-global reference.");
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    string snapshot = project.Plant.Xml.ToString() + unit.Xml.ToString() + op.Xml.ToString();
                    project.Plant.HasChanged = unit.HasChanged = op.HasChanged = false;
                    Prepare(project.Plant);
                    Check(snapshot == project.Plant.Xml.ToString() + unit.Xml.ToString() + op.Xml.ToString() && !project.Plant.HasChanged && !unit.HasChanged && !op.HasChanged, "Modular control routing not idempotent.");
                    Reload(project.Plant, version); Reload(unit, version); Reload(op, version);
                }
                Check(manualXml == project.Plant.FindNetwork("Manual").Xml.ToString(), "Manual Plant content changed.");
                Check(dbs.Count == SiemensPortal.Current.GlobalDatablocks.Count && dbs.All(p => SiemensPortal.Current.GlobalDatablocks[p.Key].SectionList.ToString() == p.Value), "Routing mutated/created a global DB member.");
                var artifacts = new ConfigurationArtifacts(migration.Configuration);
                string artifactsSnapshot = artifacts.Udt.ToString() + artifacts.DatablockMember.ToString();
                Check(artifacts.RequiredTypes.Count == 7, "Configuration type dependencies differ.");
                var udtLeaves = artifacts.Udt.Descendants().First(e => e.Name.LocalName == "Section").Elements().ToArray();
                Check(udtLeaves.Select(e => (string)e.Attribute("Name")).SequenceEqual(migration.Configuration.Leaves.Select(l => l.Instance)), "UDT lost declaration order or configuration leaves.");
                Check(udtLeaves.Single(e => (string)e.Attribute("Name") == "PID-1").Descendants().Single(e => e.Name.LocalName == "StartValue").Value == "false", "UDT lost nested offline value.");
                var proposed = artifacts.ProposeDatablockSection(originalCfg);
                Check(proposed.Elements().Count() == originalCfg.Elements().Count() + 1 && originalCfg.ToString() == cfgSnapshot,
                    "DB proposal changed/deleted legacy members or the original snapshot.");
                Check(originalCfg.Elements().Zip(proposed.Elements(), (a, b) => XNode.DeepEquals(a, b)).All(v => v), "DB proposal changed existing manual/legacy data.");
                artifacts.Udt.RemoveNodes(); artifacts.DatablockMember.RemoveNodes();
                Check(artifactsSnapshot == artifacts.Udt.ToString() + artifacts.DatablockMember.ToString(), "Artifact snapshots are mutable.");
                typeof(ConfigurationArtifacts).GetMethod("Bind", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { project.Plant, unit, migration.Configuration });
                Reference(project.Plant.FindNetwork("A2").Calls["A2"], "CFG", "cfgData|A2", true);
                foreach (var device in unit.Devices.Values)
                {
                    var method = typeof(TIA_LIB.Devices.GeneratedObject).GetMethod("ConfiguredDeviceCalls", BindingFlags.Instance | BindingFlags.NonPublic);
                    foreach (var call in (IEnumerable<XmlCall>)method.Invoke(device, null)) Reference(call, "CFG", "CFG|" + call.InstName);
                }
                var bound = project.Plant.Xml.ToString() + unit.Xml.ToString();
                project.Plant.HasChanged = unit.HasChanged = false;
                typeof(ConfigurationArtifacts).GetMethod("Bind", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { project.Plant, unit, migration.Configuration });
                Check(bound == project.Plant.Xml.ToString() + unit.Xml.ToString() && !project.Plant.HasChanged && !unit.HasChanged,
                    "Configuration interface/bindings are not idempotent.");
                Check(dbs.All(p => SiemensPortal.Current.GlobalDatablocks[p.Key].SectionList.ToString() == p.Value), "Configuration binding implicitly mutated global DBs.");
                var logicalDevices = new List<GeneratedLogicalNetwork>();
                foreach (var device in unit.Devices.Values)
                {
                    var network = unit.FindNetwork(device.TagName);
                    var model = GeneratedNetworkModel.FromGeneratedFbd("A2/" + device.TagName, network.Name, network.Xml,
                        new[] { "fbMonAn", "fbMonDi", "fbVlv2Out", "fbVlvCtrl", "fbMotS", "fbMotAn", "fbPIDCtrl", "fbIntlck7" });
                    Check(model.Invocations.Count == network.Calls.Count, "Shared model lost device/helper calls: " + device.TagName);
                    Check(XNode.DeepEquals(model.RenderFbd(), network.Xml), "FBD backend changed device network.");
                    Check(model.RenderScl().Contains("REGION " + device.TagName), "SCL backend lost device title.");
                    Check(model.Invocations.Select(c => c.Instance.Components[0]).Distinct().Count() == network.Calls.Count, "Device stateful calls duplicated.");
                    logicalDevices.Add(new GeneratedLogicalNetwork(model, GeneratedBlockBody.CategoryFor(network.Calls[device.TagName].BlockName)));
                }
                var groupedBody = new GeneratedBlockBody(logicalDevices);
                Check(groupedBody.Networks.Select(n => n.Body.Title).SequenceEqual(new[] { "DI-1", "AI-1", "V-1", "CV-1", "M-1", "MC-1", "PID-1" }), "Seven device groups differ from golden order.");
                Check(groupedBody.RenderScl().Contains("REGION Devices\r\nREGION Digital_Sensors\r\nREGION DI-1"), "SCL nesting differs.");
                var structured = new StructuredTextBodyRenderer().Render(groupedBody);
                Check(System.Text.RegularExpressions.Regex.Replace(GeneratedNetworkModelTests.ReadStructuredText(structured), @"\s+", "") ==
                    System.Text.RegularExpressions.Regex.Replace(groupedBody.RenderScl(), @"\s+", ""), "Seven-family SCL token/source parity differs.");
                foreach (var groupedNetwork in groupedBody.RenderFbd())
                    Check(groupedNetwork.Descendants("Text").Any(t => t.Value.StartsWith("[ETU group: Devices/")), "Missing FBD group presentation.");
            }
            Console.WriteLine("PASS: explicit controls and seven-family configuration migration inventory, metadata/value copies, preconditions/collisions, cyclic reset/manual preservation and repeated routing in both modes/languages/v4-v5. Runtime activation remains pending.");
        }
        finally { Directory.SetCurrentDirectory(previous); }
    }
}
