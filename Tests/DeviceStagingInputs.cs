using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Linq;
using System.Xml;
using System.Xml.Schema;
using TIA_LIB;
using TIA_LIB.Devices;
using TIA_LIB.SignalStaging;
using TIA_LIB.Xml;
using TIA_LIB.Xml.Network;

public static class DeviceStagingInputTests
{
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static T Bare<T>() { return (T)FormatterServices.GetUninitializedObject(typeof(T)); }
    private static PlcProject Project()
    {
        var portal = Bare<SiemensPortal>();
        portal.GlobalDatablocks = new Dictionary<string, XmlDatablock>();
        foreach (string name in new[] { "HMI", "cfgData", "F_Safe" })
        {
            var db = Bare<XmlDatablock>();
            db.SectionList = new XElement("Section");
            db.GlobalsInstance = new Dictionary<string, XmlGlobalInstance>();
            portal.GlobalDatablocks.Add(name, db);
        }
        SiemensPortal.Current = portal; // Software/session fields remain null: no fallback live call is possible.
        var unit = Bare<XmlUnit>();
        unit.Xml = XElement.Load("Xml/EmptyUnit.xml");
        unit.Devices = new Dictionary<string, GeneratedObject>();
        unit.Networks = new Dictionary<string, XmlNetwork>();
        unit.LocalInstance = new Dictionary<string, XmlLocalInstance>();
        typeof(XmlBlock).GetField("_Name", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(unit, "fbA2");
        typeof(XmlBlock).GetField("_Namespace", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(unit, InterfaceNs);
        typeof(XmlBlock).GetField("_ObjectList", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(unit, unit.Xml.Element("SW.Blocks.FB").Element("ObjectList"));
        var network = unit.GetNetwork("A2");
        network.GetCall("fbUnit", "FB", "LocalVariable", "A2");
        var plant = Bare<XmlPlant>();
        plant.Units = new Dictionary<string, XmlUnit> { { "fbA2", unit } };
        XmlPlant.Current = plant;
        var project = Bare<PlcProject>();
        project.Plant = plant;
        project.StagingInventory = new SignalStagingInventory();
        project.Digitals = new List<PlcProject.TagDigital>();
        project.Valves = new List<PlcProject.TagValve>();
        project.Analogs = new List<PlcProject.TagAnalog>();
        project.ControlValves = new List<PlcProject.TagControlValve>();
        project.Motors = new List<PlcProject.TagMotor>();
        plant.Xml = XElement.Load("Xml/EmptyPlant.xml");
        plant.Networks = new Dictionary<string, XmlNetwork>();
        plant.LocalInstance = new Dictionary<string, XmlLocalInstance>();
        typeof(XmlBlock).GetField("_ObjectList", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(plant, plant.Xml.Element("SW.Blocks.FB").Element("ObjectList"));
        plant.GetNetwork("A2").GetCall("fbA2", "FB", "GlobalVariable", "A2");
        return project;
    }
    private static readonly XNamespace InterfaceNs = "http://www.siemens.com/automation/Openness/SW/Interface/v5";
    private static void Validate(XElement sections, XmlSchemaSet schemas)
    {
        new XDocument(new XElement(sections)).Validate(schemas, (o, e) => { throw new Exception(e.Message); });
    }
    private static string SerializedInventory(PlcProject project, XmlSchemaSet schemas)
    {
        var udts = project.StagingInventory.CreateUdts().Select(u => u.Xml).ToArray();
        foreach (var udt in udts) Validate(udt.Descendants(InterfaceNs + "Sections").Single(), schemas);
        foreach (var created in udts.SelectMany(u => u.Descendants("Created"))) created.Value = "normalized";
        // Exercise dbIO's exact member serializer without the live XmlDatablock constructor.
        var db = Bare<XmlDatablock>();
        db.SectionList = new XElement(InterfaceNs + "Section", new XAttribute("Name", "Static"));
        foreach (var plan in project.StagingInventory.Units)
        {
            var member = new XmlDataMember(plan.SafeName, "Struct", "Generated hardware IO staging for " + plan.OriginalName);
            member.Children.Add(new XmlDataMember("IN", "hwIN_" + plan.SafeName));
            member.Children.Add(new XmlDataMember("OUT", "hwOUT_" + plan.SafeName));
            db.SetMember(member);
        }
        var sections = new XElement(InterfaceNs + "Sections", db.SectionList);
        Validate(sections, schemas);
        return new XElement("Inventory", udts, sections).ToString();
    }
    private static void PidFixtures(string root)
    {
        var schemas = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
        schemas.Add(InterfaceNs.NamespaceName, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Siemens/Automation/Portal V21/PublicAPI/V21/Schemas/SW.InterfaceSections_v5.xsd"));
        string source = Path.Combine(Path.GetTempPath(), "pid-io-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllLines(source, new[] { "Call(); // Pressure PID", "Call(); // Updated PID" });
            foreach (SignalStagingMode mode in Enum.GetValues(typeof(SignalStagingMode)))
            {
                PlcProject.StagingMode = mode;
                var project = Project();
                var pid = project.AddPidControl("A2", "PID-G31-G41", 3, "bar", 2, "%", 1, source, 1);
                Assert(!project.StagingInventory.Units.Any(), "PID-only Add registered hardware IO: " + mode);
                Assert(!project.StagingInventory.CreateUdts().Any(), "PID-only UDTs created.");
                Assert(!SerializedInventory(project, schemas).Contains("Name=\"A2\""), "PID-only dbIO member created.");
                var unit = pid.Unit;
                var network = unit.FindNetwork("PID-G31-G41");
                var call = network.Calls.Values.Single();
                Assert(call.BlockName == "fbPIDCtrl" && call.InstName == "PID-G31-G41", "PID call identity changed.");
                Assert(unit.LocalInstance.Values.Count(i => i.Datatype == "fbPIDCtrl") == 1, "PID instance count/type changed.");
                Assert(unit.LocalInstance["PID-G31-G41"].Xml.Elements(InterfaceNs + "Comment").Descendants(InterfaceNs + "MultiLanguageText").Any(t => t.Value == "Pressure PID"), "Fresh PID instance description missing.");
                Assert(new[] { "TYP_ICON", "CFG", "UNIT_SP_AV", "NUM_POINTS_SP_AV", "UNIT_OUT", "NUM_POINTS_OUT", "RESET" }.All(call.Parameters.ContainsKey), "PID configuration pins missing.");
                foreach (string value in new[] { "3", "'bar'", "2", "'%'", "1" })
                    Assert(network.Access.Values.OfType<XmlConstant>().Any(c => c.Value == value), "PID configuration value missing: " + value);
                Assert(References(network).Contains("cfgData|A2_PID-G31-G41") && References(network).Contains("HMI|RESET"), "PID configuration/reset references missing.");
                Assert(References(unit.FindNetwork("A2")).Contains("PID-G31-G41|QERR"), "PID unit alarm integration missing.");
                Assert(!References(network).Any(r => r.StartsWith("hwIN|") || r.StartsWith("hwOUT|") || r.EndsWith("_SP") || r.EndsWith("_OUT")), "PID hardware operand introduced.");
                string before = network.Xml.ToString();
                Assert(object.ReferenceEquals(pid, project.AddPidControl("A2", "PID-G31-G41", callerFilePath: source, callerLineNumber: 2)), "Cached PID identity changed.");
                Assert(!project.StagingInventory.Units.Any(), "Cached PID registered IO.");
                Assert(before == network.Xml.ToString(), "Cached PID changed configuration/network.");
                Assert(unit.LocalInstance["PID-G31-G41"].Xml.Elements(InterfaceNs + "Comment").Descendants(InterfaceNs + "MultiLanguageText").Any(t => t.Value == "Updated PID"), "Cached PID description update missing.");
                Assert(!network.Xml.Descendants("Text").Any(t => t.Value == "Pressure PID" || t.Value == "Updated PID"), "PID description became network comment.");
                Assert(!project.Analogs.Any() && !project.Digitals.Any() && !project.Valves.Any() && !project.ControlValves.Any() && !project.Motors.Any(), "PID added hardware marker tags.");
                // Preparation is XML-only; Upload and artifact constructors are never called.
                var prepare = typeof(PlcProject).GetMethod("PrepareSignalStaging", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert(prepare != null, "Missing inventory-driven staging preparation.");
                prepare.Invoke(project, null);
                Assert(!unit.Xml.Descendants(InterfaceNs + "Member").Any(m => new[] { "hwIN", "hwOUT" }.Contains((string)m.Attribute("Name"))), "PID-only unit staging interface created.");
                Assert(!project.Plant.FindNetwork("A2").Calls["A2"].Parameters.Keys.Any(k => k == "hwIN" || k == "hwOUT"), "PID-only Plant binding created.");
                Validate(unit.Xml.Descendants(InterfaceNs + "Sections").Single(), schemas);
                // Existing staging/manual content is preserved even when inventory is empty.
                unit.GetInterfaceMember("Input", "hwIN", "hwIN_A2");
                unit.GetInterfaceMember("Output", "hwOUT", "hwOUT_A2");
                var plantNetwork = project.Plant.FindNetwork("A2");
                plantNetwork.SetParameter(plantNetwork.Calls["A2"], "hwIN", "Input", "hwIN_A2", "dbIO|A2|IN", true);
                before = unit.Xml.ToString() + project.Plant.Xml.ToString();
                prepare.Invoke(project, null);
                Assert(before == unit.Xml.ToString() + project.Plant.Xml.ToString(), "Empty inventory deleted existing interfaces/bindings/manual content.");

                var baseline = Project();
                baseline.AddValve("A2", "VV1008", mon_cls: true);
                // A legitimate caller owns this suffix; never filter it globally.
                baseline.StagingInventory.AddOutput("A2", "PID-G31-G41_SP", "Int", "Explicit output");
                string expected = SerializedInventory(baseline, schemas);
                project = Project();
                project.AddValve("A2", "VV1008", mon_cls: true);
                project.StagingInventory.AddOutput("A2", "PID-G31-G41_SP", "Int", "Explicit output");
                project.AddPidControl("A2", "PID-G31-G41");
                Assert(expected == SerializedInventory(project, schemas), "Mixed-unit inventory/serialized members changed with PID.");
                // Existing Plant calls may live in a network with a different title.
                project.Plant.Networks.Clear();
                var renamed = project.Plant.GetNetwork("Manual unit title");
                renamed.GetCall("fbA2", "FB", "GlobalVariable", "A2");
                prepare.Invoke(project, null);
                var prepared = project.Plant.GetUnit("A2");
                Assert(prepared.Xml.Descendants(InterfaceNs + "Member").Count(m => new[] { "hwIN", "hwOUT" }.Contains((string)m.Attribute("Name"))) == (mode == SignalStagingMode.GeneratedDbUdt ? 2 : 0), "Mixed-unit staging interface mismatch.");
                Assert(renamed.Calls["A2"].Parameters.ContainsKey("hwIN") == (mode == SignalStagingMode.GeneratedDbUdt), "Mixed-unit Plant binding mismatch.");
                Validate(prepared.Xml.Descendants(InterfaceNs + "Sections").Single(), schemas);
                Console.WriteLine("PASS: PID fresh/cached/no IO/configuration/comments and PID-only/mixed staging: " + mode);
            }
            // Constructors must defer staging until the complete inventory is known.
            foreach (string file in new[] { "Xml/XmlUnit.cs", "Xml/XmlPlant.cs" })
                Assert(!File.ReadAllText(Path.Combine(root, "TIAOpenness/TIA_Lib/TIA_LIB", file)).Contains("\"hwIN\""), "Eager staging remains in " + file);
        }
        finally { File.Delete(source); }
    }
    private static string[] References(XmlNetwork network)
    {
        return network.Access.Values.OfType<XmlGlobal>().Select(a => a.Value).ToArray();
    }
    private static void LegacyInput(XmlNetwork network, XmlCall call, string pin, string signal, bool negate)
    {
        var reference = SignalStagingInventory.InputReference(signal);
        network.SetParameter(call, pin, "Input", "Bool", reference.Value, reference.IsGlobal, false, negate);
    }
    private static XmlNetwork Reload(XmlNetwork network, string version)
    {
        var exported = new XElement(network.Xml);
        var source = exported.Descendants().Single(e => e.Name.LocalName == "FlgNet");
        XNamespace ns = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/" + version;
        foreach (var element in source.DescendantsAndSelf())
        {
            element.Attributes().Where(a => a.IsNamespaceDeclaration).Remove();
            element.Name = ns + element.Name.LocalName;
        }
        network.Xml.ReplaceWith(exported);
        network.Block.Networks.Remove(network.Name);
        return new XmlNetwork(network.Block, exported);
    }
    public static void Run(string root)
    {
        string previousDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(Path.Combine(root, "TIAOpenness/TIA_Lib/TIA_LIB"));
            PidFixtures(root);
            foreach (SignalStagingMode mode in new[] { SignalStagingMode.GeneratedDbUdt, SignalStagingMode.MarkerMemory })
            {
                PlcProject.StagingMode = mode;
                foreach (bool quality in new[] { false, true })
                foreach (int count in new[] { 1, 0, 3 })
                {
                    var project = Project();
                    var device = project.AddDigital("A2", "FI1007", instanceCount: count, qualityBit: quality);
                    var network = device.Unit.FindNetwork("FI1007");
                    var qb = SignalStagingInventory.InputReference("IN_FI1007_QB").Value;
                    Assert(References(network).Contains(qb) == quality, "Digital QB mismatch: " + mode + "/" + quality + "/" + count);
                    Assert(project.Digitals.Single().QualityBit == quality, "Marker tag QB flag differs.");
                    if (!quality)
                    {
                        var call = network.Calls[count == 0 ? "FI1007" : "FI1007_Instance1"];
                        LegacyInput(network, call, "ERR_EXT", "IN_FI1007_QB", true);
                        LegacyInput(network, call, "Manual", "IN_Manual_QB", false);
                        var wires = network.Wires.Values.Select(w => w.Xml.ToString()).ToArray();
                        project.AddDigital("A2", "FI1007", instanceCount: count, qualityBit: false);
                        Assert(!References(network).Contains(qb), "Cached digital left legacy missing QB reference.");
                        Assert(References(network).Contains(SignalStagingInventory.InputReference("IN_Manual_QB").Value), "Manual input changed.");
                        Assert(wires.SequenceEqual(network.Wires.Values.Select(w => w.Xml.ToString())), "Repair changed wire/negation topology.");
                        Assert(network.Access.Values.OfType<XmlConstant>().Any(c => c.Value == "True"), "Disabled QB did not become neutral good quality.");
                        string repaired = network.Xml.ToString();
                        project.AddDigital("A2", "FI1007", instanceCount: count, qualityBit: false);
                        Assert(repaired == network.Xml.ToString(), "Digital repair not idempotent.");
                    }
                }
                foreach (bool quality in new[] { false, true })
                foreach (bool open in new[] { false, true })
                foreach (bool close in new[] { false, true })
                {
                    var project = Project();
                    var device = project.AddValve("A2", "VV1108", mon_opn: open, mon_cls: close, qualityBit: quality);
                    var network = device.Unit.FindNetwork("VV1108");
                    foreach (var side in new[] { Tuple.Create("FB_OPN_", open), Tuple.Create("FB_CLS_", close) })
                    {
                        Assert(References(network).Contains(SignalStagingInventory.InputReference(side.Item1 + "VV1108").Value) == side.Item2, "Valve feedback mismatch: " + side.Item1 + open + close);
                        Assert(References(network).Contains(SignalStagingInventory.InputReference(side.Item1 + "VV1108_QB").Value) == (side.Item2 && quality), "Valve quality mismatch.");
                    }
                    if (!open || !close)
                    {
                        string disabled = !open ? "FB_OPN_" : "FB_CLS_";
                        var call = network.Calls["VV1108"];
                        LegacyInput(network, call, "LegacyFeedback", disabled + "VV1108", false);
                        LegacyInput(network, call, "LegacyQuality", disabled + "VV1108_QB", false);
                        LegacyInput(network, call, "Manual", "IN_ManualValve", false);
                        var wires = network.Wires.Values.Select(w => w.Xml.ToString()).ToArray();
                        project.AddValve("A2", "VV1108", mon_opn: open, mon_cls: close, qualityBit: quality);
                        Assert(!References(network).Contains(SignalStagingInventory.InputReference(disabled + "VV1108").Value), "Legacy disabled valve feedback remains.");
                        Assert(!References(network).Contains(SignalStagingInventory.InputReference(disabled + "VV1108_QB").Value), "Legacy disabled valve QB remains.");
                        Assert(References(network).Contains(SignalStagingInventory.InputReference("IN_ManualValve").Value), "Manual valve input changed.");
                        Assert(wires.SequenceEqual(network.Wires.Values.Select(w => w.Xml.ToString())), "Valve repair changed wire topology.");
                        string repaired = network.Xml.ToString();
                        project.AddValve("A2", "VV1108", mon_opn: open, mon_cls: close, qualityBit: quality);
                        Assert(repaired == network.Xml.ToString(), "Valve repair not idempotent.");
                    }
                    var expected = project.StagingInventory.CreateUdts().First().Xml.ToString();
                    foreach (var access in network.Access.Values.OfType<XmlGlobal>().Where(a => a.Components.FirstOrDefault() == "hwIN" && a.Components[1] != "ManualValve"))
                        Assert(expected.Contains("Name=\"" + access.Components[1] + "\""), "Generated valve references a missing UDT input.");
                }
                foreach (string version in new[] { "v4", "v5" })
                {
                    var project = Project();
                    var digital = project.AddDigital("A2", "LS1181", instanceCount: 1);
                    var network = digital.Unit.FindNetwork("LS1181");
                    LegacyInput(network, network.Calls["LS1181_Instance1"], "ERR_EXT", "IN_LS1181_QB", true);
                    network = Reload(network, version);
                    digital.Unit.Devices.Clear();
                    project.AddDigital("A2", "LS1181", instanceCount: 1);
                    Assert(!References(network).Contains(SignalStagingInventory.InputReference("IN_LS1181_QB").Value), "Actual digital rerun did not repair " + version);
                    var valve = project.AddValve("A2", "VV1008", mon_cls: true);
                    network = valve.Unit.FindNetwork("VV1008");
                    LegacyInput(network, network.Calls["VV1008"], "LegacyFeedback", "FB_OPN_VV1008", false);
                    LegacyInput(network, network.Calls["VV1008"], "LegacyQuality", "FB_OPN_VV1008_QB", false);
                    network = Reload(network, version);
                    valve.Unit.Devices.Clear();
                    project.AddValve("A2", "VV1008", mon_cls: true);
                    Assert(!References(network).Contains(SignalStagingInventory.InputReference("FB_OPN_VV1008").Value), "Actual valve rerun did not repair " + version);
                    Assert(!References(network).Contains(SignalStagingInventory.InputReference("FB_OPN_VV1008_QB").Value), "Actual valve QB rerun did not repair " + version);
                    Assert(network.Parts.Elements().Count(e => e.Name.LocalName == "Part") == 1, "Rerun duplicated enabled feedback logic in " + version);
                }
            }
        }
        finally { Directory.SetCurrentDirectory(previousDirectory); }
        Console.WriteLine("PASS: actual Digital/Valve Add paths, QB flags/0-1-3 monitors/all feedback combinations, both staging modes, stale reference repair, wire/manual preservation and idempotence.");
    }
}
