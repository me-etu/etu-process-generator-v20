using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Linq;
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
        return project;
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
