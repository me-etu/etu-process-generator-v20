using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using TIA_LIB.Generation;
using TIA_LIB;
using TIA_LIB.SignalStaging;
using TIA_LIB.Xml;
using System.Runtime.Serialization;

public static class SclStyleAndTypeFoldersTests
{
    const BindingFlags Hidden = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static T Make<T>(params object[] arguments) { return (T)Activator.CreateInstance(typeof(T), Hidden, null, arguments, null); }
    static GeneratedExpression Bool(string value) { return (GeneratedExpression)typeof(GeneratedExpression).GetMethod("Constant", Hidden).Invoke(null, new object[] { "Bool", value }); }
    static GeneratedExpression Logic(GeneratedExpressionKind kind, params GeneratedExpression[] terms) { return (GeneratedExpression)typeof(GeneratedExpression).GetMethod("Boolean", Hidden).Invoke(null, new object[] { kind, terms }); }
    static string Name(string name) { return System.Text.RegularExpressions.Regex.IsMatch(name, @"\A[A-Za-z_][A-Za-z_0-9]*\z") ? name : "\"" + name + "\""; }
    public static string Text(XElement node)
    {
        switch (node.Name.LocalName)
        {
            case "Token": return (string)node.Attribute("Text");
            case "Blank": return new string(' ', (int)node.Attribute("Num"));
            case "NewLine": return new string('\n', (int)node.Attribute("Num"));
            case "Text": case "ConstantValue": return node.Value;
            case "Component": return Name((string)node.Attribute("Name"));
            case "Parameter": return Name((string)node.Attribute("Name")) + string.Concat(node.Elements().Select(Text));
            case "LineComment": return "//" + node.Value;
            case "Access": return ((string)node.Attribute("Scope") == "LocalVariable" ? "#" : "") + string.Concat(node.Elements().Select(Text));
            case "CallInfo":
                var instance = node.Elements().Single(e => e.Name.LocalName == "Instance");
                return ((string)instance.Attribute("Scope") == "LocalVariable" ? "#" : "") + string.Join(".", instance.Elements().Select(Text)) + string.Concat(node.Elements().Where(e => e != instance).Select(Text));
            default: return string.Concat(node.Elements().Select(Text));
        }
    }
    static GeneratedInvocation Call(string name, GeneratedExpression enable, params GeneratedArgument[] arguments)
    { return Make<GeneratedInvocation>(name, "fbTest", new SymbolReference(false, name), enable, arguments, new GeneratedAssignment[0]); }
    static GeneratedArgument Arg(string name, GeneratedPinDirection direction, GeneratedExpression expression)
    { return Make<GeneratedArgument>(name, "Bool", direction, expression); }
    static GeneratedExpression Symbol(string name)
    { return (GeneratedExpression)typeof(GeneratedExpression).GetMethod("Reference", Hidden).Invoke(null, new object[] { new SymbolReference(false, name), "Bool" }); }
    static GeneratedNetworkModel Network(string title, string instance, params string[] pins)
    {
        return GeneratedNetworkModel.FromGeneratedFbd(title, title, new XElement("SW.Blocks.CompileUnit",
            new XElement("FlgNet", new XElement("Parts", new XElement("Call", new XAttribute("UId", "1"),
                new XElement("CallInfo", new XAttribute("Name", "fbTest"), new XAttribute("BlockType", "FB"),
                    new XElement("Instance", new XAttribute("Scope", "LocalVariable"), new XElement("Component", new XAttribute("Name", instance))),
                    pins.Select(p => new XElement("Parameter", new XAttribute("Name", p), new XAttribute("Section", "Input"), new XAttribute("Type", "Bool"))))),
                pins.Select((p, i) => new XElement("Access", new XAttribute("UId", i + 10), new XAttribute("Scope", "LiteralConstant"),
                    new XElement("Constant", new XElement("ConstantType", "Bool"), new XElement("ConstantValue", "TRUE"))))),
                new XElement("Wires", pins.Select((p, i) => new XElement("Wire", new XAttribute("UId", i + 100),
                    new XElement("IdentCon", new XAttribute("UId", i + 10)), new XElement("NameCon", new XAttribute("UId", 1), new XAttribute("Name", p))))))), new[] { "fbTest" });
    }
    static void Golden(string root, string name, string actual)
    {
        string expected = File.ReadAllText(Path.Combine(root, "Tests/Fixtures/G018-" + name + ".scl")).Replace("\r\n", "\n");
        Check(actual == expected, name + " reference golden differs.\n" + actual);
        Check(!actual.Contains("\n\n\n") && !actual.Split('\n').Any(l => l.Length != l.TrimEnd().Length), "Decorative blank runs or trailing whitespace.");
    }
    static void LayoutGoldens(string root)
    {
        var unit = new GeneratedBlockBody(new[] {
            new GeneratedLogicalNetwork(Network("U-1", "U-1", "OP_REQ", "ALARM", "WARN"), role: GeneratedNetworkRole.UnitStatus),
            new GeneratedLogicalNetwork(Network("Preconditions U-1", "precond_U-1"), role: GeneratedNetworkRole.UnitPreconditions),
            new GeneratedLogicalNetwork(Network("Unit sfc control", "sfc_U-1", "isUnit", "Reset"), role: GeneratedNetworkRole.UnitControl),
            new GeneratedLogicalNetwork(Network("DI-1", "DI-1"), DeviceCategory.Digital_Sensors, description: "Overfill protection"),
            new GeneratedLogicalNetwork(Network("AI-1", "AI-1"), DeviceCategory.Analog_Sensors),
            new GeneratedLogicalNetwork(Network("OP1", "OP1"), role: GeneratedNetworkRole.OperationCall),
            new GeneratedLogicalNetwork(Network("OP2", "OP2"), role: GeneratedNetworkRole.OperationCall) });
        Golden(root, "Unit", Text(new StructuredTextBodyRenderer().Render(unit)));
        var operation = new List<GeneratedLogicalNetwork> {
            new GeneratedLogicalNetwork(Network("OP1", "sfc_OP1"), role: GeneratedNetworkRole.OperationControl),
            new GeneratedLogicalNetwork(Network("Interlocks", "Interlocks"), role: GeneratedNetworkRole.OperationInterlocks) };
        foreach (string phase in new[] { "PH-1", "PH-2" })
        {
            operation.Add(new GeneratedLogicalNetwork(Network("Preconditions " + phase, "precond_" + phase), role: GeneratedNetworkRole.PhasePreconditions));
            operation.Add(new GeneratedLogicalNetwork(Network("Sfc control " + phase, "sfc_" + phase), role: GeneratedNetworkRole.PhaseControl));
            operation.Add(new GeneratedLogicalNetwork(Network(phase, phase), role: GeneratedNetworkRole.PhaseCall));
        }
        Golden(root, "Operation", Text(new StructuredTextBodyRenderer().Render(new GeneratedBlockBody(operation))));
        Golden(root, "Interlocks", Text(new StructuredTextBodyRenderer().Render(new GeneratedInterlockModel(new OperationInterlockDescriptor("U", "OP", new[] { "PH-1", "PH-2" })))));
        try
        {
            new StructuredTextBodyRenderer().Render(new GeneratedBlockBody(new[] { unit.Networks[5], operation[1], unit.Networks[6] }));
            throw new Exception("Noncontiguous Operations were silently reordered/grouped.");
        }
        catch (InvalidOperationException error) { Check(error.Message.Contains("not contiguous"), "Unclear grouping diagnostic."); }
        var original = Network("Manual comments", "Call").RenderFbd();
        original.Add(new XElement("MultilingualText", new XAttribute("CompositionName", "Comment"), new XElement("Text", "Manual purpose."), new XElement("Text", "Beschreibung erhalten.")));
        var withComments = GeneratedNetworkModel.FromGeneratedFbd("Manual", "Manual", original, new[] { "fbTest" });
        string comments = Text(new StructuredTextBodyRenderer().Render(new GeneratedBlockBody(new[] { new GeneratedLogicalNetwork(withComments, role: GeneratedNetworkRole.UnitStatus) })));
        Check(comments.Contains("// Manual purpose.") && comments.Contains("// Beschreibung erhalten.") && !comments.Contains("// Unit status"), "Existing multilingual purpose comments were overwritten.");
    }
    static void Enables()
    {
        var proof = typeof(StructuredTextBodyRenderer).GetMethod("ConstantEnable", Hidden);
        var comparison = (GeneratedExpression)typeof(GeneratedExpression).GetMethod("Compare", Hidden).Invoke(null, new object[] { true, "Bool", Bool("TRUE"), Bool("TRUE") });
        var expressions = new[] { Bool("TRUE"), Bool("FALSE"), Logic(GeneratedExpressionKind.Not, Bool("FALSE")), comparison,
            Logic(GeneratedExpressionKind.And, Bool("TRUE"), Logic(GeneratedExpressionKind.Or, Bool("FALSE"), Bool("TRUE"))),
            Symbol("Permit"), Symbol("ENO"), Logic(GeneratedExpressionKind.And, Bool("TRUE"), Symbol("ENO")),
            Logic(GeneratedExpressionKind.Or, Bool("TRUE"), Symbol("Permit")) };
        foreach (var expression in expressions)
        {
            bool proven = Equals(proof.Invoke(null, new object[] { expression }), true);
            var assignment = Make<GeneratedAssignment>(Symbol("Destination"), Symbol("Counter"));
            var call = Make<GeneratedInvocation>("gated", "fbTest", new SymbolReference(false, "Counter"), expression,
                new[] { Arg("HAS_CALL", GeneratedPinDirection.Input, Bool("TRUE")), Arg("LAST_CALL", GeneratedPinDirection.Input, Bool("TRUE")) }, new[] { assignment });
            var region = Make<GeneratedRegion>("Gating", new[] { Make<GeneratedStatement>(call, false) }, new GeneratedRegion[0]);
            var xml = new StructuredTextBodyRenderer().Render(new GeneratedStructuredBody(new[] { region }));
            string source = Text(xml);
            Check(source.Contains("IF ") == !proven, "Unknown/false/ENO gate removed.");
            Check(source.Contains("HAS_CALL := TRUE") && source.Contains("LAST_CALL := TRUE"), "Meaningful TRUE parameters changed.");
            Check(source.IndexOf("Destination :=", StringComparison.Ordinal) > source.IndexOf(");", StringComparison.Ordinal), "Following assignment moved before invocation.");
            int originalState = 0, simplifiedState = 0;
            for (int scan = 0; scan < 64; scan++)
            {
                bool enabled = Evaluate(expression, scan);
                if (enabled) originalState++;
                if (proven || enabled) simplifiedState++;
                Check(originalState == simplifiedState, "Call counts/state/following output assignment differs across scans.");
            }
        }
    }
    static bool Evaluate(GeneratedExpression expression, int scan)
    {
        switch (expression.Kind)
        {
            case GeneratedExpressionKind.Literal: return expression.Literal == "TRUE";
            case GeneratedExpressionKind.Symbol: return (scan & (expression.Symbol.Components[0] == "ENO" ? 2 : 1)) != 0;
            case GeneratedExpressionKind.Not: return !Evaluate(expression.Operands[0], scan);
            case GeneratedExpressionKind.And: return expression.Operands.All(e => Evaluate(e, scan));
            case GeneratedExpressionKind.Or: return expression.Operands.Any(e => Evaluate(e, scan));
            case GeneratedExpressionKind.Equal: return Evaluate(expression.Operands[0], scan) == Evaluate(expression.Operands[1], scan);
            default: throw new Exception("Unexpected fixture expression.");
        }
    }
    static void Reject(Action action, string expected)
    {
        try { action(); throw new Exception("Accepted " + expected); }
        catch (InvalidOperationException error) { Check(error.Message.Contains(expected), "Wrong diagnostic: " + error.Message); }
    }
    static void Folders()
    {
        foreach (string unit in new[] { "U-1", "U2" })
        foreach (string prefix in new[] { "hwIN_", "hwOUT_", "udtCfg_" })
        {
            string safe = SignalStagingInventory.SafeName(unit), name = prefix + safe;
            Check(UnitTypeFolderPolicy.Target(name, new[] { "U-1", "U2" }).SequenceEqual(new[] { safe }), "Type folder ownership differs.");
            var doc = XElement.Parse("<Document><Member Name='Keep' Datatype='Int' Version='0.1'><Comment>Preserve</Comment><StartValue>7</StartValue></Member></Document>");
            string snapshot = doc.ToString();
            var record = UnitTypeFolderPolicy.Capture("Project|CPU|" + name, name, new[] { safe }, doc);
            UnitTypeFolderPolicy.Validate(XElement.Parse(record.ToString()), "Project|CPU|" + name, name, new[] { safe }, new[] { safe }, new XElement(doc));
            Check(doc.ToString() == snapshot, "Folder policy changed schema/values/comments.");
            Reject(() => UnitTypeFolderPolicy.Validate(record, "OtherCPU", name, new[] { safe }, new[] { safe }, doc), "ownership");
            Reject(() => UnitTypeFolderPolicy.Validate(null, "Project|CPU|" + name, name, new[] { safe }, new[] { safe }, doc), "ownership");
            Reject(() => UnitTypeFolderPolicy.Validate(record, "Project|CPU|" + name, name, new[] { "Wrong" }, new[] { safe }, doc), "folder");
            var changed = new XElement(doc); changed.Descendants("StartValue").Single().Value = "8";
            Reject(() => UnitTypeFolderPolicy.Validate(record, "Project|CPU|" + name, name, new[] { safe }, new[] { safe }, changed), "changed after generation");
            record.SetAttributeValue("Version", "1"); record.Attribute("ExportHash").Remove(); record.Element("Folder").Remove();
            Reject(() => UnitTypeFolderPolicy.Validate(record, "Project|CPU|" + name, name, new string[0], new[] { safe }, doc), "no verified identity-preserving move");
        }
        Reject(() => UnitTypeFolderPolicy.Target("hwIN_Unknown", new[] { "U1" }), "No completed Unit");
        Reject(() => UnitTypeFolderPolicy.Target("hwIN_U_1", new[] { "U-1", "U_1" }), "collision");
        var empty = new SignalStagingInventory(); Check(!empty.CreateUdts().Any(), "Empty/PID-only model invented IO types.");
    }
    static object Fixture(string method, params object[] arguments)
    {
        try { return typeof(DeviceInstanceComments).GetMethod(method, Hidden).Invoke(null, arguments); }
        catch (TargetInvocationException error) { throw error.InnerException; }
    }
    static T Bare<T>() { return (T)FormatterServices.GetUninitializedObject(typeof(T)); }
    static XElement RuntimeRender(XmlBlock block)
    {
        var type = typeof(PlcProject).Assembly.GetType("TIA_LIB.Generation.ModularGenerationRuntime");
        var runtime = FormatterServices.GetUninitializedObject(type);
        type.GetField("shadows", Hidden).SetValue(runtime, new Dictionary<XmlBlock, XElement>());
        try { return (XElement)type.GetMethod("Render", Hidden).Invoke(runtime, new object[] { block }); }
        catch (TargetInvocationException error) { throw error.InnerException; }
    }
    static void RuntimeOperation()
    {
        PlcProject.Language = GenerationLanguage.Scl;
        var unit = (XmlUnit)Fixture("Unit", "RuntimeUnit", "en-US");
        var operation = Bare<XmlOperation>();
        operation.Xml = XElement.Load("Xml/EmptyOperation.xml");
        typeof(XmlBlock).GetField("_Namespace", Hidden).SetValue(operation, (XNamespace)"http://www.siemens.com/automation/Openness/SW/Interface/v5");
        typeof(XmlBlock).GetField("_Name", Hidden).SetValue(operation, "OP-1");
        typeof(XmlBlock).GetField("_ObjectList", Hidden).SetValue(operation, operation.Xml.Descendants("SW.Blocks.FB").Elements("ObjectList").Single());
        operation.LocalInstance = new Dictionary<string, XmlLocalInstance>(); operation.Networks = new Dictionary<string, XmlNetwork>();
        operation.Unit = unit;
        operation.Phases = new Dictionary<string, XmlPhase>();
        typeof(XmlOperation).GetField("PhaseOrder", Hidden).SetValue(operation, new List<string> { "PH-1", "PH-2" });
        operation.GetNetwork("OP-1").GetCall("fbControlSfc", "FB", "LocalVariable", "sfc_OP-1");
        operation.GetNetwork("Interlocks").GetCall("OP-1_Interlocks", "FB", "LocalVariable", "Interlocks");
        foreach (string phase in new[] { "PH-1", "PH-2" })
        {
            operation.Phases.Add(phase, Bare<XmlPhase>());
            operation.GetNetwork("Preconditions " + phase).GetCall("fbIntlck50", "FB", "LocalVariable", "precond_" + phase);
            operation.GetNetwork("Sfc control " + phase).GetCall("fbControlSfc", "FB", "LocalVariable", "sfc_" + phase);
            operation.GetNetwork(phase).GetCall("OP-1_" + phase, "FB", "LocalVariable", phase);
        }
        unit.Operations = new Dictionary<string, XmlOperation> { { "OP-1", operation } };
        unit.GetNetwork("OP-1").GetCall("OP-1", "FB", "LocalVariable", "OP-1");
        string unitText = Text(RuntimeRender(unit).Descendants().Single(e => e.Name.LocalName == "StructuredText"));
        Check(unitText.Contains("REGION Operations\n    // Operations under this unit\n    REGION OP-1"), "Actual Unit operation ownership was not grouped.");
        string original = operation.Xml.ToString();
        string text = Text(RuntimeRender(operation).Descendants().Single(e => e.Name.LocalName == "StructuredText"));
        Check(text.Split(new[] { "REGION *****************\nEND_REGION" }, StringSplitOptions.None).Length == 3, "Actual Operation phase inventory did not produce separators.");
        foreach (string comment in new[] { "Operation status control", "Interlocks for devices and phases assigned to this operation", "Please assign all pre-conditions for this phase here", "Status control of this phase with its non-technical parameters", "Phase sequence logic" })
            Check(text.Contains("// " + comment), "Actual Operation role comment missing: " + comment);
        Check(original == operation.Xml.ToString(), "Operation presentation changed FBD shadow.");
    }
    static void FamilyMatrix()
    {
        var originalPortal = SiemensPortal.Current; var originalPlant = XmlPlant.Current;
        var modeBefore = PlcProject.StagingMode; var languageBefore = PlcProject.Language;
        string source = Path.Combine(Path.GetTempPath(), "G018-source-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(source, "Call(); // Owner description");
        var portal = Bare<SiemensPortal>();
        portal.GlobalDatablocks = new Dictionary<string, XmlDatablock>();
        foreach (string name in new[] { "HMI", "cfgData", "F_Safe" })
        {
            var db = Bare<XmlDatablock>(); db.SectionList = new XElement("Section"); db.GlobalsInstance = new Dictionary<string, XmlGlobalInstance>();
            portal.GlobalDatablocks.Add(name, db);
        }
        SiemensPortal.Current = portal;
        try
        {
            foreach (SignalStagingMode mode in Enum.GetValues(typeof(SignalStagingMode)))
            foreach (string culture in new[] { "en-US,de-DE", "de-DE" })
            foreach (string method in new[] { "AddAnalog", "AddDigital", "AddValve", "AddValveControl", "AddMotor", "AddMotorControl", "AddPidControl" })
            foreach (int count in new[] { 0, 1, 3 })
            foreach (int safe in method == "AddAnalog" || method == "AddDigital" || method == "AddPidControl" ? new[] { 0 } : new[] { 0, 2 })
            {
                PlcProject.StagingMode = mode;
                var unit = (XmlUnit)Fixture("Unit", "U-1", culture);
                var project = (PlcProject)Fixture("Project", unit);
                Fixture("Add", project, method, "Device-1", count, source, 1, count, safe, null);
                var network = unit.FindNetwork("Device-1");
                var allowed = unit.LocalInstance.Values.Select(i => i.Datatype.Trim('"')).ToArray();
                var model = GeneratedNetworkModel.FromGeneratedFbd("U/Device", "Device-1", network.Xml, allowed);
                string original = unit.Xml.ToString();
                var renderer = new StructuredTextBodyRenderer();
                var tokens = renderer.Render(new GeneratedBlockBody(new[] { new GeneratedLogicalNetwork(model) }));
                Check(XNode.DeepEquals(model.RenderFbd(), network.Xml) && original == unit.Xml.ToString(), "Enable/layout change mutated FBD organizational logic or comments.");
                Check(tokens.Descendants().Where(e => e.Name.LocalName == "CallInfo").Select(e => e.Elements().Single(c => c.Name.LocalName == "Instance").Elements().Single().Attribute("Name").Value)
                    .SequenceEqual(model.Invocations.Select(c => c.Instance.Components[0])), "Helper/base execution order or call count changed.");
                int expectedIfs = model.Invocations.Count(c => !Equals(typeof(StructuredTextBodyRenderer).GetMethod("ConstantEnable", Hidden).Invoke(null, new object[] { c.Enable }), true));
                Check(tokens.Descendants().Count(e => e.Name.LocalName == "Token" && (string)e.Attribute("Text") == "IF") == expectedIfs, "Helper IF policy differs from proof.");
                // Compare the gate transformation over successive scans. Vendor FB
                // implementations are deliberately external; this oracle models a
                // stateful call with a changing output and checks assignment order.
                var before = new Dictionary<string, int>(); var after = new Dictionary<string, int>();
                for (int scan = 0; scan < 16; scan++)
                foreach (var invocation in model.Invocations)
                {
                    string id = invocation.Identity;
                    if (!before.ContainsKey(id)) { before[id] = 0; after[id] = 0; }
                    bool enabled = Evaluate(invocation.Enable, scan);
                    bool proven = Equals(typeof(StructuredTextBodyRenderer).GetMethod("ConstantEnable", Hidden).Invoke(null, new object[] { invocation.Enable }), true);
                    if (enabled) before[id]++;
                    if (proven || enabled) after[id]++;
                    Check(before[id] == after[id], "State/output divergence for " + method + "/" + id);
                }
                string text = Text(tokens);
                foreach (var argument in model.Invocations.SelectMany(c => c.Arguments).Where(a => a.Name == "HAS_CALL" || a.Name == "LAST_CALL"))
                    Check(text.Contains(argument.Name + " := " + argument.Expression.RenderScl()), "Meaningful helper flag changed.");
                foreach (GenerationLanguage language in Enum.GetValues(typeof(GenerationLanguage)))
                {
                    PlcProject.Language = language;
                    var rendered = RuntimeRender(unit);
                    if (language == GenerationLanguage.Scl)
                    {
                        string body = Text(rendered.Descendants().Single(e => e.Name.LocalName == "StructuredText"));
                        Check(body.Split(new[] { "// Owner description" }, StringSplitOptions.None).Length == 2, "Description missing/duplicated per helper owner.");
                        Check(body.Contains("// Unit status") && body.Contains("// All assigned IOs to this unit"), "Runtime semantic roles missing.");
                        Check(XNode.DeepEquals(rendered, RuntimeRender(unit)), "Runtime rerender differs.");
                    }
                    else
                    {
                        var modes = new UnitModePlan(unit);
                        int added = (modes.Off.Count == 0 ? 0 : 1) + (modes.Service.Count == 0 ? 0 : 1);
                        Check(rendered.Descendants().Count(e => e.Name.LocalName == "FlgNet") == unit.Networks.Count + added, "FBD changed original networks or duplicated mode groups.");
                        Check(rendered.Descendants().Count(e => e.Name.LocalName == "CallInfo") == unit.Networks.Values.Sum(n => n.Calls.Count), "Mode networks changed device invocation count.");
                    }
                    Check(unit.Xml.ToString() == original, "Runtime changed editable shadow/declaration comments.");
                }
            }
            RuntimeOperation();
            Check(portal.Portal == null && portal.PlcSoftware == null && portal.OpenProject == null, "Fixture accessed live Portal.");
        }
        finally { SiemensPortal.Current = originalPortal; XmlPlant.Current = originalPlant; PlcProject.StagingMode = modeBefore; PlcProject.Language = languageBefore; File.Delete(source); }
    }
    static void StyleOwnership()
    {
        var shadow = new XElement("Document", new XElement("SW.Blocks.FB", new XElement("AttributeList", new XElement("Name", "Unit"), new XElement("ProgrammingLanguage", "FBD")),
            new XElement("ObjectList", Network("Old", "Call").RenderFbd())));
        var previous = new XElement(shadow); previous.Element("SW.Blocks.FB").Element("AttributeList").Element("ProgrammingLanguage").Value = "SCL";
        previous.Descendants("SW.Blocks.CompileUnit").Single().ReplaceWith(new XElement("SW.Blocks.CompileUnit", new XElement("OldStyle", "Recorded export including comments")));
        var ledger = ManagedArtifactOwnership.Capture("Project|CPU|Unit", "SCL", previous, shadow).ToXml();
        var restored = ManagedArtifactOwnership.Read(ledger).Restore("Project|CPU|Unit", "SCL", new XElement(previous));
        Check(XNode.DeepEquals(restored.Descendants("SW.Blocks.CompileUnit").Single(), shadow.Descendants("SW.Blocks.CompileUnit").Single()), "Prior-version style did not restore verified FBD shadow.");
        var edited = new XElement(previous); edited.Descendants("OldStyle").Single().Value += " manual note";
        Reject(() => ManagedArtifactOwnership.Read(ledger).Restore("Project|CPU|Unit", "SCL", edited), "changed after the recorded generation");
        var current = new XElement(previous); current.Descendants("SW.Blocks.CompileUnit").Single().ReplaceWith(new XElement("SW.Blocks.CompileUnit", new StructuredTextBodyRenderer().Render(new GeneratedBlockBody(new[] { new GeneratedLogicalNetwork(Network("New", "Call")) }))));
        ManagedArtifactOwnership.Read(ManagedArtifactOwnership.Capture("Project|CPU|Unit", "SCL", current, shadow).ToXml()).Restore("Project|CPU|Unit", "SCL", current);
    }
    public static void Run(string root)
    {
        var call = Call("LC-B13", Logic(GeneratedExpressionKind.And, Bool("TRUE"), Logic(GeneratedExpressionKind.Not, Bool("FALSE"))),
            Arg("HAS_CALL", GeneratedPinDirection.Input, Bool("TRUE")), Arg("Q", GeneratedPinDirection.Output, Symbol("Destination")));
        var region = Make<GeneratedRegion>("Fixture", new[] { Make<GeneratedStatement>(call, false) }, new string[] { "Keep this comment." }, new GeneratedRegion[0]);
        var model = new GeneratedStructuredBody(new[] { region });
        var renderer = new StructuredTextBodyRenderer();
        var xml = renderer.Render(model);
        string text = Text(xml);
        Check(!text.Contains("IF "), "Pure constant TRUE enable still emits an IF wrapper.");
        string golden = "REGION Fixture\n    // Keep this comment.\n    #\"LC-B13\"(HAS_CALL := TRUE,\n              Q => #Destination);\n\nEND_REGION\n";
        Check(text == golden, "Multiline call alignment/indentation/operator spacing/comment/blank line differs.\n" + text);
        Check(XNode.DeepEquals(xml, renderer.Render(model)), "Renderer reuse is not deterministic.");
        var single = Call("Single", Symbol("Permit"), Arg("IN", GeneratedPinDirection.Input, Bool("TRUE")));
        var child = Make<GeneratedRegion>("Child", new[] { Make<GeneratedStatement>(single, false) }, new GeneratedRegion[0]);
        var nested = Make<GeneratedRegion>("Parent", new GeneratedStatement[0], new[] { child });
        string conditional = Text(renderer.Render(new GeneratedStructuredBody(new[] { nested })));
        Check(conditional.Contains("        IF #Permit THEN\n            #Single(IN := TRUE);\n        END_IF;"), "One-argument conditional/nested-region indentation differs.");
        LayoutGoldens(root); Enables(); Folders(); StyleOwnership(); FamilyMatrix();
        Console.WriteLine("PASS: G-018 reference goldens, layout, comment preservation, enable scan parity/counterexamples, and Unit type-folder/ownership policy.");
    }
}
