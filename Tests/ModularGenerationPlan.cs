using System;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Schema;
using System.IO;
using TIA_LIB.Generation;

public static class ModularGenerationPlanTests
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Reject(Action action, string diagnostic)
    {
        try { action(); throw new Exception("Expected rejection: " + diagnostic); }
        catch (InvalidOperationException ex) { Check(ex.Message.Contains(diagnostic), "Wrong diagnostic: " + ex.Message); }
    }
    static ConfigurationLeaf Leaf(string unit, string instance, string datatype)
    {
        return new ConfigurationLeaf(unit, instance, XElement.Parse("<Member Name='legacy' Datatype='" + datatype +
            "' Version='1.0' Remanence='Retain'><Comment><MultiLanguageText Lang='de-DE'>Keep &amp; preserve</MultiLanguageText></Comment>" +
            "<Sections><Section Name='None'><Member Name='Limit' Datatype='Real'><StartValue>42.5</StartValue></Member>" +
            "<Member Name='Manual' Datatype='Bool'/></Section></Sections></Member>"));
    }
    static ScheduledNetwork Network(string id, DeviceCategory category, string[] reads, string[] writes)
    {
        return new ScheduledNetwork(id, "Original " + id + " - Füllstand", category, reads, writes);
    }
    public static void Run()
    {
        foreach (var answer in new[] { "", "1", "invalid\n2", "2" })
        {
            var output = new StringWriter();
            var selected = GenerationConfiguration.Select(new StringReader(answer), output);
            Check(selected == (answer.EndsWith("2") ? GenerationLanguage.Scl : GenerationLanguage.Fbd) && output.ToString().StartsWith("Select language for code generation:"), "General selector contract differs.");
            Check(!output.ToString().Contains("(default)") && !output.ToString().Contains("Phase templates remain"), "Console includes removed annotations.");
        }
        var selection = new GenerationConfiguration();
        Check(selection.Language == GenerationLanguage.Fbd, "FBD is not the default.");
        selection.Language = GenerationLanguage.Scl;
        Reject(() => selection.CompatibilityInterlockLanguage = TIA_LIB.OperationInterlockLanguage.Fbd, "conflicts");
        selection.CompatibilityInterlockLanguage = TIA_LIB.OperationInterlockLanguage.Scl;
        selection.Freeze(); selection.Language = GenerationLanguage.Scl;
        Reject(() => selection.Language = GenerationLanguage.Fbd, "conflicts");
        var legacyFirst = new GenerationConfiguration { CompatibilityInterlockLanguage = TIA_LIB.OperationInterlockLanguage.Scl };
        Reject(() => legacyFirst.Language = GenerationLanguage.Fbd, "conflicts");
        legacyFirst.Freeze();
        Reject(() => legacyFirst.CompatibilityInterlockLanguage = TIA_LIB.OperationInterlockLanguage.Fbd, "before constructing");
        var approvalPlan = XElement.Parse("<ConfigurationMigration Context='project|CPU'><OriginalCfgDataHash>snapshot</OriginalCfgDataHash><Unit Name='A_2'><Path Old='cfgData|A_2_PID-1' New='cfgData|A_2|PID-1' Type='typePIDCfg'/></Unit></ConfigurationMigration>");
        var approval = ConfigurationMigrationApproval.CreateTemplate(approvalPlan);
        Reject(() => ConfigurationMigrationApproval.Validate(approvalPlan, approval), "unknown consumers");
        approval.SetAttributeValue("UnknownConsumers", "false");
        foreach (string acknowledgement in new[] { "ExternalConsumersReviewed", "OfflineBackupVerified", "OnlineActualValuesAddressed", "LegacyMembersRetained", "DivergenceRiskAccepted" }) approval.Element(acknowledgement).Value = "true";
        approval.Element("ExternalConsumerEvidence").Value = "Reviewed PLC/HMI/script references for the exact mapping; no unknown consumers remain.";
        approval.Element("OnlineValueEvidence").Value = "Offline-only test project; actual online values are outside this execution and no download is made.";
        ConfigurationMigrationApproval.Validate(approvalPlan, approval);
        var stalePlan = new XElement(approvalPlan); stalePlan.Element("OriginalCfgDataHash").Value = "changed commissioning values";
        Reject(() => ConfigurationMigrationApproval.Validate(stalePlan, approval), "exact project/CPU/plan");
        var missingSymbol = new XElement(approval); missingSymbol.Element("Symbols").RemoveNodes();
        Reject(() => ConfigurationMigrationApproval.Validate(approvalPlan, missingSymbol), "exact symbol/type");
        var controls = ModularControlContract.PlantToUnit();
        string[] expected = { "iReset=HMI.RESET", "iReset_Delayed=HMI.RESET_DELAYED", "iMode_Off=HMI.MODE_OFF", "iMode_Service=HMI.MODE_SERV", "iHSS=F_Safe.HSS" };
        Check(controls.Select(c => c.Input + "=" + string.Join(".", c.Source.Components)).SequenceEqual(expected), "Five-control golden contract differs.");
        Check(controls.All(c => c.Datatype == "Bool" && c.Source.IsGlobal), "Plant control type/scope differs.");
        Check(ModularControlContract.UnitToOperation().Select(c => c.Input + "=" + string.Join(".", c.Source.Components))
            .SequenceEqual(new[] { "HSS=iHSS", "Delayed_Reset=iReset_Delayed" }), "Operation forwarding differs.");
        Check(ModularControlContract.UnitToOperation().All(c => !c.Source.IsGlobal), "Operation source is global.");
        var input = new[] { "cfgData", "A2", "PID-1" };
        var symbol = new SymbolReference(true, input);
        input[1] = "Mutated";
        Check(symbol.Components[1] == "A2", "Symbol reference was mutable.");

        var leaves = new[] {
            Leaf("A_2", "AI-X", "typeAnalogCfg"), Leaf("A_2", "AI-X_Instance1", "typeAnalogCfg"),
            Leaf("A_2", "AI-X_Instance2", "typeAnalogCfg"), Leaf("A_2", "DI_X", "typeDigitalCfg"),
            Leaf("A_2", "DI_X_Instance1", "typeDigitalCfg"), Leaf("A_2", "DI_X_Instance2", "typeDigitalCfg"),
            Leaf("A_2", "V-1", "typeValveCfg"), Leaf("A_2", "CV-1", "typeValveSpCfg"),
            Leaf("A_2", "M-1", "typeMotorCfg"), Leaf("A_2", "MC-1", "typeMotorSpCfg"), Leaf("A_2", "PID-1", "typePIDCfg")
        };
        var config = new UnitConfigurationPlan("A_2", leaves.Concat(new[] { leaves[0] }));
        Check(config.Leaves.Count == 11 && config.UdtName == "udtCfg_A_2", "Configuration inventory/count/order differs.");
        Check(config.Leaves.Select(l => l.Instance).SequenceEqual(leaves.Select(l => l.Instance)), "Config declaration order changed.");
        var digital = config.Leaves.Single(l => l.Instance == "DI_X");
        Check(digital.OldPath.Components.SequenceEqual(new[] { "cfgData", "A_2_DI_X" }) &&
            digital.NewPath.Components.SequenceEqual(new[] { "cfgData", "A_2", "DI_X" }), "Exact underscore ownership mapping was lost.");
        Check(digital.LocalPath.Components.SequenceEqual(new[] { "CFG", "DI_X" }) && !digital.LocalPath.IsGlobal, "Local CFG path differs.");
        var snapshot = digital.Member;
        Check((string)snapshot.Attribute("Version") == "1.0" && (string)snapshot.Attribute("Remanence") == "Retain" &&
            snapshot.Descendants("StartValue").Single().Value == "42.5" && snapshot.Descendants("MultiLanguageText").Single().Value == "Keep & preserve" &&
            snapshot.Descendants("Member").Any(m => (string)m.Attribute("Name") == "Manual"), "Configuration metadata/start value/manual field lost.");
        snapshot.RemoveAll();
        Check(digital.Member.Attribute("Datatype") != null, "Configuration metadata was not copied immutably.");
        var serialized = new ConfigurationArtifacts(config);
        var schema = new XmlSchemaSet();
        const string interfaceNs = "http://www.siemens.com/automation/Openness/SW/Interface/v5";
        schema.Add(interfaceNs, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Siemens/Automation/Portal V21/PublicAPI/V21/Schemas/SW.InterfaceSections_v5.xsd"));
        schema.Compile();
        new XDocument(new XElement(serialized.Udt.Descendants(XName.Get("Sections", interfaceNs)).First())).Validate(schema, (sender, error) => { throw error.Exception; });
        new XDocument(new XElement(XName.Get("Sections", interfaceNs), new XElement(XName.Get("Section", interfaceNs),
            new XAttribute("Name", "Static"), serialized.DatablockMember))).Validate(schema, (sender, error) => { throw error.Exception; });
        var emptyArtifacts = new ConfigurationArtifacts(new UnitConfigurationPlan("Empty", new ConfigurationLeaf[0]));
        Check(emptyArtifacts.Udt == null && emptyArtifacts.DatablockMember == null && emptyArtifacts.RequiredTypes.Count == 0, "Empty unit acquired a CFG artifact.");
        XNamespace cfgNs = interfaceNs;
        var ownedUdt = serialized.Udt;
        var ownedMember = serialized.DatablockMember;
        ownedUdt.Descendants(cfgNs + "Section").First().Add(new XElement(cfgNs + "Member", new XAttribute("Name", "ManualExtra"), new XAttribute("Datatype", "Real"), new XElement(cfgNs + "StartValue", "19.5")));
        ownedMember.Descendants(cfgNs + "Section").First().Add(new XElement(cfgNs + "Member", new XAttribute("Name", "ManualExtra"), new XAttribute("Datatype", "Real"), new XElement(cfgNs + "StartValue", "29.5")));
        ownedMember.Descendants(cfgNs + "StartValue").First().Value = "17.25";
        string ownedSnapshot = ownedUdt.ToString() + ownedMember.ToString();
        var additivePlan = new UnitConfigurationPlan("A_2", leaves.Concat(new[] { Leaf("A_2", "New-PID", "typePIDCfg") }));
        var additive = new ConfigurationArtifacts(additivePlan, ownedUdt, ownedMember);
        Check(additive.Udt.Descendants(cfgNs + "Section").First().Elements().Last().Attribute("Name").Value == "New-PID", "New CFG field not appended after retained/manual declarations.");
        Check(additive.DatablockMember.Descendants(cfgNs + "StartValue").First().Value == "17.25" &&
            additive.DatablockMember.Descendants(cfgNs + "Member").Single(m => (string)m.Attribute("Name") == "ManualExtra").Element(cfgNs + "StartValue").Value == "29.5", "Existing configuration overrides/manual fields were replaced by defaults.");
        var existingSection = new XElement(cfgNs + "Section", new XAttribute("Name", "Static"), new XElement(ownedMember), new XElement(cfgNs + "Member", new XAttribute("Name", "UnrelatedUnit"), new XAttribute("Datatype", "Bool")));
        var additiveProposal = additive.ProposeDatablockSection(existingSection);
        Check(additiveProposal.Elements().Count() == 2 && XNode.DeepEquals(additiveProposal, additive.ProposeDatablockSection(additiveProposal)), "Additive CFG proposal duplicates members or is not idempotent.");
        Check(ownedSnapshot == ownedUdt.ToString() + ownedMember.ToString(), "Owned CFG reconciliation modified input snapshots.");
        new XDocument(new XElement(additive.Udt.Descendants(cfgNs + "Sections").First())).Validate(schema, (sender, error) => { throw error.Exception; });
        new XDocument(new XElement(cfgNs + "Sections", additiveProposal)).Validate(schema, (sender, error) => { throw error.Exception; });
        var wrongType = new XElement(ownedUdt);
        wrongType.Descendants(cfgNs + "Member").First().SetAttributeValue("Datatype", "typePIDCfg");
        Reject(() => new ConfigurationArtifacts(additivePlan, wrongType, ownedMember), "spelling/type");
        var changedSection = new XElement(existingSection);
        changedSection.Descendants(cfgNs + "StartValue").First().Value = "999";
        Reject(() => additive.ProposeDatablockSection(changedSection), "changed");
        Check(!new UnitConfigurationPlan("Empty", new ConfigurationLeaf[0]).HasConfiguration, "Empty unit acquired configuration.");
        Check(new UnitConfigurationPlan("PID-only", new[] { Leaf("PID-only", "PIC-1", "typePIDCfg") }).HasConfiguration, "PID-only unit lost configuration.");
        Reject(() => new UnitConfigurationPlan("A_2", leaves.Concat(new[] { Leaf("A_2", "DI_X", "typePIDCfg") })), "collision");
        Reject(() => new UnitConfigurationPlan("WrongUnit", leaves), "ownership");
        Reject(() => UnitConfigurationPlan.ValidateUnitNames(new[] {
            new UnitConfigurationPlan("A-2", new ConfigurationLeaf[0]), new UnitConfigurationPlan("A_2", new ConfigurationLeaf[0]) }), "collision");

        var independent = new[] {
            Network("analog-first", DeviceCategory.Analog_Sensors, new string[0], new[] { "Analog.Q" }),
            Network("digital-second", DeviceCategory.Digital_Sensors, new string[0], new[] { "Digital.Q" }),
            Network("analog-third", DeviceCategory.Analog_Sensors, new string[0], new[] { "Other.Q" })
        };
        var grouped = DeviceSchedule.Group(independent);
        Check(grouped.Select(n => n.Identity).SequenceEqual(new[] { "digital-second", "analog-first", "analog-third" }), "Category order or declaration order changed.");
        Check(object.ReferenceEquals(grouped[1], independent[0]) && grouped[1].Title == "Original analog-first - Füllstand", "Original title/identity changed.");
        foreach (bool previousScan in new[] { false, true })
        {
            var a = Network("A", DeviceCategory.Analog_Sensors, previousScan ? new[] { "STATE" } : new string[0], previousScan ? new string[0] : new[] { "STATE" });
            var b = Network("D", DeviceCategory.Digital_Sensors, previousScan ? new string[0] : new[] { "state" }, previousScan ? new[] { "state" } : new string[0]);
            Reject(() => DeviceSchedule.Group(new[] { a, b }), "scan dependencies");
        }
        Reject(() => DeviceSchedule.Group(new[] {
            Network("A", DeviceCategory.Analog_Sensors, new string[0], new[] { "Output" }),
            Network("D", DeviceCategory.Digital_Sensors, new string[0], new[] { "Output" }) }), "scan dependencies");
        Reject(() => DeviceSchedule.Group(new[] { independent[0], independent[0] }), "identity");
        Reject(() => DeviceSchedule.Group(new[] {
            Network("A", DeviceCategory.Analog_Sensors, new string[0], new[] { "#\"Instance\"" }),
            Network("D", DeviceCategory.Digital_Sensors, new[] { "#\"Instance\".\"Q\"" }, new string[0]) }), "scan dependencies");
        try { new ScheduledNetwork("bad", "Title\nEND_REGION", DeviceCategory.Motors_Direct, new string[0], new string[0]); throw new Exception("Invalid region accepted."); }
        catch (ArgumentException) { }

        var artifacts = new[] {
            new ArtifactDependency("Plant", "Unit", "UnitDB"), new ArtifactDependency("UnitDB", "Unit"),
            new ArtifactDependency("Unit", "Operation", "cfgData"), new ArtifactDependency("Operation", "Phase", "Interlocks"),
            new ArtifactDependency("cfgData", "udtCfg_Unit"), new ArtifactDependency("Phase"),
            new ArtifactDependency("Interlocks"), new ArtifactDependency("udtCfg_Unit")
        };
        var ordered = ArtifactSchedule.Order(artifacts).Select(a => a.Identity).ToArray();
        Check(ordered.SequenceEqual(new[] { "Phase", "Interlocks", "Operation", "udtCfg_Unit", "cfgData", "Unit", "UnitDB", "Plant" }), "Dependency schedule differs from golden fixture.");
        Reject(() => ArtifactSchedule.Order(new[] { new ArtifactDependency("Unit", "Missing") }), "Missing dependency");
        Reject(() => ArtifactSchedule.Order(new[] { new ArtifactDependency("A", "B"), new ArtifactDependency("B", "A") }), "cycle");
        Reject(() => ArtifactSchedule.Order(new[] { new ArtifactDependency("A"), new ArtifactDependency("a") }), "colliding");
        var importTrace = new System.Collections.Generic.List<string>();
        ArtifactBatchExecution.Execute(artifacts, () => importTrace.Add("preflight"), id => importTrace.Add(id));
        Check(importTrace.SequenceEqual(new[] { "preflight" }.Concat(ordered)), "Batch imports precede preflight or reverse dependencies.");
        importTrace.Clear();
        Reject(() => ArtifactBatchExecution.Execute(artifacts, () => { throw new InvalidOperationException("preflight failed"); }, id => importTrace.Add(id)), "preflight failed");
        Check(importTrace.Count == 0, "Import ran after failed batch preflight.");
        Reject(() => ArtifactBatchExecution.Execute(artifacts, () => importTrace.Add("preflight"), id => {
            importTrace.Add(id); if (id == "Operation") throw new InvalidOperationException("import failed"); }), "import failed");
        Check(importTrace.SequenceEqual(new[] { "preflight", "Phase", "Interlocks", "Operation" }), "Dependent/unrelated callers imported after failure.");
        int passes = 0;
        ArtifactBatchExecution.CompileUntilConsistent(() => passes++, () => passes == 1, "Stable");
        Check(passes == 1, "Consistent block was needlessly compiled twice.");
        passes = 0;
        ArtifactBatchExecution.CompileUntilConsistent(() => passes++, () => passes == 2, "UpdatedCalls");
        Check(passes == 2, "Updated FBD calls did not get their consistency compile pass.");
        passes = 0;
        Reject(() => ArtifactBatchExecution.CompileUntilConsistent(() => passes++, () => false, "StillInconsistent"), "remains inconsistent");
        Check(passes == 2, "Inconsistent block was retried without bound.");
        passes = 0;
        Reject(() => ArtifactBatchExecution.CompileUntilConsistent(() => { passes++; throw new InvalidOperationException("real compile error"); }, () => true, "Invalid"), "real compile error");
        Check(passes == 1, "Actual compiler failure was retried/hidden.");
        int dependencyPasses = 0;
        passes = 0;
        ArtifactBatchExecution.CompileUntilConsistent(() => passes++, () => dependencyPasses == 1, "NeedsSoftwareCompile", () => dependencyPasses++);
        Check(passes == 2 && dependencyPasses == 1, "Persistent zero-error block inconsistency did not receive exactly one dependency compile.");
        passes = dependencyPasses = 0;
        ArtifactBatchExecution.CompileUntilConsistent(() => passes++, () => true, "AlreadyConsistent", () => dependencyPasses++);
        Check(passes == 1 && dependencyPasses == 0, "Consistent block needlessly compiled the PLC software.");
        passes = dependencyPasses = 0;
        Reject(() => ArtifactBatchExecution.CompileUntilConsistent(() => { passes++; throw new InvalidOperationException("block compile failed"); },
            () => false, "BadBlock", () => dependencyPasses++), "block compile failed");
        Check(passes == 1 && dependencyPasses == 0, "A real block compile failure incorrectly triggered dependency compilation.");
        passes = dependencyPasses = 0;
        Reject(() => ArtifactBatchExecution.CompileUntilConsistent(() => passes++, () => false, "BadDependencies",
            () => { dependencyPasses++; throw new InvalidOperationException("software compile failed"); }), "software compile failed");
        Check(passes == 2 && dependencyPasses == 1, "A failed dependency compile was hidden or retried.");
        passes = dependencyPasses = 0;
        Reject(() => ArtifactBatchExecution.CompileUntilConsistent(() => passes++, () => false, "StillNotExportable", () => dependencyPasses++), "remains inconsistent");
        Check(passes == 2 && dependencyPasses == 1, "Persistent inconsistency bypassed the guard or retried without bound.");
        var lifecycle = new System.Collections.Generic.List<string>();
        bool instancePrepared = false, published = false;
        var publicationGraph = new[] { new ArtifactDependency("Plant", "Unit", "UnitDB"), new ArtifactDependency("UnitDB", "Unit"), new ArtifactDependency("Unit") };
        ArtifactBatchExecution.Execute(publicationGraph, () => { }, id => {
            if (id == "Unit") ArtifactBatchExecution.PublishWithInstances(
                () => lifecycle.Add("check FB definition"),
                new Action[] { () => { instancePrepared = true; lifecycle.Add("prepare instance DB"); } },
                () => { Check(instancePrepared, "FB consistency/export checked before its instance DB exists."); published = true; lifecycle.Add("export consistent FB"); });
            else if (id == "Plant") { Check(instancePrepared && published, "Plant imported before Unit/DB completion."); lifecycle.Add("import Plant call"); }
        });
        Check(lifecycle.SequenceEqual(new[] { "check FB definition", "prepare instance DB", "export consistent FB", "import Plant call" }), "FB/instance/ownership/Plant lifecycle differs.");
        lifecycle.Clear();
        Reject(() => ArtifactBatchExecution.Execute(publicationGraph, () => { }, id => {
            if (id == "Unit") ArtifactBatchExecution.PublishWithInstances(() => lifecycle.Add("definition checked"),
                new Action[] { () => { lifecycle.Add("instance failed"); throw new InvalidOperationException("instance failure"); } }, () => lifecycle.Add("unsafe export"));
            else lifecycle.Add(id);
        }), "instance failure");
        Check(lifecycle.SequenceEqual(new[] { "definition checked", "instance failed" }), "Failed companion allowed export or Plant import.");
        lifecycle.Clear();
        Reject(() => ArtifactBatchExecution.PublishWithInstances(() => { throw new InvalidOperationException("definition failed"); },
            new Action[] { () => lifecycle.Add("unsafe instance creation") }, () => lifecycle.Add("unsafe export")), "definition failed");
        Check(lifecycle.Count == 0, "Invalid definition created an instance or exported ownership.");
        Console.WriteLine("PASS: planning, dependency/failure gates, bounded consistency checks and FB/companion DB publication before Plant. No Portal execution.");
    }
}
