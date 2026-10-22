using System;
using System.Linq;
using System.Xml.Linq;
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
        Console.WriteLine("PASS: G-017 planning foundation: explicit control/config ownership, metadata copies, category scheduling hazards and dependency graph. Runtime adapters/renderers are not exercised.");
    }
}
