using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Linq;
using TIA_LIB;
using TIA_LIB.Xml;
using TIA_LIB.SignalStaging;

public static class OperationInterlockTests
{
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    private static void Reject(Action action, string expected)
    {
        try { action(); }
        catch (InvalidOperationException error)
        {
            Assert(error.Message.Contains(expected), "Wrong diagnostic: " + error.Message);
            return;
        }
        throw new Exception("Expected rejection: " + expected);
    }
    private sealed class Backend : IOperationInterlockBackend
    {
        public readonly Dictionary<string, List<OperationInterlockExisting>> Existing = new Dictionary<string, List<OperationInterlockExisting>>();
        public int Writes;
        public bool Fail;
        public IEnumerable<OperationInterlockExisting> Find(OperationInterlockDescriptor descriptor)
        {
            List<OperationInterlockExisting> values;
            return Existing.TryGetValue(descriptor.Identity, out values) ? values : new List<OperationInterlockExisting>();
        }
        public void Generate(OperationInterlockDescriptor descriptor)
        {
            Writes++;
            if (Fail) throw new InvalidOperationException("fixture compile error");
        }
        public void Add(OperationInterlockDescriptor descriptor, XElement document, bool target)
        {
            Existing.Add(descriptor.Identity, new List<OperationInterlockExisting> {
                new OperationInterlockExisting { Document = document, InTargetFolder = target, Path = "CPU/Other/" + descriptor.BlockName }
            });
        }
    }
    private static XElement Existing(OperationInterlockDescriptor descriptor, string root)
    {
        var document = XElement.Load(Path.Combine(root, "TIAOpenness/TIA_Lib/TIA_LIB/Xml/EmptyBlock.xml"));
        var attributes = document.Element("SW.Blocks.FB").Element("AttributeList");
        attributes.Element("Name").Value = descriptor.BlockName;
        attributes.Element("ProgrammingLanguage").Value = "SCL";
        XNamespace ns = "http://www.siemens.com/automation/Openness/SW/Interface/v5";
        var statics = attributes.Descendants(ns + "Section").First(e => (string)e.Attribute("Name") == "Static");
        foreach (string member in descriptor.BoolMembers)
            statics.Add(new XElement(ns + "Member", new XAttribute("Name", member), new XAttribute("Datatype", "Bool"), new XAttribute("Accessibility", "Public")));
        // No source parser is involved: preserve the complete unknown body verbatim.
        attributes.Parent.Element("ObjectList").Add(new XElement("ManualBody", "// edited & retained\n#PRECOND_01 := #OperatorPermit;"));
        statics.Add(new XElement(ns + "Member", new XAttribute("Name", "ManualExtra"), new XAttribute("Datatype", "Int")));
        return document;
    }

