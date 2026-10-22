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

public static class SfcHssTests
{
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static T Bare<T>() { return (T)FormatterServices.GetUninitializedObject(typeof(T)); }
    static XmlBlock Block(bool unit)
    {
        XmlBlock block = unit ? (XmlBlock)Bare<XmlUnit>() : Bare<XmlOperation>();
        block.Xml = XElement.Load("Xml/EmptyUnit.xml");
        block.Networks = new Dictionary<string, XmlNetwork>();
        block.LocalInstance = new Dictionary<string, XmlLocalInstance>();
        typeof(XmlBlock).GetField("_Name", Private).SetValue(block, unit ? "fbA2" : "OP1");
        typeof(XmlBlock).GetField("_ObjectList", Private).SetValue(block, block.Xml.Element("SW.Blocks.FB").Element("ObjectList"));
        return block;
    }
    static XmlCall Configure(XmlBlock block, string title, string instance, out bool created)
    {
        var method = typeof(XmlBlock).GetMethod("ConfigureSfcControl", Private);
        Check(method != null, "Missing shared unit/operation/phase HSS configuration path.");
        object[] args = { title, instance, false };
        try
        {
            var call = (XmlCall)method.Invoke(block, args);
            created = (bool)args[2];
            return call;
        }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
    }
    static XmlCall Configure(XmlBlock block, string title, string instance)
    {
        bool created;
        return Configure(block, title, instance, out created);
    }
    static void Reload(XmlBlock block, string version)
    {
        var originals = block.Networks.Values.ToArray();
        block.Networks.Clear();
        foreach (var network in originals)
        {
            var xml = new XElement(network.Xml);
            var source = xml.Descendants().Single(e => e.Name.LocalName == "FlgNet");
            XNamespace ns = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/" + version;
            foreach (var element in source.DescendantsAndSelf())
            {
                element.Attributes().Where(a => a.IsNamespaceDeclaration).Remove();
                element.Name = ns + element.Name.LocalName;
            }
            network.Xml.ReplaceWith(xml);
            new XmlNetwork(block, xml);
        }
    }
    static void Verify(XmlCall call)
    {
        var network = call.Network;
        var ns = network.SourceNamespace;
        Check(call.BlockName == "fbControlSfc" && call.BlockType == "FB" && call.InstType == "LocalVariable", "Wrong controller identity.");
        Check(call.CallInfo.Elements().Count(p => p.Name.LocalName == "Parameter" && (string)p.Attribute("Name") == "HSS") == 1, "Duplicate/missing HSS parameter.");
        var parameter = call.Parameters["HSS"];
        Check(parameter.Section == "Input" && parameter.Type == "Bool", "Wrong HSS parameter type/section.");
        var endpoint = network.Xml.Descendants().Single(e => e.Name.LocalName == "NameCon" && (string)e.Attribute("UId") == call.UId.ToString() && (string)e.Attribute("Name") == "HSS");
        var id = (int)endpoint.Parent.Elements().Single(e => e.Name.LocalName == "IdentCon").Attribute("UId");
        var access = network.Access[id] as XmlGlobal;
        Check(access != null && (string)access.Xml.Attribute("Scope") == "GlobalVariable" && access.Components.SequenceEqual(new[] { "F_Safe", "HSS" }), "Incorrect HSS operand/scope.");
        Check(!call.Xml.Elements().Any(e => e.Name.LocalName == "Negated" && (string)e.Attribute("Name") == "HSS") && !call.Negations.Contains("HSS"), "HSS inversion remains.");
        Check(network.UIdIdentCon[call.UId + "|HSS"] == id.ToString(), "HSS access cache mismatch.");
        var wire = network.Wires.Values.Single(w => w.Xml == endpoint.Parent);
        Check(wire.Contains(parameter) && wire.Value == access && network.GetPinValue(parameter) == access, "HSS wire/pin caches mismatch.");
        Check(!network.OpenConWires.ContainsKey(call.UId + "|HSS"), "Stale HSS open-connection cache.");
        Check(!network.Xml.Descendants().Where(e => e.Name.LocalName == "NameCon").GroupBy(e => (string)e.Attribute("UId") + "|" + (string)e.Attribute("Name")).Any(g => g.Count() != 1), "Duplicate endpoint.");
    }
    static string WithoutHss(XmlBlock block, XmlCall call)
    {
        var copy = new XElement(block.Xml);
        var ns = call.Network.SourceNamespace;
        var network = copy.Descendants("SW.Blocks.CompileUnit").Single(n => n.Descendants("MultilingualText")
            .Where(t => (string)t.Attribute("CompositionName") == "Title").Descendants("Text").Any(t => t.Value == call.Network.Name));
        var controller = network.Descendants().Single(c => c.Name.LocalName == "Call" && (string)c.Attribute("UId") == call.UId.ToString());
        controller.Descendants().Where(e => e.Name.LocalName == "Parameter" && (string)e.Attribute("Name") == "HSS").Remove();
        controller.Elements().Where(e => e.Name.LocalName == "Negated" && (string)e.Attribute("Name") == "HSS").Remove();
        var ends = network.Descendants().Where(e => e.Name.LocalName == "NameCon" && (string)e.Attribute("UId") == call.UId.ToString() && (string)e.Attribute("Name") == "HSS").ToArray();
        foreach (var end in ends)
        {
            var wire = end.Parent;
            end.Remove();
            if (!wire.Elements().Any(e => e.Name.LocalName == "NameCon")) wire.Remove();
        }
        // Accesses may be retained for other/manual users; compare those separately.
        network.Descendants().Where(e => e.Name.LocalName == "Access").Remove();
        return copy.ToString();
    }
    static void CachedAddPaths()
    {
        var unit = (XmlUnit)Block(true);
        var operation = (XmlOperation)Block(false);
        operation.Unit = unit;
        operation.Phases = new Dictionary<string, XmlPhase>();
        unit.Operations = new Dictionary<string, XmlOperation> { { "OP1", operation } };
        Configure(unit, "Unit sfc control", "sfc_A2");
        Configure(operation, "OP1", "sfc_OP1");
        foreach (string name in new[] { "PH1", "PH2" })
        {
            var phase = Bare<XmlPhase>();
            phase.Op = operation;
            operation.Phases.Add(name, phase);
            Configure(operation, "Sfc control " + name, "sfc_" + name);
        }
        var plant = Bare<XmlPlant>();
        plant.Units = new Dictionary<string, XmlUnit> { { "fbA2", unit } };
        XmlPlant.Current = plant;
        var project = Bare<PlcProject>();
        project.Plant = plant;
        project.StagingInventory = new SignalStagingInventory();
        string before = unit.Xml.ToString() + operation.Xml.ToString();
        unit.HasChanged = operation.HasChanged = false;
        Check(object.ReferenceEquals(unit, project.AddUnit("A2")) && object.ReferenceEquals(operation, project.AddOP("A2", "OP1")), "Cached unit/operation identity changed.");
        foreach (string name in new[] { "PH1", "PH2" })
            Check(object.ReferenceEquals(operation.Phases[name], project.AddPH("A2", "OP1", name)), "Cached phase identity changed.");
        Check(before == unit.Xml.ToString() + operation.Xml.ToString() && !unit.HasChanged && !operation.HasChanged, "Cached Add paths changed configured HSS/XML.");
        Check(!project.StagingInventory.Units.Any(), "HSS contributed hardware IO.");
        foreach (var network in unit.Networks.Values.Concat(operation.Networks.Values)) Verify(network.Calls.Values.Single());
    }
    public static void Run(string root)
    {
        var directory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(Path.Combine(root, "TIAOpenness/TIA_Lib/TIA_LIB"));
            var portal = Bare<SiemensPortal>();
            portal.GlobalDatablocks = new Dictionary<string, XmlDatablock>();
            SiemensPortal.Current = portal; // No live session. HSS must not invent a DB/member.
            foreach (SignalStagingMode mode in Enum.GetValues(typeof(SignalStagingMode)))
            foreach (OperationInterlockLanguage language in Enum.GetValues(typeof(OperationInterlockLanguage)))
            foreach (string version in new[] { "v4", "v5" })
            foreach (string route in new[] { "Unit", "Operation", "Phase" })
            {
                PlcProject.StagingMode = mode;
                PlcProject.InterlockLanguage = language;
                var block = Block(route == "Unit");
                string title = route == "Unit" ? "Unit sfc control" : route == "Operation" ? "OP1" : "Sfc control PH1";
                string instance = "sfc_" + (route == "Unit" ? "A2" : route == "Operation" ? "OP1" : "PH1");
                bool created;
                var call = Configure(block, title, instance, out created);
                Check(created, "New route was not initialized.");
                Verify(call);
                var manual = block.GetNetwork("Manual network");
                manual.SetComment("Keep my manual text");
                if (route == "Phase") Verify(Configure(block, "Sfc control PH2", "sfc_PH2"));
                for (int cycle = 0; cycle < 3; cycle++)
                {
                    if (cycle != 0) Reload(block, version);
                    call = block.Networks[title].Calls[instance];
                    string before = block.Xml.ToString();
                    block.HasChanged = false;
                    Configure(block, title, instance, out created);
                    Check(!created && !block.HasChanged && before == block.Xml.ToString(), "Correct HSS processing is not a no-op.");
                    Verify(call);
                }
                // Existing selectors with no HSS must be resolved, rather than duplicated.
                foreach (string scenario in new[] { "missing", "wrong", "local", "globalWrong", "negated", "shared", "correctShared", "wrongType", "open", "logic", "multiple", "duplicateParameter" })
                {
                    block = Block(route == "Unit");
                    var network = block.GetNetwork(title);
                    call = network.GetCall("fbControlSfc", "FB", "LocalVariable", instance);
                    network.SetParameter(call, "Manual", "Input", "Bool", "True");
                    network.SetComment("Keep existing controller comment");
                    if (scenario == "negated" || scenario == "correctShared")
                    {
                        Configure(block, title, instance);
                        if (scenario == "negated") call.GetParameter("HSS", "Input", "Bool", true);
                        else network.Wires.Values.Last().AddNameCon(call.GetParameter("SharedManual", "Input", "Bool"));
                    }
                    else if (scenario == "open") network.SetParameter(call, "HSS", "Input", "Bool");
                    else if (scenario == "logic")
                    {
                        var logic = new XmlLogic(network, XmlLogicType.Or);
                        network.SetPin(logic, "in1", "Bool", "False");
                        network.SetParameter(call, "HSS", "Input", logic.GetPin("out"));
                    }
                    else if (scenario != "missing")
                    {
                        network.SetParameter(call, "HSS", scenario == "wrongType" ? "Output" : "Input", scenario == "wrongType" ? "Int" : "Bool",
                            scenario == "local" ? "F_Safe|HSS" : scenario == "globalWrong" ? "Other|HSS" : "False");
                        if (scenario == "globalWrong") network.Access.Values.OfType<XmlGlobal>().Single().Xml.SetAttributeValue("Scope", "GlobalVariable");
                        if (scenario == "shared")
                        {
                            var other = call.GetParameter("SharedManual", "Input", "Bool");
                            network.Wires.Values.Last().AddNameCon(other);
                        }
                        if (scenario == "multiple") network.SetPin(call.Parameters["HSS"], new XmlConstant(network, "Bool", "True"));
                    }
                    Reload(block, version);
                    network = block.Networks[title];
                    call = network.Calls[instance];
                    // Duplicate parameters cannot be parsed by the generic call reader;
                    // exercise targeted rejection without rebuilding its parameter cache.
                    if (scenario == "duplicateParameter") call.CallInfo.Add(new XElement(call.Parameters["HSS"].Xml));
                    string snapshot = block.Xml.ToString();
                    string preserved = WithoutHss(block, call);
                    var oldAccesses = network.Access.Values.Select(a => a.Xml.ToString()).ToArray();
                    block.HasChanged = false;
                    if (scenario == "multiple" || scenario == "logic" || scenario == "duplicateParameter")
                    {
                        try { Configure(block, title, instance); throw new Exception("Ambiguous HSS accepted."); }
                        catch (InvalidOperationException ex)
                        {
                            Check(ex.Message.Contains(instance) && ex.Message.Contains(title) && ex.Message.Contains("HSS"), "Missing HSS diagnostic context.");
                        }
                        Check(snapshot == block.Xml.ToString() && !block.HasChanged, "Rejected topology mutated XML.");
                        continue;
                    }
                    Configure(block, title, instance);
                    Verify(call);
                    Check(block.HasChanged == (scenario != "correctShared") && preserved == WithoutHss(block, call), "HSS reconciliation changed unrelated XML: " + route + "/" + version + "/" + scenario);
                    Check(oldAccesses.All(a => network.Access.Values.Any(v => v.Xml.ToString() == a)), "Existing shared/manual operand changed.");
                    snapshot = block.Xml.ToString();
                    block.HasChanged = false;
                    Configure(block, title, instance);
                    Check(snapshot == block.Xml.ToString() && !block.HasChanged, "Reconciliation not idempotent.");
                    for (int reopen = 0; reopen < 2; reopen++)
                    {
                        // Parse exactly what was written; do not normalize away namespace mistakes.
                        var exported = new XElement(network.Xml);
                        network.Xml.ReplaceWith(exported);
                        block.Networks.Remove(title);
                        network = new XmlNetwork(block, exported);
                        call = network.Calls[instance];
                        snapshot = block.Xml.ToString();
                        block.HasChanged = false;
                        Configure(block, title, instance);
                        Verify(call);
                        Check(snapshot == block.Xml.ToString() && !block.HasChanged, "Exact reopened HSS connection changed.");
                    }
                }
                foreach (string invalid in new[] { "missingCall", "wrongBlock", "wrongScope", "wrongDeclaration" })
                {
                    block = Block(route == "Unit");
                    var network = block.GetNetwork(title);
                    if (invalid != "missingCall")
                    {
                        var bad = network.GetCall(invalid == "wrongBlock" ? "ManualFB" : "fbControlSfc", "FB",
                            invalid == "wrongScope" ? "GlobalVariable" : "LocalVariable", instance);
                        if (invalid == "wrongDeclaration") block.LocalInstance[instance].Datatype = "ManualFB";
                    }
                    string snapshot = block.Xml.ToString();
                    block.HasChanged = false;
                    try { Configure(block, title, instance); throw new Exception("Invalid existing controller accepted."); }
                    catch (InvalidOperationException ex) { Check(ex.Message.Contains(instance) && ex.Message.Contains(title), "Missing call-resolution diagnostic context."); }
                    Check(snapshot == block.Xml.ToString() && !block.HasChanged, "Rejected call resolution changed XML.");
                }
                CachedAddPaths();
            }
            Check(portal.GlobalDatablocks.Count == 0, "HSS invented/mutated a global DB/member.");
            Console.WriteLine("PASS: SFC HSS unit/operation/multi-phase new/reopened v4-v5, both modes/languages, reconciliation, preservation, cache/dirty-state/idempotence and ambiguous topology rejection.");
        }
        finally { Directory.SetCurrentDirectory(directory); }
    }
}
