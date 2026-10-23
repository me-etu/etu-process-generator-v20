using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using TIA_LIB;
using TIA_LIB.Generation;
using TIA_LIB.SignalStaging;
using TIA_LIB.Xml;

public static class GeneratedPhaseModelTests
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static string ReadSource(XElement node)
    {
        switch (node.Name.LocalName)
        {
            case "Token": return (string)node.Attribute("Text");
            case "Blank": return new string(' ', (int)node.Attribute("Num"));
            case "NewLine": return new string('\n', (int)node.Attribute("Num"));
            case "Text": return node.Value;
            case "Component": return (string)node.Attribute("Name");
            case "ConstantValue": return node.Value;
            case "ConstantType": return "";
            case "LineComment": return "//" + node.Value;
            case "Parameter": return (string)node.Attribute("Name") + string.Concat(node.Elements().Select(ReadSource));
            case "Access": return ((string)node.Attribute("Scope") == "LocalVariable" ? "#" : "") + string.Concat(node.Elements().Select(ReadSource));
            case "CallInfo":
                var instance = node.Elements().Single(e => e.Name.LocalName == "Instance");
                return ((string)instance.Attribute("Scope") == "LocalVariable" ? "#" : "") +
                    string.Join(".", instance.Elements().Select(ReadSource)) + string.Concat(node.Elements().Where(e => e != instance).Select(ReadSource));
            default: return string.Concat(node.Elements().Select(ReadSource));
        }
    }
    static XElement Source(XElement document) { return document.Descendants().Single(e => e.Name.LocalName == "StructuredText"); }
    static string Normalize(string source) { return Regex.Replace(source, @"\s+", "").ToUpperInvariant(); }
    static XElement Render(XmlBlock phase, out string language)
    {
        var type = typeof(PlcProject).Assembly.GetType("TIA_LIB.Generation.ModularGenerationRuntime");
        var runtime = FormatterServices.GetUninitializedObject(type);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        type.GetField("shadows", flags).SetValue(runtime, new Dictionary<XmlBlock, XElement>());
        language = (string)type.GetMethod("BlockLanguage", flags).Invoke(runtime, new object[] { phase });
        try { return (XElement)type.GetMethod("Render", flags).Invoke(runtime, new object[] { phase }); }
        catch (TargetInvocationException error) { throw error.InnerException; }
    }
    public static void Run(string root)
    {
        var template = XElement.Load(Path.Combine(root, "TIAOpenness/TIA_Lib/TIA_LIB/Xml/EmptyPhase.xml"));
        string golden = File.ReadAllText(Path.Combine(root, "Tests/Fixtures/PhaseTemplateBody.scl"));
        Check(Normalize(ReadSource(Source(template))) == Normalize(golden), "Repository phase differs from the user's supplied legacy reference (including comments/placeholders).");
        GeneratedPhaseModel.ValidateFreshTemplate(template);
        var preserved = GeneratedPhaseModel.PreserveCompileUnit(template);
        Check(XNode.DeepEquals(preserved, template.Descendants("SW.Blocks.CompileUnit").Single()), "Phase preservation changes template tokens or presentation.");
        preserved.RemoveAll();
        Check(Source(GeneratedPhaseModel.PreserveCompileUnit(template)).HasElements, "Returned phase aliases its source.");
        var changed = new XElement(template);
        Source(changed).Descendants().First(e => e.Name.LocalName == "Token" && (string)e.Attribute("Text") == "OR").SetAttributeValue("Text", "AND");
        try { GeneratedPhaseModel.PreserveCompileUnit(changed); throw new Exception("Manual phase logic was overwritten."); }
        catch (InvalidOperationException) { }

        foreach (GenerationLanguage selected in Enum.GetValues(typeof(GenerationLanguage)))
        foreach (SignalStagingMode staging in Enum.GetValues(typeof(SignalStagingMode)))
        {
            PlcProject.Language = selected; PlcProject.StagingMode = staging;
            var phase = (XmlPhase)FormatterServices.GetUninitializedObject(typeof(XmlPhase));
            phase.Xml = new XElement(template);
            phase.Xml.Element("SW.Blocks.FB").Element("AttributeList").Element("Name").Value = "OP_Template_PH_Test";
            string snapshot = phase.Xml.ToString();
            string language;
            var rendered = Render(phase, out language);
            Check(language == "SCL" && (string)rendered.Element("SW.Blocks.FB").Element("AttributeList").Element("ProgrammingLanguage") == "SCL", "FBD selection changed the phase language.");
            Check(!rendered.Descendants().Any(e => e.Name.LocalName == "FlgNet"), "Phase was lowered to FBD.");
            Check(XNode.DeepEquals(Source(rendered), Source(template)), "Phase control flow/comments/formatting changed during runtime rendering.");
            Check(Normalize(ReadSource(Source(rendered))) == Normalize(golden), "Rendered phase differs from the independent legacy golden.");
            Check(XNode.DeepEquals(rendered.Element("SW.Blocks.FB").Element("AttributeList").Element("Interface"), phase.Xml.Element("SW.Blocks.FB").Element("AttributeList").Element("Interface")), "Phase interface/defaults/metadata changed.");
            Check(snapshot == phase.Xml.ToString(), "Rendering mutated the editable source.");
            var ownership = ManagedArtifactOwnership.Capture("Phase/Test", language, rendered, phase.Xml);
            var restored = ManagedArtifactOwnership.Read(ownership.ToXml()).Restore("Phase/Test", language, rendered);
            Check(XNode.DeepEquals(Source(restored), Source(template)), "Phase ownership round trip loses its original SCL template.");
        }
        // Interlocks also remain SCL by explicit user instruction.
        var descriptor = new OperationInterlockDescriptor("A2", "OP1", new[] { "PH-1", "PH-2" });
        var model = new GeneratedInterlockModel(descriptor);
        var fbd = new PhaseFbdRenderer().Render(model);
        var blankTemplate = XElement.Load(Path.Combine(root, "TIAOpenness/TIA_Lib/TIA_LIB/Xml/EmptyNetworkWithOutCall.xml"));
        Check(XNode.DeepEquals(fbd[0].Element("AttributeList").Element("NetworkSource"), blankTemplate.Element("AttributeList").Element("NetworkSource")), "Empty Function network uses a TIA-invalid empty graph.");
        Check(fbd[0].Descendants("MultilingualText").Single(t => (string)t.Attribute("CompositionName") == "Title").Descendants("Text").Single().Value == "Function", "Empty Function heading was removed.");
        Check(fbd.Skip(1).All(n => n.Descendants().Any(e => e.Name.LocalName == "Wires" && e.HasElements)), "Executable interlock network lost its wiring.");
        Check(fbd.SelectMany(n => n.Descendants()).Count(e => e.Name.LocalName == "Part" && (string)e.Attribute("Name") == "Coil") == descriptor.BoolMembers.Count, "Interlock FBD starter calls/writes changed.");
        var scl = new StructuredTextBodyRenderer().Render(model);
        Check(scl.Descendants().Count(e => e.Name.LocalName == "Token" && (string)e.Attribute("Text") == ":=") == descriptor.BoolMembers.Count, "Interlock SCL starter writes changed.");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var unit = (XmlUnit)FormatterServices.GetUninitializedObject(typeof(XmlUnit));
        typeof(XmlBlock).GetField("_Name", flags).SetValue(unit, "fbA2");
        var operation = (XmlOperation)FormatterServices.GetUninitializedObject(typeof(XmlOperation)); operation.Unit = unit;
        typeof(XmlBlock).GetField("_Name", flags).SetValue(operation, "OP1");
        typeof(XmlOperation).GetField("PhaseOrder", flags).SetValue(operation, new List<string> { "PH-1", "PH-2" });
        foreach (GenerationLanguage selected in Enum.GetValues(typeof(GenerationLanguage)))
        {
            PlcProject.Language = selected;
            var interlock = (XmlInterlock)FormatterServices.GetUninitializedObject(typeof(XmlInterlock));
            interlock.Xml = XElement.Load(Path.Combine(root, "TIAOpenness/TIA_Lib/TIA_LIB/Xml/EmptyBlock.xml"));
            interlock.Xml.Element("SW.Blocks.FB").Element("AttributeList").Element("Name").Value = descriptor.BlockName;
            typeof(XmlInterlock).GetField("Operation", flags).SetValue(interlock, operation);
            string language; var rendered = Render(interlock, out language);
            Check(language == "SCL" && (string)rendered.Element("SW.Blocks.FB").Element("AttributeList").Element("ProgrammingLanguage") == "SCL", "Interlock followed the selected backend instead of remaining SCL.");
            string source = ReadSource(Source(rendered));
            Check(source.Contains("REGION Function") && source.Contains("// here all interlocks for this operation will be defined") && source.Contains("// Later FALSE assignments execute every scan."), "Interlock Function comments/style differ from the reference.");
            Check(source.Contains("REGION General Interlocks") && source.Contains("REGION PH-1 Interlocks") && source.Contains("REGION PH-2 Interlocks"), "Interlock region structure changed.");
            Check(Source(rendered).Descendants().Count(e => e.Name.LocalName == "Token" && (string)e.Attribute("Text") == ":=") == descriptor.BoolMembers.Count, "Interlock lost cyclic FALSE assignments.");
            var previousFbd = new XElement(interlock.Xml);
            previousFbd.Element("SW.Blocks.FB").Element("AttributeList").Element("ProgrammingLanguage").Value = "FBD";
            previousFbd.Element("SW.Blocks.FB").Element("ObjectList").Add(new PhaseFbdRenderer().Render(model));
            var record = ManagedArtifactOwnership.Capture("TestContext|" + descriptor.BlockName, "FBD", previousFbd, previousFbd);
            var runtimeType = typeof(PlcProject).Assembly.GetType("TIA_LIB.Generation.ModularGenerationRuntime");
            var runtime = FormatterServices.GetUninitializedObject(runtimeType);
            runtimeType.GetField("context", flags).SetValue(runtime, "TestContext");
            var restore = runtimeType.GetMethod("RestoreOwnedBlock", flags);
            Check(restore.Invoke(runtime, new object[] { interlock, descriptor.BlockName, record, previousFbd }) is XElement, "Unchanged owned FBD interlock cannot follow the user-requested SCL migration.");
            var manuallyEdited = new XElement(previousFbd);
            manuallyEdited.Descendants().First(e => e.Name.LocalName == "ConstantValue").Value = "TRUE";
            try { restore.Invoke(runtime, new object[] { interlock, descriptor.BlockName, record, manuallyEdited }); throw new Exception("Manual interlock edit was overwritten during conversion."); }
            catch (TargetInvocationException error) { Check(error.InnerException is InvalidOperationException, "Unexpected interlock protection failure."); }
        }
        Console.WriteLine("PASS: exact legacy SCL phase source and SCL interlock regions/comments in both modes; runtime/interface/ownership preservation, manual-edit guards and original placeholders. No Portal execution.");
    }
}