    public static void Run(string root)
    {
        var descriptor = new OperationInterlockDescriptor("BV130_B10_20", "OP5_Pressure", new[] { "PH_Vent", "PH_Fill", "PH_Vent" });
        string source = descriptor.RenderScl();
        string expected = File.ReadAllText(Path.Combine(root, "Tests/Fixtures/OP5_Pressure_Interlocks.scl")).Replace("\r\n", "\n");
        Assert(source == expected, "Complete two-phase SCL snapshot differs.");
        Assert(descriptor.BoolMembers.Count == 150, "Two-phase inventory count.");
        Assert(source.Split('\n').Count(l => l.EndsWith(" : Bool;")) == 150, "Bool declaration count.");
        Assert(source.Split('\n').Count(l => l.EndsWith(" := FALSE;")) == 150, "Assignment count.");
        Assert(descriptor.Phases.SequenceEqual(new[] { "PH_Vent", "PH_Fill" }), "First occurrence order/deduplication.");
        Assert(source == descriptor.RenderScl(), "Rendering must be deterministic.");
        // Exercise the actual public AddPH cached route without any constructors or Portal.
        var project = (PlcProject)FormatterServices.GetUninitializedObject(typeof(PlcProject));
        var plant = (XmlPlant)FormatterServices.GetUninitializedObject(typeof(XmlPlant));
        var unit = (XmlUnit)FormatterServices.GetUninitializedObject(typeof(XmlUnit));
        var operation = (XmlOperation)FormatterServices.GetUninitializedObject(typeof(XmlOperation));
        var vent = (XmlPhase)FormatterServices.GetUninitializedObject(typeof(XmlPhase));
        var fill = (XmlPhase)FormatterServices.GetUninitializedObject(typeof(XmlPhase));
        project.Plant = plant;
        plant.Units = new Dictionary<string, XmlUnit> { { "fbBV130_B10_20", unit } };
        unit.Operations = new Dictionary<string, XmlOperation> { { "OP5_Pressure", operation } };
        operation.Phases = new Dictionary<string, XmlPhase> { { "PH_Vent", vent }, { "PH_Fill", fill } };
        Assert(ReferenceEquals(project.AddPH("BV130_B10_20", "OP5_Pressure", "PH_Vent"), vent), "Cached AddPH identity.");
        Assert(ReferenceEquals(project.AddPH("BV130_B10_20", "OP5_Pressure", "PH_Fill"), fill), "Cached later phase identity.");
        Assert(ReferenceEquals(project.AddPH("BV130_B10_20", "OP5_Pressure", "PH_Vent"), vent) && operation.Phases.Count == 2, "Repeated AddPH duplicated a phase.");
        var one = new OperationInterlockDescriptor("Unit2", "OP6_Other", new[] { "Vent" });
        string singleExpected = expected.Replace("OP5_Pressure", "OP6_Other");
        // Derive a second complete expected document by removing the literal PH_Fill sections,
        // not by duplicating the renderer's numeric/member generation loops.
        singleExpected = string.Join("\n", singleExpected.Split('\n').Where(l => !l.StartsWith("      PH_Fill_PRECOND_")));
        singleExpected = singleExpected.Substring(0, singleExpected.IndexOf("\n   REGION PH_Fill Interlocks", StringComparison.Ordinal)) + "END_FUNCTION_BLOCK\n";
        singleExpected = singleExpected.Replace("PH_Vent", "Vent");
        Assert(one.RenderScl() == singleExpected, "Complete single-phase source differs.");
        Assert(one.BoolMembers.Count == 100 && !one.RenderScl().Contains("PH_Vent"), "Actual complete phase names, no forced prefix.");
        Assert(new OperationInterlockDescriptor("Unit2", "OP7_Empty", new string[0]).RenderScl() == "", "AddOP only.");
        Assert(descriptor.ArtifactStem == new OperationInterlockDescriptor("BV130_B10_20", "OP5_Pressure", new[] { "PH_Vent" }).ArtifactStem, "Artifact independent of first phase inventory.");
        Assert(descriptor.ArtifactStem != new OperationInterlockDescriptor("OtherUnit", "OP5_Pressure", new[] { "PH_Vent" }).ArtifactStem, "Artifacts isolate units.");
        var hyphenated = new OperationInterlockDescriptor("A2", "OP2_Pressure", new[] { "PH_Re-Inert", "PH_H2-replace", "PH_Re-Inert" });
        string quotedExpected = System.Text.RegularExpressions.Regex.Replace(expected,
            @"(?m)^(      #?)(PH_(?:Vent|Fill)_PRECOND_[0-9]{2})( : Bool;| := FALSE;)$", "$1\"$2\"$3")
            .Replace("OP5_Pressure", "OP2_Pressure").Replace("PH_Vent", "PH_Re-Inert").Replace("PH_Fill", "PH_H2-replace");
        Assert(hyphenated.RenderScl() == quotedExpected, "Full hyphenated-phase source snapshot differs.");
        Assert(hyphenated.BoolMembers.Contains("PH_Re-Inert_PRECOND_01") && !hyphenated.BoolMembers.Any(n => n.Contains("\"")), "Quoting must not change model/interface names.");
        hyphenated.ValidateExisting(Existing(hyphenated, root), OperationInterlockLanguage.Scl);
        var nitrogen = new OperationInterlockDescriptor("A2", "OP2_Pressure", new[] { "PH_N2-replace" });
        Assert(nitrogen.RenderScl().Contains("#\"PH_N2-replace_PRECOND_02\" := FALSE;"), "Reported nitrogen phase quoting.");
        Assert(new OperationInterlockDescriptor("A2", "OP2-Reaction", new[] { "PH_Vent" }).RenderScl().StartsWith("FUNCTION_BLOCK \"OP2-Reaction_Interlocks\"\n"), "Hyphenated block identity.");
        foreach (string invalid in new[] { "PH_Bad\"", "PH_é", "PH_1\nEND_VAR", "PH_Test\n", "PH_Test\r", "PH_X$", "PH_X;", "-PH_X", "", new string('x', 128) })
            Reject(() => new OperationInterlockDescriptor("U", "OP", new[] { invalid }), "identifier");
        Reject(() => new OperationInterlockDescriptor("U", "Bad/Operation", new[] { "PH" }), "identifier");

        var config = new OperationInterlockConfiguration();
        Assert(config.Language == OperationInterlockLanguage.Fbd, "Default FBD.");
        config.Language = OperationInterlockLanguage.Scl;
        config.Freeze();
        config.Language = OperationInterlockLanguage.Scl;
        Reject(() => config.Language = OperationInterlockLanguage.Fbd, "before constructing");
        foreach (var pair in new[] { new[] { "\n", "Fbd" }, new[] { "1\n", "Fbd" }, new[] { " 2 \n", "Scl" }, new[] { "bad\n3\n2\n", "Scl" }, new[] { "", "Fbd" } })
        {
            var output = new StringWriter();
            Assert(OperationInterlockConfiguration.Select(new StringReader(pair[0]), output).ToString() == pair[1], "Selector input: " + pair[0]);
            Assert(output.ToString().Contains("operation interlock FBs only"), "Selector scope.");
        }

        foreach (SignalStagingMode mode in Enum.GetValues(typeof(SignalStagingMode)))
        {
            PlcProject.StagingMode = mode;
            Assert(descriptor.RenderScl() == expected, "Staging must not affect rendering.");
            var backend = new Backend();
            var batch = new OperationInterlockBatch(new[] { descriptor, one }, backend, OperationInterlockLanguage.Scl);
            batch.Upload(descriptor); batch.Upload(one); batch.Upload(descriptor); batch.Upload(one);
            Assert(backend.Writes == 2, "Double enumeration generated twice in " + mode);

            XElement manual = Existing(descriptor, root);
            string before = manual.ToString(SaveOptions.DisableFormatting);
            backend = new Backend(); backend.Add(descriptor, manual, true);
            batch = new OperationInterlockBatch(new[] { descriptor }, backend, OperationInterlockLanguage.Scl);
            batch.Upload(descriptor); batch.Upload(descriptor);
            Assert(backend.Writes == 0 && before == manual.ToString(SaveOptions.DisableFormatting), "Manual body/comments/extra members changed.");
        }

        foreach (string scenario in new[] { "missing", "type", "language", "folder", "stateVersion", "stateAccess", "parameter", "layout", "blockType" })
        {
            var backend = new Backend();
            XElement document = Existing(descriptor, root);
            var attributes = document.Element("SW.Blocks.FB").Element("AttributeList");
            var member = document.Descendants().First(e => (string)e.Attribute("Name") == "PH_Fill_PRECOND_02");
            if (scenario == "missing") member.Remove();
            if (scenario == "type") member.SetAttributeValue("Datatype", "Int");
            if (scenario == "language") attributes.Element("ProgrammingLanguage").Value = "FBD";
            if (scenario == "layout") attributes.Element("MemoryLayout").Value = "Standard";
            if (scenario == "blockType") document.Element("SW.Blocks.FB").Name = "SW.Blocks.FC";
            if (scenario == "stateVersion") document.Descendants().First(e => (string)e.Attribute("Name") == "State").SetAttributeValue("Version", "2.0");
            if (scenario == "stateAccess") document.Descendants().First(e => (string)e.Attribute("Name") == "ExternalVisible").Value = "false";
            if (scenario == "parameter") attributes.Descendants().First(e => (string)e.Attribute("Name") == "Input").Add(new XElement(member));
            backend.Add(descriptor, document, scenario != "folder");
            string before = document.ToString();
            // The first request is new; a later rejection must prevent ALL generation.
            Reject(() => new OperationInterlockBatch(new[] { one, descriptor }, backend, OperationInterlockLanguage.Scl), scenario == "folder" ? "conflicts" : "Reconcile");
            Assert(backend.Writes == 0 && before == document.ToString(), "Preflight wrote during " + scenario);
        }
        var collisions = new Backend();
        var duplicate = new OperationInterlockDescriptor("OtherUnit", "OP5_Pressure", new[] { "PH_Test" });
        Reject(() => new OperationInterlockBatch(new[] { descriptor, duplicate }, collisions, OperationInterlockLanguage.Scl), "BV130_B10_20/OP5_Pressure, OtherUnit/OP5_Pressure");
        Assert(collisions.Writes == 0, "Global-name collision generated something.");
        var caseDuplicate = new OperationInterlockDescriptor("OtherUnit", "op5_pressure", new[] { "PH_Test" });
        Reject(() => new OperationInterlockBatch(new[] { descriptor, caseDuplicate }, collisions, OperationInterlockLanguage.Scl), "collision");
        var duplicateExisting = new Backend();
        duplicateExisting.Add(descriptor, Existing(descriptor, root), true);
        duplicateExisting.Existing[descriptor.Identity].Add(new OperationInterlockExisting { Path = "CPU/Elsewhere/" + descriptor.BlockName, InTargetFolder = false });
        Reject(() => new OperationInterlockBatch(new[] { descriptor }, duplicateExisting, OperationInterlockLanguage.Scl), "conflicts");
        var failure = new Backend { Fail = true };
        var failedBatch = new OperationInterlockBatch(new[] { descriptor, one }, failure, OperationInterlockLanguage.Scl);
        Reject(() => failedBatch.Upload(descriptor), "compile error");
        Reject(() => failedBatch.Upload(one), "previously failed");
        Assert(failure.Writes == 1, "Continued generation after error.");
        var emptyBackend = new Backend();
        new OperationInterlockBatch(new[] { new OperationInterlockDescriptor("U", "OP", new string[0]) }, emptyBackend, OperationInterlockLanguage.Scl);
        Assert(emptyBackend.Writes == 0, "Zero-phase generation.");

        // Actual legacy call builder and serialization, with XmlBlock's Portal constructor bypassed.
        string oldDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(Path.Combine(root, "TIAOpenness/TIA_Lib/TIA_LIB"));
            var block = (XmlBlock)FormatterServices.GetUninitializedObject(typeof(XmlBlock));
            block.Xml = XElement.Load("Xml/EmptyOperation.xml");
            block.Networks = new Dictionary<string, XmlNetwork>();
            block.LocalInstance = new Dictionary<string, XmlLocalInstance>();
            typeof(XmlBlock).GetField("_ObjectList", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(block, block.Xml.Element("SW.Blocks.FB").Element("ObjectList"));
            var network = block.GetNetwork("Interlocks");
            var call = network.GetCall("OP5_Pressure_Interlocks", "FB", "LocalVariable", "Interlocks");
            network.SetParameter(call, "en", "Input", "Bool");
            Assert(call.BlockName == "OP5_Pressure_Interlocks" && call.InstName == "Interlocks" && call.InstType == "LocalVariable", "Legacy call identity.");
            Assert(block.LocalInstance["Interlocks"].Datatype == "OP5_Pressure_Interlocks", "Legacy multi-instance type.");
            Assert(network.Xml.ToString().Contains("NameCon") && call.Parameters.ContainsKey("en"), "Legacy EN handling.");
            // Model a Siemens round-trip: source nodes are qualified on export. The reader
            // must use the document's namespace rather than mistake existing calls for absent ones.
            foreach (string version in new[] { "v4", "v5" })
            {
                XNamespace sourceNamespace = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/" + version;
                var exported = new XElement(network.Xml);
                var flgNet = exported.Descendants().Single(e => e.Name.LocalName == "FlgNet");
                foreach (var element in flgNet.DescendantsAndSelf())
                {
                    element.Attributes().Where(a => a.IsNamespaceDeclaration).Remove();
                    element.Name = sourceNamespace + element.Name.LocalName;
                }
                block.Networks.Clear();
                block.HasChanged = false;
                string original = exported.ToString();
                var reopened = new XmlNetwork(block, exported);
                var reusedCall = reopened.GetCall("OP5_Pressure_Interlocks", "FB", "LocalVariable", "Interlocks");
                Assert(reopened.Calls.Count == 1 && reusedCall.Xml.Parent == reopened.Parts, "Reopen lost/duplicated existing call in " + version);
                Assert(!block.HasChanged && original == exported.ToString(), "Reopening changed existing network in " + version);
                var newCall = reopened.GetCall("OP5_NewPhase", "FB", "LocalVariable", "NewPhase");
                Assert(reopened.Calls.Count == 2, "Could not extend existing network in " + version);
                if (version == "v5")
                {
                    reopened.SetParameter(newCall, "Permit", "Input", "Bool", "False");
                    reopened.SetParameter(newCall, "Condition", "Input", "Bool", "Interlocks|PRECOND_01");
                    Assert(reopened.Parts.Elements().All(e => e.Name.Namespace == sourceNamespace), "New nodes lost source namespace.");
                    Assert(flgNet.DescendantsAndSelf().All(e => e.Name.Namespace == sourceNamespace), "New parameters/accesses/wires lost source namespace.");
                    block.Networks.Clear();
                    block.HasChanged = false;
                    string extended = exported.ToString();
                    var reloaded = new XmlNetwork(block, exported);
                    Assert(reloaded.Calls.Count == 2 && reloaded.Access.Count == 2 && reloaded.Wires.Count == 3, "Extended network round-trip inventory.");
                    Assert(!block.HasChanged && extended == exported.ToString(), "Extended network round-trip changed manual XML.");
                    var local = reloaded.Access.Values.OfType<TIA_LIB.Xml.Network.XmlGlobal>().Single();
                    Assert(local.Value == "Interlocks|PRECOND_01", "Existing local reference was not read.");
                    local.Set("Interlocks|PRECOND_02");
                    Assert(local.Xml.Descendants(sourceNamespace + "Component").Last().Attribute("Name").Value == "PRECOND_02", "Reference update lost namespace.");
                    var literal = reloaded.Access.Values.OfType<TIA_LIB.Xml.Network.XmlConstant>().Single();
                    literal.Set("True");
                    Assert(literal.Xml.Descendants(sourceNamespace + "ConstantValue").Single().Value == "True", "Constant update lost namespace.");
                }
            }
            var noParts = new XElement(network.Xml);
            noParts.Descendants().Single(e => e.Name.LocalName == "Parts").Remove();
            block.Networks.Clear();
            var unsupported = new XmlNetwork(block, noParts);
            string untouched = noParts.ToString();
            Reject(() => unsupported.GetCall("fbUnit", "FB", "LocalVariable", "Unit"), "no editable FBD Parts/Wires");
            Assert(untouched == noParts.ToString() && unsupported.Calls.Count == 0, "Unsupported network was modified.");
            var fbd = Existing(descriptor, root);
            fbd.Element("SW.Blocks.FB").Element("AttributeList").Element("ProgrammingLanguage").Value = "FBD";
            string before = fbd.ToString();
            descriptor.ValidateExisting(fbd, OperationInterlockLanguage.Fbd);
            Assert(before == fbd.ToString(), "FBD preflight changed serialization.");
        }
        finally { Directory.SetCurrentDirectory(oldDirectory); }
        Console.WriteLine("PASS: complete SCL snapshots, inventory/order, selection/freeze, identifiers, both staging modes, preservation/preflight/collisions, failure/double upload, and legacy FBD call contract.");
    }
}
