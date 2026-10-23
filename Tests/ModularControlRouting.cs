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
    static XElement Render(XmlBlock block)
    {
        var runtimeType = typeof(PlcProject).Assembly.GetType("TIA_LIB.Generation.ModularGenerationRuntime");
        var runtime = FormatterServices.GetUninitializedObject(runtimeType);
        runtimeType.GetField("shadows", Private).SetValue(runtime, new Dictionary<XmlBlock, XElement>());
        try { return (XElement)runtimeType.GetMethod("Render", Private).Invoke(runtime, new object[] { block }); }
        catch (TargetInvocationException error) { throw error.InnerException; }
    }
    static void RuntimeDependencies()
    {
        var type = typeof(PlcProject).Assembly.GetType("TIA_LIB.Generation.ModularGenerationRuntime");
        var runtime = FormatterServices.GetUninitializedObject(type);
        var field = type.GetField("artifacts", Private);
        var artifacts = (System.Collections.IList)Activator.CreateInstance(field.FieldType);
        field.SetValue(runtime, artifacts);
        var add = type.GetMethod("Add", Private);
        Action<string, string, XElement> artifact = (kind, name, document) => add.Invoke(runtime, new object[] { kind, name, document, null, null });
        // Deliberately caller-first. Cover the actual runtime scanner's interface,
        // CallInfo, global Instance and global Access edges, not a hand-built DAG.
        artifact("block", "Plant", XElement.Parse("<Document><Instance Scope='GlobalVariable'><Component Name='UnitDB'/></Instance></Document>"));
        artifact("block", "Unit", XElement.Parse("<Document><Member Datatype='&quot;udtCfg_A2&quot;'/><CallInfo Name='Operation'/><Access Scope='GlobalVariable'><Symbol><Component Name='cfgData'/><Component Name='A2'/></Symbol></Access></Document>"));
        artifact("instance", "UnitDB", null);
        var instance = artifacts[2]; instance.GetType().GetField("InstanceOf").SetValue(instance, "Unit");
        artifact("block", "Operation", XElement.Parse("<Document><Member Datatype='&quot;Phase&quot;'/></Document>"));
        artifact("block", "Phase", new XElement("Document"));
        artifact("block", "cfgData", XElement.Parse("<Document><Member Datatype='&quot;udtCfg_A2&quot;'/></Document>"));
        artifact("type", "udtCfg_A2", new XElement("Document"));
        type.GetMethod("Dependencies", Private).Invoke(runtime, null);
        var graph = artifacts.Cast<object>().Select(a => new ArtifactDependency(
            (string)a.GetType().GetField("Id").GetValue(a), ((HashSet<string>)a.GetType().GetField("Requires").GetValue(a)).ToArray())).ToArray();
        Func<string, string[]> requires = id => graph.Single(a => a.Identity == id).Requires.ToArray();
        Check(requires("block/Unit").OrderBy(x => x).SequenceEqual(new[] { "block/Operation", "block/cfgData", "type/udtCfg_A2" }.OrderBy(x => x)), "Runtime lost Unit dependencies.");
        Check(requires("block/Plant").SequenceEqual(new[] { "instance/UnitDB" }) && requires("instance/UnitDB").SequenceEqual(new[] { "block/Unit" }), "Runtime lost global instance dependency chain.");
        var order = ArtifactSchedule.Order(graph).Select(a => a.Identity).ToList();
        Check(order.IndexOf("type/udtCfg_A2") < order.IndexOf("block/cfgData") && order.IndexOf("block/Phase") < order.IndexOf("block/Operation") &&
            order.IndexOf("block/cfgData") < order.IndexOf("block/Unit") && order.IndexOf("block/Operation") < order.IndexOf("block/Unit") &&
            order.IndexOf("block/Unit") < order.IndexOf("instance/UnitDB") && order.IndexOf("instance/UnitDB") < order.IndexOf("block/Plant"), "Runtime dependency order differs from required compile/import order.");
    }
    static void RuntimePaths()
    {
        const string working = @"C:\Users\User\source\repos\me-etu\etu-process-generator-v20\etu-process-generator-TiAv21\bin\Debug";
        string owner = Path.Combine(working, "GeneratedModels", new string('A', 64));
        var type = typeof(PlcProject).Assembly.GetType("TIA_LIB.Generation.ModularGenerationRuntime");
        var build = type.GetMethod("BuildRunDirectory", BindingFlags.Static | BindingFlags.NonPublic);
        var runtime = FormatterServices.GetUninitializedObject(type);
        type.GetField("ledgerDirectory", Private).SetValue(runtime, owner);
        string ledger = (string)type.GetMethod("Ledger", Private).Invoke(runtime, new object[] { "fbA2" });
        Check(Path.GetDirectoryName(ledger) == owner && Path.GetFileNameWithoutExtension(ledger).Length == 64, "Existing ownership path format changed.");
        string run = (string)build.Invoke(null, new object[] { owner, new string('B', 32) });
        Check(run == Path.Combine(working, "GeneratedModels", "Runs", new string('B', 32)), "Run folder still nests the ownership hash.");
        foreach (string name in new[] { "TEMP-tags-backup.xml", "configuration-approval.template.xml", new string('C', 64) + "-generated.xml", new string('D', 64) + "-published.xml" })
            Check(Path.Combine(run, name).Length < 260, "Normal output still exceeds the Openness path limit.");
        string other = (string)build.Invoke(null, new object[] { owner, new string('E', 32) });
        Check(run != other, "Run paths collide across executions.");
        try
        {
            build.Invoke(null, new object[] { Path.Combine(working, new string('F', 80), "GeneratedModels", new string('A', 64)), new string('B', 32) });
            throw new Exception("Overlong export path was accepted.");
        }
        catch (TargetInvocationException error)
        { Check(error.InnerException is InvalidOperationException && error.InnerException.Message.Contains("shorter generator working directory"), "Overlong path lacks actionable context."); }
    }
    static void LibraryPinTypes()
    {
        var type = typeof(PlcProject).Assembly.GetType("TIA_LIB.Generation.ModularGenerationRuntime");
        var match = type.GetMethod("LibraryPinTypeMatches", BindingFlags.Static | BindingFlags.NonPublic);
        Func<string, string, string, bool> compatible = (actual, generated, direction) => (bool)match.Invoke(null, new object[] { actual, generated, direction });
        Check(compatible("String[10]", "String", "Input"), "Real fbMonAn.UNIT String[10] rejected generic String call marker.");
        Check(compatible("string[254]", "STRING", "Input") && compatible("UInt", "Uint", "Input") && compatible("\"typeAnalogCfg\"", "typeAnalogCfg", "Input"), "Valid existing pin spelling/representation rejected.");
        foreach (string invalid in new[] { "WString[10]", "Bool", "String[0]", "String[255]", "String[]", "String[10]extra", "Array[1..10] of Char" })
            Check(!compatible(invalid, "String", "Input"), "Invalid string contract accepted: " + invalid);
        Check(!compatible("String[10]", "String", "InOut") && !compatible("String[10]", "String", "Output") &&
            !compatible("String[10]", "String[20]", "Input") && !compatible(null, "String", "Input"), "Explicit capacity/direction/missing-pin mismatch accepted.");
    }
    static void EmptyAndPidOnly()
    {
        foreach (SignalStagingMode mode in Enum.GetValues(typeof(SignalStagingMode)))
        foreach (OperationInterlockLanguage language in Enum.GetValues(typeof(OperationInterlockLanguage)))
        foreach (bool pidOnly in new[] { false, true })
        {
            PlcProject.StagingMode = mode; PlcProject.InterlockLanguage = language;
            var project = (PlcProject)typeof(DeviceStagingInputTests).GetMethod("Project", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            var unit = project.Plant.GetUnit("A2"); unit.Operations = new Dictionary<string, XmlOperation>();
            if (pidOnly) project.AddPidControl("A2", "PID-only");
            var controller = Selector(unit, "Unit sfc control", "sfc_A2");
            controller.Network.SetParameter(controller, "Reset", "Input", "Bool", "HMI|RESET", true);
            Prepare(project.Plant);
            var plan = ConfigurationMigrationPlan.Inspect(unit, null).Configuration;
            var artifacts = new ConfigurationArtifacts(plan);
            typeof(ConfigurationArtifacts).GetMethod("Bind", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { project.Plant, unit, plan });
            Check(plan.HasConfiguration == pidOnly && (artifacts.Udt != null) == pidOnly, "Empty/PID configuration contract differs.");
            Check(!project.StagingInventory.Units.Any(), "Empty/PID unit has hardware staging inventory.");
            var sections = unit.Xml.Descendants().First(e => e.Name.LocalName == "Sections");
            var inputs = sections.Elements().Single(s => (string)s.Attribute("Name") == "Input");
            foreach (string name in new[] { "iReset", "iReset_Delayed", "iMode_Off", "iMode_Service", "iHSS" })
                Check(inputs.Elements().Any(m => (string)m.Attribute("Name") == name && (string)m.Attribute("Datatype") == "Bool"), "Empty/PID unit is missing controls.");
            Check(inputs.Elements().Any(m => (string)m.Attribute("Name") == "CFG") == pidOnly &&
                !inputs.Elements().Any(m => new[] { "hwIN", "hwOUT" }.Contains((string)m.Attribute("Name"))), "Empty/PID CFG/hardware interfaces differ.");
            Check(Render(unit).Descendants().Count(e => e.Name.LocalName == "CallInfo") == unit.Networks.Values.Sum(n => n.Calls.Count), "Empty/PID renderer lost calls.");
        }
    }
    static XmlBlock HelperBlock(string name)
    {
        var block = Bare<XmlBlock>(); block.Xml = XElement.Load("Xml/EmptyBlock.xml");
        block.Networks = new Dictionary<string, XmlNetwork>(); block.LocalInstance = new Dictionary<string, XmlLocalInstance>(); block.Instances = new List<string>();
        typeof(XmlBlock).GetField("_Name", Private).SetValue(block, name);
        block.Xml.Element("SW.Blocks.FB").Element("AttributeList").Element("Name").Value = name;
        typeof(XmlBlock).GetField("_Namespace", Private).SetValue(block, (XNamespace)"http://www.siemens.com/automation/Openness/SW/Interface/v5");
        typeof(XmlBlock).GetField("_ObjectList", Private).SetValue(block, block.Xml.Element("SW.Blocks.FB").Element("ObjectList"));
        return block;
    }
    static void StandaloneHelpers()
    {
        foreach (OperationInterlockLanguage language in Enum.GetValues(typeof(OperationInterlockLanguage)))
        {
            PlcProject.InterlockLanguage = language;
            var project = (PlcProject)typeof(DeviceStagingInputTests).GetMethod("Project", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            string originalPlant = project.Plant.Xml.ToString();
            var tp = HelperBlock("fbTPs"); var rp = HelperBlock("fbRPs");
            new Tp(tp, "TP-1", false, false, 100, 0, 10, "%", 2, "List");
            new Tp(tp, "TP-1", false, false, 120, 0, 10, "%", 2, "List");
            new Rp(rp, "RP-1", false, 100, 0, "%", 2, "List");
            foreach (var helper in new[] { tp, rp })
            {
                var call = helper.Networks.Values.Single().Calls.Values.Single();
                Reference(call, "RESET", "HMI|RESET", true);
                Check(helper.Instances.Count == 1 && Render(helper).Descendants().Count(e => e.Name.LocalName == "CallInfo") == 1, "Standalone helper duplicated execution or lost global instance ownership.");
            }
            Check(project.Plant.Xml.ToString() == originalPlant, "Standalone helper acquired an unintended generated Plant caller.");
            Check(tp.Networks.Values.Single().Access.Values.OfType<XmlConstant>().Any(c => c.Type == "Real" && c.Value == "120.0"), "Repeated fresh helper declaration did not update its constant.");
        }
    }
    static void OperandTransitions()
    {
        foreach (string version in new[] { "v4", "v5" })
        {
            var project = (PlcProject)typeof(DeviceStagingInputTests).GetMethod("Project", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            var unit = project.Plant.GetUnit("A2");
            var network = unit.GetNetwork("Transitions");
            var first = network.GetCall("fbDummy", "FB", "LocalVariable", "D1");
            network.SetParameter(first, "IN", "Input", "Bool", "False", neg: true);
            network.SetParameter(first, "Q", "Output", "Bool", "OUT", true);
            var second = network.GetCall("fbDummy", "FB", "LocalVariable", "D2");
            network.SetParameter(second, "IN", "Input", first.GetParameter("Q", "Output", "Bool"));
            Reload(unit, version); network = unit.FindNetwork("Transitions"); first = network.Calls["D1"];
            string inputWireId = (string)network.Xml.Descendants().Single(e => e.Name.LocalName == "NameCon" && (string)e.Attribute("UId") == first.UId.ToString() && (string)e.Attribute("Name") == "IN").Parent.Attribute("UId");
            var runtimeType = typeof(PlcProject).Assembly.GetType("TIA_LIB.Generation.ModularGenerationRuntime");
            var current = runtimeType.GetField("Current", BindingFlags.Static | BindingFlags.NonPublic); var previous = current.GetValue(null);
            try
            {
                current.SetValue(null, FormatterServices.GetUninitializedObject(runtimeType));
                network.SetParameter(first, "IN", "Input", "Bool", "DI_QB", true); Reference(first, "IN", "DI_QB", true);
                network.SetParameter(first, "IN", "Input", "Bool", "hwIN|DI_QB"); Reference(first, "IN", "hwIN|DI_QB");
                network.SetParameter(first, "IN", "Input", "Bool", "True");
                Check(network.Access[int.Parse(network.UIdIdentCon[first.UId + "|IN"])] is XmlConstant, "Symbol-to-constant repair kept a symbolic scope.");
                network.SetParameter(first, "IN", "Input", "Bool", "DI_QB", true); Reference(first, "IN", "DI_QB", true);
                network.SetParameter(first, "Q", "Output", "Bool", "hwOUT|Q"); Reference(first, "Q", "hwOUT|Q");
                var outputEndpoint = network.Xml.Descendants().Single(e => e.Name.LocalName == "NameCon" && (string)e.Attribute("UId") == first.UId.ToString() && (string)e.Attribute("Name") == "Q");
                Check(outputEndpoint.Parent.Elements().Count(e => e.Name.LocalName == "NameCon") == 2, "Output operand scope change disconnected a fan-out consumer.");
                var inputEndpoint = network.Xml.Descendants().Single(e => e.Name.LocalName == "NameCon" && (string)e.Attribute("UId") == first.UId.ToString() && (string)e.Attribute("Name") == "IN");
                Check((string)inputEndpoint.Parent.Attribute("UId") == inputWireId && first.Xml.Elements().Count(e => e.Name.LocalName == "Negated" && (string)e.Attribute("Name") == "IN") == 1, "Operand repair changed wire identity or inversion.");
                Check(GeneratedNetworkModel.FromGeneratedFbd("Transitions", "Transitions", network.Xml, new[] { "fbDummy" }).Invocations.Count == 2, "Operand transitions corrupted the shared model.");
            }
            finally { current.SetValue(null, previous); }
        }
    }
    public static void Run(string root)
    {
        string previous = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(Path.Combine(root, "TIAOpenness/TIA_Lib/TIA_LIB"));
            RuntimeDependencies();
            RuntimePaths();
            LibraryPinTypes();
            EmptyAndPidOnly();
            StandaloneHelpers();
            OperandTransitions();
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
                project.AddValve("A2", "V-1", interlockCount: 2, interlockSafeCount: 1, mon_opn: true, mon_cls: true);
                project.AddValveControl("A2", "CV-1", interlockCount: 2, interlockSafeCount: 1);
                project.AddMotor("A2", "M-1", interlockCount: 2, interlockSafeCount: 1, mon_on: true);
                project.AddMotorControl("A2", "MC-1", interlockCount: 2, interlockSafeCount: 1, mon_on: true);
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
                var op2 = (XmlOperation)typeof(SfcHssTests).GetMethod("Block", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { false });
                typeof(XmlBlock).GetField("_Name", Private).SetValue(op2, "OP2");
                op2.Xml.Element("SW.Blocks.FB").Element("AttributeList").Element("Name").Value = "OP2";
                typeof(XmlBlock).GetField("_Namespace", Private).SetValue(op2, (XNamespace)"http://www.siemens.com/automation/Openness/SW/Interface/v5");
                op2.Unit = unit; op2.Phases = new Dictionary<string, XmlPhase>();
                unit.Operations.Add("OP2", op2); unit.GetNetwork("OP2").GetCall("OP2", "FB", "LocalVariable", "OP2");
                Selector(op2, "OP2", "sfc_OP2");
                // Matches the user's declaration order: PID appears after operation calls.
                project.AddPidControl("A2", "PID-1");
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
                Reload(project.Plant, version); Reload(unit, version); Reload(op, version); Reload(op2, version);
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
                Reference(unit.FindNetwork("OP2").Calls["OP2"], "HSS", "iHSS");
                Reference(unit.FindNetwork("OP2").Calls["OP2"], "Delayed_Reset", "iReset_Delayed");
                Reference(op2.FindNetwork("OP2").Calls["sfc_OP2"], "HSS", "HSS");
                Check(!op.FindNetwork("OP1").Calls["sfc_OP1"].Parameters.ContainsKey("Reset"), "Invented operation selector reset behavior.");
                foreach (string name in op.Phases.Keys)
                {
                    Reference(op.FindNetwork("Sfc control " + name).Calls["sfc_" + name], "HSS", "HSS");
                    Reference(op.FindNetwork("Sfc control " + name).Calls["sfc_" + name], "Reset", "Delayed_Reset");
                }
                foreach (var block in new XmlBlock[] { unit, op, op2 })
                    Check(!block.Networks.Values.SelectMany(n => n.Access.Values.OfType<XmlGlobal>()).Any(a =>
                        (string)a.Xml.Attribute("Scope") == "GlobalVariable" && new[] { "HMI|RESET", "HMI|RESET_DELAYED", "HMI|MODE_OFF", "HMI|MODE_SERV", "F_Safe|HSS" }.Contains(a.Value)), "Owned block retained a hidden control-global reference.");
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    string snapshot = project.Plant.Xml.ToString() + unit.Xml.ToString() + op.Xml.ToString() + op2.Xml.ToString();
                    project.Plant.HasChanged = unit.HasChanged = op.HasChanged = op2.HasChanged = false;
                    Prepare(project.Plant);
                    Check(snapshot == project.Plant.Xml.ToString() + unit.Xml.ToString() + op.Xml.ToString() + op2.Xml.ToString() && !project.Plant.HasChanged && !unit.HasChanged && !op.HasChanged && !op2.HasChanged, "Modular control routing not idempotent.");
                    Reload(project.Plant, version); Reload(unit, version); Reload(op, version); Reload(op2, version);
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
                var nestedInventory = ConfigurationMigrationPlan.Inspect(unit, proposed, artifacts.Udt);
                Check(!nestedInventory.RequiresMigration && nestedInventory.Configuration.Leaves.Count == 11, "Owned nested CFG inventory was mistaken for another legacy migration.");
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
                    Check(!groupedNetwork.Descendants("Text").Any(t => t.Value.Contains("[ETU group: Devices/")), "Unwanted FBD group comment was generated.");
                var legacy = logicalDevices[0].Body.RenderFbd();
                foreach (var text in legacy.Descendants("MultilingualText").Single(e => (string)e.Attribute("CompositionName") == "Comment").Descendants("Text"))
                    text.Value = "[ETU group: Devices/Analog_Sensors]\nKeep commissioning note";
                var legacyModel = GeneratedNetworkModel.FromGeneratedFbd("LegacyHeader", "Original device title", legacy,
                    new[] { "fbMonAn", "fbMonDi", "fbVlv2Out", "fbVlvCtrl", "fbMotS", "fbMotAn", "fbPIDCtrl", "fbIntlck7" });
                var cleaned = new GeneratedBlockBody(new[] { new GeneratedLogicalNetwork(legacyModel, DeviceCategory.Analog_Sensors) }).RenderFbd().Single();
                Check(cleaned.Descendants("MultilingualText").Single(e => (string)e.Attribute("CompositionName") == "Comment").Descendants("Text").All(t => t.Value == "Keep commissioning note"), "Removing the generated header damaged existing comments.");
                foreach (var sourceBlock in new XmlBlock[] { unit, op, op2 })
                {
                    var rendered = Render(sourceBlock);
                    var beforeInterface = sourceBlock.Xml.Element("SW.Blocks.FB").Element("AttributeList").Element("Interface");
                    Check(XNode.DeepEquals(beforeInterface, rendered.Element("SW.Blocks.FB").Element("AttributeList").Element("Interface")), "Runtime renderer changed interface/Static comments.");
                    Check((string)rendered.Element("SW.Blocks.FB").Element("AttributeList").Element("ProgrammingLanguage") == (language == OperationInterlockLanguage.Scl ? "SCL" : "FBD"), "Runtime renderer selected wrong block language.");
                    int calls = sourceBlock.Networks.Values.Sum(n => n.Calls.Count);
                    Check(rendered.Descendants().Count(e => e.Name.LocalName == "CallInfo") == calls, "Runtime renderer lost/duplicated Unit/Operation calls.");
                    if (language == OperationInterlockLanguage.Scl) Check(!rendered.Descendants().Any(e => e.Name.LocalName == "FlgNet"), "SCL block retained FBD executable body.");
                    else Check(!rendered.Descendants().Any(e => e.Name.LocalName == "StructuredText"), "FBD block retained SCL body.");
                }
                // A new execution reconstructs devices against the owned FBD shadow.
                // Exercise constructor re-entry with finalized local CFG sources: legacy
                // SetParameter must not turn CFG into LocalVariable cfgData|flat-name.
                var runtimeType = typeof(PlcProject).Assembly.GetType("TIA_LIB.Generation.ModularGenerationRuntime");
                var runtimeCurrent = runtimeType.GetField("Current", BindingFlags.Static | BindingFlags.NonPublic);
                var previousRuntime = runtimeCurrent.GetValue(null);
                try
                {
                    runtimeCurrent.SetValue(null, FormatterServices.GetUninitializedObject(runtimeType));
                    unit.Devices.Clear(); project.StagingInventory = new SignalStagingInventory();
                    project.Analogs.Clear(); project.Digitals.Clear(); project.Valves.Clear(); project.ControlValves.Clear(); project.Motors.Clear();
                    project.AddAnalog("A2", "AI-1", instanceCount: 2); project.AddDigital("A2", "DI-1", instanceCount: 2);
                    project.AddValve("A2", "V-1", interlockCount: 2, interlockSafeCount: 1, mon_opn: true, mon_cls: true); project.AddValveControl("A2", "CV-1", interlockCount: 2, interlockSafeCount: 1); project.AddMotor("A2", "M-1", interlockCount: 2, interlockSafeCount: 1, mon_on: true);
                    project.AddMotorControl("A2", "MC-1", interlockCount: 2, interlockSafeCount: 1, mon_on: true); project.AddPidControl("A2", "PID-1");
                    foreach (var device in unit.Devices.Values)
                    {
                        var calls = (IEnumerable<XmlCall>)typeof(TIA_LIB.Devices.GeneratedObject).GetMethod("ConfiguredDeviceCalls", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(device, null);
                        foreach (var call in calls) Reference(call, "CFG", "CFG|" + call.InstName);
                    }
                    Prepare(project.Plant); Render(unit);
                }
                finally { runtimeCurrent.SetValue(null, previousRuntime); }
            }
            Console.WriteLine("PASS: explicit controls, seven-family configuration artifacts/bindings, migration inventory, grouping and integrated Unit/Operation renderers in both modes/languages/v4-v5. No Portal import/runtime execution.");
        }
        finally { Directory.SetCurrentDirectory(previous); }
    }
}
