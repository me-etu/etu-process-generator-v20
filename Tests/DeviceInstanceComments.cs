using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using TIA_LIB;
using TIA_LIB.Devices;
using TIA_LIB.SignalStaging;
using TIA_LIB.Xml;

// No SiemensPortal, PlcProject, XmlPlant, XmlUnit or XmlBlock constructor is called.
// Only device constructors and XML helpers run. An uninitialized SiemensPortal holds
// in-memory HMI/cfgData DB caches; all live Siemens session fields remain null.
public static class DeviceInstanceComments
{
    static readonly XNamespace Ns = "http://www.siemens.com/automation/Openness/SW/Interface/v5";
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static XmlSchemaSet Schemas;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static T Bare<T>() { return (T)FormatterServices.GetUninitializedObject(typeof(T)); }
    static void Field(object obj, Type type, string name, object value) { type.GetField(name, Private).SetValue(obj, value); }
    static XElement Sections(XmlBlock block) { return block.Xml.Descendants(Ns + "Sections").First(); }
    static string Direct(XmlLocalInstance instance, string culture)
    {
        return (string)instance.Xml.Elements(Ns + "Comment").Elements(Ns + "MultiLanguageText")
            .FirstOrDefault(t => (string)t.Attribute("Lang") == culture);
    }
    static void Validate(XmlBlock block)
    {
        var doc = new XDocument(new XElement(Sections(block)));
        doc.Validate(Schemas, (o, e) => { throw new Exception(e.Message); });
    }
    static XmlUnit Unit(string name, string culture)
    {
        var unit = Bare<XmlUnit>();
        unit.Xml = XElement.Load("Xml/EmptyUnit.xml");
        Field(unit, typeof(XmlBlock), "_Namespace", Ns);
        Field(unit, typeof(XmlBlock), "_Name", name);
        Field(unit, typeof(XmlBlock), "_ObjectList", unit.Xml.Descendants("SW.Blocks.FB").Elements("ObjectList").First());
        Field(unit, typeof(XmlBlock), "_AllowedCultures", new HashSet<string>(culture.Split(','), StringComparer.OrdinalIgnoreCase));
        unit.LocalInstance = new Dictionary<string, XmlLocalInstance>();
        unit.Networks = new Dictionary<string, XmlNetwork>();
        unit.Devices = new Dictionary<string, GeneratedObject>();
        var network = unit.GetNetwork(name);
        var call = network.GetCall("fbUnit", "FB", "LocalVariable", name);
        unit.SetLocalInstanceMemberComment(name, "Input", "FUNC_TEXT", "Bool", "wrapper preserved");
        return unit;
    }
    static PlcProject Project(XmlUnit unit)
    {
        var plant = Bare<XmlPlant>();
        plant.Units = new Dictionary<string, XmlUnit>();
        plant.Units.Add("fb" + unit.Name, unit);
        XmlPlant.Current = plant;
        var project = Bare<PlcProject>();
        project.Plant = plant;
        project.StagingInventory = new SignalStagingInventory();
        project.Valves = new List<PlcProject.TagValve>();
        project.Analogs = new List<PlcProject.TagAnalog>();
        project.Digitals = new List<PlcProject.TagDigital>();
        project.ControlValves = new List<PlcProject.TagControlValve>();
        project.Motors = new List<PlcProject.TagMotor>();
        return project;
    }
    static GeneratedObject Add(PlcProject project, string method, string name, int count, string source, int line, int ordinary = 0, int safe = 0, bool? quality = null)
    {
        var info = typeof(PlcProject).GetMethod(method);
        var args = info.GetParameters().Select(p => p.DefaultValue).ToArray();
        args[0] = project.Plant.Units.Values.First().Name;
        args[1] = name;
        foreach (var p in info.GetParameters()) {
            if (p.Name == "instanceCount") args[p.Position] = count;
            if (p.Name == "interlockCount") args[p.Position] = ordinary;
            if (p.Name == "interlockSafeCount") args[p.Position] = safe;
            if (p.Name == "qualityBit" && quality.HasValue) args[p.Position] = quality.Value;
            if (p.Name == "callerFilePath") args[p.Position] = source;
            if (p.Name == "callerLineNumber") args[p.Position] = line;
        }
        return (GeneratedObject)info.Invoke(project, args);
    }
    static XElement WithoutDirectComments(XmlUnit unit)
    {
        var copy = new XElement(unit.Xml);
        foreach (var comment in copy.Descendants(Ns + "Section").Where(s => (string)s.Attribute("Name") == "Static")
            .Elements(Ns + "Member").Elements(Ns + "Comment").ToList()) comment.Remove();
        return copy;
    }
    public static void Run(string root, string schemaDirectory)
    {
        Check(SiemensPortal.Current == null, "Offline test requires no Portal instance.");
        Schemas = new XmlSchemaSet();
        Schemas.XmlResolver = new XmlUrlResolver();
        Schemas.Add(Ns.NamespaceName, Path.Combine(schemaDirectory, "SW.InterfaceSections_v5.xsd"));
        var source = Path.Combine(root, "Tests", ".device-comments-source.tmp");
        var originalPortal = SiemensPortal.Current;
        var offlinePortal = Bare<SiemensPortal>();
        offlinePortal.GlobalDatablocks = new Dictionary<string, XmlDatablock>();
        foreach (var dbName in new[] { "HMI", "cfgData", "F_Safe" }) {
            var db = Bare<XmlDatablock>();
            db.SectionList = new XElement("Section");
            db.GlobalsInstance = new Dictionary<string, XmlGlobalInstance>();
            offlinePortal.GlobalDatablocks.Add(dbName, db);
        }
        SiemensPortal.Current = offlinePortal;
        var originalPlant = XmlPlant.Current;
        var originalMode = PlcProject.StagingMode;
        try {
            File.WriteAllLines(source, new[] {
                "AddAnalog(\"BV130_B10_20\", \"LC-B13\", 2, 0, \"%\", 0, 0, 100); // Level radar sensor BV130 B13",
                "Call(\"http://example\", '\"'); // Füllstand < & \"quoted\"",
                "Call();", "Call(); //   ", "Call(\"//\"); // Changed description"
            });
            foreach (SignalStagingMode mode in Enum.GetValues(typeof(SignalStagingMode))) {
                PlcProject.StagingMode = mode;
                foreach (var method in new[] { "AddAnalog", "AddDigital", "AddValve", "AddValveControl", "AddMotor", "AddMotorControl", "AddPidControl" }) {
                    foreach (int count in (method == "AddAnalog" || method == "AddDigital" ? new[] { 0, 1, 2 } : new[] { 0 })) {
                        var unit = Unit("BV130_B10_20", "en-US,de-DE");
                        var project = Project(unit);
                        string name = method == "AddAnalog" ? "LC-B13" : "Q1";
                        var unrelated = unit.GetLocalInstance(name + "1", "fbMonAn");
                        var helper = unit.GetLocalInstance(name + "_Interlock1", "fbIntlck7");
                        var otherUnit = Unit("OtherUnit", "en-US");
                        var other = otherUnit.GetLocalInstance(name, "fbMonAn");
                        var otherBefore = otherUnit.Xml.ToString();
                        var device = Add(project, method, name, count, source, 1);
                        for (int i = 0; i <= count; i++) {
                            var instance = unit.LocalInstance[i == 0 ? name : name + "_Instance" + i];
                            Check(Direct(instance, "en-US") == "Level radar sensor BV130 B13", method + " exact declaration");
                        }
                        Check(!unrelated.Xml.Elements(Ns + "Comment").Any() && !helper.Xml.Elements(Ns + "Comment").Any(), "Exact instance selection");
                        Check(otherBefore == otherUnit.Xml.ToString(), "Other unit isolation");
                        Validate(unit);
                        var network = unit.FindNetwork(name);
                        Check(!network.Xml.Descendants("Text").Any(t => t.Value == "Level radar sensor BV130 B13"), "No description on new network");
                        network.SetComment("Legacy manual network comment");
                        var networkBefore = network.Xml.ToString();
                        var logicBefore = WithoutDirectComments(unit);
                        unit.HasChanged = false;
                        Check(object.ReferenceEquals(device, Add(project, method, name, count, source, 1)), "Cached device identity");
                        Check(!unit.HasChanged, "Same-text cached call stays clean");
                        Add(project, method, name, count, source, 2);
                        Check(unit.HasChanged, "Comment-only update dirty");
                        for (int i = 0; i <= count; i++)
                            Check(Direct(unit.LocalInstance[i == 0 ? name : name + "_Instance" + i], "en-US") == "Füllstand < & \"quoted\"", "Every monitor updated / escaped source extraction");
                        Check(XNode.DeepEquals(logicBefore, WithoutDirectComments(unit)), "Logic and nested FUNC_TEXT preserved");
                        Check(networkBefore == network.Xml.ToString(), "Cached path preserves network");
                        unit.HasChanged = false;
                        Add(project, method, name, count, source, 3);
                        Add(project, method, name, count, source, 4);
                        Add(project, method, name, count, source + ".missing", 1);
                        Check(!unit.HasChanged, "Missing/empty source no-op");
                        // Re-enter actual device constructor against the existing call/declaration.
                        unit.Devices.Remove(name);
                        Add(project, method, name, count, source, 5);
                        Check(Direct(unit.LocalInstance[name], "en-US") == "Changed description", "Existing declaration constructor update");
                        Check(network.Xml.Descendants("MultilingualText").Where(t => (string)t.Attribute("CompositionName") == "Comment").Descendants("Text").All(t => t.Value == "Legacy manual network comment"), "Existing network comment preserved");
                        Validate(unit);
                        Console.WriteLine("PASS: " + mode + " " + method + " monitors=" + count + " new/existing/cached");
                    }
                }
                InterlockFixtures(source);
                DigitalQualityFixtures(source);
                // Match the user's failing unit topology without opening Portal or uploading.
                var runtimeUnit = Unit("BV130_B10_20", "en-US,de-DE");
                var runtimeProject = Project(runtimeUnit);
                Add(runtimeProject, "AddAnalog", "LC-B13", 3, source, 1);
                Add(runtimeProject, "AddDigital", "LSA-B23", 1, source, 1);
                Add(runtimeProject, "AddDigital", "MS-G11", 2, source, 1);
                Add(runtimeProject, "AddValve", "Q11", 0, source, 1, 4, 0);
                Add(runtimeProject, "AddMotor", "G11", 0, source, 1, 2, 1);
                foreach (var runtimeNetwork in runtimeUnit.Networks.Values) CheckUniqueConnections(runtimeNetwork);
                Validate(runtimeUnit);
                Console.WriteLine("PASS: " + mode + " reported BV130_B10_20 topology has unique pin connections.");
            }
            DeclarationFixtures();
            Check(offlinePortal.Portal == null && offlinePortal.OpenProject == null && offlinePortal.PlcSoftware == null, "No live Portal/session.");
            Console.WriteLine("PASS: all device instance comment checks; no Portal connection/import.");
        }
        finally {
            SiemensPortal.Current = originalPortal;
            XmlPlant.Current = originalPlant;
            PlcProject.StagingMode = originalMode;
            if (File.Exists(source)) File.Delete(source);
        }
    }
    static void DigitalQualityFixtures(string source)
    {
        foreach (int count in new[] { 0, 1, 2 })
        foreach (bool quality in new[] { false, true }) {
            var unit = Unit("DigitalQuality", "en-US");
            var project = Project(unit);
            Add(project, "AddDigital", "MS-G11", count, source, 1, quality: quality);
            CheckDigitalQuality(project, unit, count, quality);
            string before = unit.Xml.ToString();
            unit.HasChanged = false;
            Add(project, "AddDigital", "MS-G11", count, source, 1, quality: quality);
            Check(!unit.HasChanged && before == unit.Xml.ToString(), "Cached quality wiring is idempotent");
            var network = unit.FindNetwork("MS-G11");
            if (!quality) {
                // Reproduce XML emitted by the old generator, including negated ERR_EXT.
                var reference = SignalStagingInventory.InputReference("IN_MS-G11_QB");
                for (int i = 0; i <= count; i++) {
                    var call = network.FindCall("fbMonDi", "FB", "LocalVariable", i == 0 ? "MS-G11" : "MS-G11_Instance" + i);
                    network.SetParameter(call, "ERR_EXT", "Input", "Bool", reference.Value, reference.IsGlobal, false, true);
                }
                var otherCall = network.GetCall("fbMonDi", "FB", "LocalVariable", "ManualMonitor");
                network.SetParameter(otherCall, "ERR_EXT", "Input", "Bool", "ManualError", true);
                string otherBefore = otherCall.Xml.ToString();
                var unrelatedWires = network.Xml.Descendants().Where(e => e.Name.LocalName == "Wire" &&
                    !e.Elements().Any(c => c.Name.LocalName == "NameCon" && (string)c.Attribute("Name") == "ERR_EXT" &&
                        (string)c.Attribute("UId") != otherCall.UId.ToString())).Select(e => e.ToString()).ToArray();
                unit.HasChanged = false;
                Add(project, "AddDigital", "MS-G11", count, source, 1, quality: false);
                Check(unit.HasChanged, "Legacy QB repair marks unit changed");
                CheckDigitalQuality(project, unit, count, false);
                Check(otherBefore == otherCall.Xml.ToString(), "Unrelated manual call is preserved");
                Check(unrelatedWires.All(w => network.Xml.Descendants().Any(e => e.Name.LocalName == "Wire" && e.ToString() == w)), "Unrelated wires are preserved");
                // Restore the old error so the exported-network path must repair it too.
                for (int i = 0; i <= count; i++) {
                    var call = network.FindCall("fbMonDi", "FB", "LocalVariable", i == 0 ? "MS-G11" : "MS-G11_Instance" + i);
                    network.SetParameter(call, "ERR_EXT", "Input", "Bool", reference.Value, reference.IsGlobal, false, true);
                }
            }
            // Exercise the actual existing-network parser with qualified exported FlgNet XML.
            var exported = new XElement(network.Xml);
            XNamespace flg = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v4";
            foreach (var element in exported.Descendants().First(e => e.Name.LocalName == "FlgNet").DescendantsAndSelf())
                element.Name = flg + element.Name.LocalName;
            network.Xml.ReplaceWith(exported);
            unit.Networks.Remove(network.Name);
            new XmlNetwork(unit, exported);
            unit.Devices.Remove("MS-G11");
            project = Project(unit); // A fresh run rebuilds the staging inventory from its DSL.
            Add(project, "AddDigital", "MS-G11", count, source, 1, quality: quality);
            CheckDigitalQuality(project, unit, count, quality);
            var clean = unit.Xml.ToString();
            unit.HasChanged = false;
            Add(project, "AddDigital", "MS-G11", count, source, 1, quality: quality);
            Check(!unit.HasChanged && clean == unit.Xml.ToString(), "Repaired quality wiring remains clean");
            Validate(unit);
            Console.WriteLine("PASS: " + PlcProject.StagingMode + " digital QB=" + quality + " monitors=" + count + " new/exported/cached");
        }
    }
    static void CheckDigitalQuality(PlcProject project, XmlUnit unit, int count, bool quality)
    {
        Check(project.StagingInventory.Units.Single().Inputs.Any(s => s.SignalName == "IN_MS-G11_QB") == quality, "QB input inventory follows flag");
        Check(project.Digitals.Single().QualityBit == quality, "Marker QB tag creation follows flag");
        var network = unit.FindNetwork("MS-G11");
        var reference = SignalStagingInventory.InputReference("IN_MS-G11_QB");
        for (int i = 0; i <= count; i++) {
            var call = network.FindCall("fbMonDi", "FB", "LocalVariable", i == 0 ? "MS-G11" : "MS-G11_Instance" + i);
            var endpoints = network.Xml.Descendants().Where(e => e.Name.LocalName == "NameCon" &&
                (string)e.Attribute("UId") == call.UId.ToString() && (string)e.Attribute("Name") == "ERR_EXT").ToList();
            Check(endpoints.Count == (quality ? 1 : 0), "Every monitor obeys QB flag");
            Check(call.Xml.Elements().Count(e => e.Name.LocalName == "Negated" && (string)e.Attribute("Name") == "ERR_EXT") == (quality ? 1 : 0), "QB inversion follows connection");
            if (quality) {
                var ident = endpoints.Single().Parent.Elements().Single(e => e.Name.LocalName == "IdentCon");
                var access = network.Parts.Elements().Single(e => (string)e.Attribute("UId") == (string)ident.Attribute("UId"));
                Check(string.Join("|", access.Descendants().Where(e => e.Name.LocalName == "Component").Select(e => (string)e.Attribute("Name"))) == reference.Value, "Exact QB path");
                Check((string)access.Attribute("Scope") == (reference.IsGlobal ? "GlobalVariable" : "LocalVariable"), "QB reference scope");
            }
        }
        if (!quality) Check(!network.Parts.Elements().Any(e => e.Name.LocalName == "Access" &&
            e.Descendants().Any(c => c.Name.LocalName == "Component" && ((string)c.Attribute("Name")).EndsWith("_QB"))), "No dangling quality accesses");
    }
    static void InterlockFixtures(string source)
    {
        foreach (var method in new[] { "AddValve", "AddValveControl", "AddMotor", "AddMotorControl" })
        foreach (var counts in new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 3, 0 }, new[] { 0, 3 }, new[] { 2, 1 } })
        foreach (var culture in new[] { "en-US,de-DE", "de-DE" })
        foreach (bool emptyFirst in new[] { false, true }) {
            var unit = Unit("InterlockFixture", culture);
            var project = Project(unit);
            int total = counts[0] + counts[1];
            string lang = culture.Split(',')[0];
            var device = Add(project, method, "Q11", 0, source, emptyFirst ? 3 : 1, counts[0], counts[1]);
            CheckUniqueConnections(unit.FindNetwork("Q11"));
            if (method == "AddMotor" && total > 0) {
                var network = unit.FindNetwork("Q11");
                CheckMotorSignal(network, "FB_ON", SignalStagingInventory.InputReference("FB_ON_Q11"));
                CheckMotorSignal(network, "QCMD_ON", SignalStagingInventory.OutputReference("CTRL_Q11"));
            }
            var targets = new List<XmlLocalInstance> { unit.LocalInstance["Q11"] };
            for (int i = 1; i <= total; i++) targets.Add(unit.LocalInstance["Q11_Interlock" + i]);
            Check(unit.LocalInstance.Keys.Count(k => k.StartsWith("Q11_Interlock", StringComparison.Ordinal)) == total, "Exact helper count, including zero");
            foreach (var target in targets) {
                Check(Direct(target, lang) == (emptyFirst ? null : "Level radar sensor BV130 B13"), "New equipment target description");
                target.SetDeclarationComment("Manual existing text");
                target.Xml.Element(Ns + "Comment").Add(new XElement(Ns + "MultiLanguageText", new XAttribute("Lang", "fr-FR"), "Traduit"));
                target.SetMemberComment("Input", "FUNC_TEXT", "Bool", "Nested preserved");
            }
            var excluded = new[] {
                unit.GetLocalInstance("Q11_Interlock" + (total + 1), "fbIntlck7"),
                unit.GetLocalInstance("Q1_Interlock1", "fbIntlck7"),
                unit.GetLocalInstance("Q11_InterlockManual", "fbWrongType")
            };
            foreach (var instance in excluded) instance.SetDeclarationComment("Excluded");
            var otherUnit = Unit("OtherInterlock", culture);
            otherUnit.GetLocalInstance("Q11_Interlock1", "fbIntlck7").SetDeclarationComment("Other unit");
            string otherBefore = otherUnit.Xml.ToString();
            unit.FindNetwork("Q11").SetComment("Legacy network metadata");
            // Re-enter the constructor to reuse actual declarations with manual text.
            unit.Devices.Remove("Q11");
            device = Add(project, method, "Q11", 0, source, emptyFirst ? 3 : 1, counts[0], counts[1]);
            foreach (var target in targets)
                Check(Direct(target, lang) == (emptyFirst ? "Manual existing text" : "Level radar sensor BV130 B13"), "Existing equipment declaration reuse");
            var before = WithoutTargetComments(unit, targets);
            unit.HasChanged = false;
            Check(object.ReferenceEquals(device, Add(project, method, "Q11", 0, source, 2, counts[0], counts[1])), "Cached equipment identity");
            foreach (var target in targets) {
                Check(Direct(target, lang) == "Füllstand < & \"quoted\"", "All cached equipment descriptions / escaping");
                Check(Direct(target, "fr-FR") == "Traduit", "Helper translations preserved");
                Check(target.Xml.Elements(Ns + "Comment").Count() == 1 && target.Xml.Element(Ns + "Comment").Elements().Count(t => (string)t.Attribute("Lang") == lang) == 1, "No duplicate helper comments");
                if (lang == "de-DE") Check(Direct(target, "en-US") == null, "Helper de-DE only selection");
            }
            Check(unit.HasChanged, "Changed equipment description dirty");
            Check(XNode.DeepEquals(before, WithoutTargetComments(unit, targets)), "Only configured direct comments change in complete block");
            Check(otherBefore == otherUnit.Xml.ToString(), "Other equipment unit preserved");
            string serialized = unit.Xml.ToString();
            unit.HasChanged = false;
            Add(project, method, "Q11", 0, source, 2, counts[0], counts[1]);
            Add(project, method, "Q11", 0, source, 3, counts[0], counts[1]);
            Add(project, method, "Q11", 0, source, 4, counts[0], counts[1]);
            Add(project, method, "Q11", 0, source + ".missing", 1, counts[0], counts[1]);
            Check(!unit.HasChanged && serialized == unit.Xml.ToString(), "Equipment identical/empty/unavailable updates preserve XML");
            Validate(unit);
            if (total > 0) {
                var helper = unit.LocalInstance["Q11_Interlock1"];
                unit.LocalInstance.Remove(helper.Name);
                bool diagnosed = false;
                try { device.SetDeviceDescription("Missing target"); }
                catch (InvalidOperationException e) { diagnosed = e.Message.Contains(helper.Name) && e.Message.Contains("fbIntlck7") && e.Message.Contains(unit.Name); }
                Check(diagnosed && !unit.LocalInstance.ContainsKey(helper.Name), "Missing configured helper diagnostic; no phantom");
                unit.LocalInstance.Add(helper.Name, helper);
                helper.Datatype = "fbWrongType";
                string helperBefore = helper.Xml.ToString();
                diagnosed = false;
                try { device.SetDeviceDescription("Wrong target"); } catch (InvalidOperationException) { diagnosed = true; }
                Check(diagnosed && helperBefore == helper.Xml.ToString(), "Wrong helper type diagnosed and untouched");
            }
            Console.WriteLine("PASS: " + PlcProject.StagingMode + " " + method + " ordinary/safe=" + counts[0] + "/" + counts[1] + " " + culture + " empty-first=" + emptyFirst);
        }
    }
    static XElement WithoutTargetComments(XmlUnit unit, IEnumerable<XmlLocalInstance> targets)
    {
        var names = new HashSet<string>(targets.Select(t => t.Name));
        var copy = new XElement(unit.Xml);
        foreach (var comment in copy.Descendants(Ns + "Section").Where(s => (string)s.Attribute("Name") == "Static")
            .Elements(Ns + "Member").Where(m => names.Contains((string)m.Attribute("Name"))).Elements(Ns + "Comment").ToList()) comment.Remove();
        return copy;
    }
    static void CheckUniqueConnections(XmlNetwork network)
    {
        var duplicate = network.Xml.Descendants().Where(e => e.Name.LocalName == "NameCon")
            .GroupBy(e => (string)e.Attribute("UId") + "|" + (string)e.Attribute("Name"))
            .FirstOrDefault(g => g.Count() > 1);
        Check(duplicate == null, "Duplicate connection in " + network.Name + ": " + (duplicate == null ? "" : duplicate.Key));
    }
    static void CheckMotorSignal(XmlNetwork network, string pin, SignalReference reference)
    {
        var call = network.FindCall("fbMotS", "FB", "LocalVariable", "Q11");
        var wire = network.Xml.Descendants().Where(e => e.Name.LocalName == "NameCon" &&
            (string)e.Attribute("UId") == call.UId.ToString() && (string)e.Attribute("Name") == pin).Single().Parent;
        var uid = wire.Elements().Single(e => e.Name.LocalName == "IdentCon").Attribute("UId").Value;
        var access = network.Parts.Elements().Single(e => (string)e.Attribute("UId") == uid);
        var path = string.Join("|", access.Descendants().Where(e => e.Name.LocalName == "Component").Select(e => (string)e.Attribute("Name")));
        Check(path == reference.Value && (string)access.Attribute("Scope") == (reference.IsGlobal ? "GlobalVariable" : "LocalVariable"), "Motor " + pin + " staging reference");
    }
    static void DeclarationFixtures()
    {
        foreach (var policy in new[] { new[] { "de-DE,en-US", "en-US" }, new[] { "it-IT,fr-FR", "fr-FR" } }) {
            var languageUnit = Unit("Language", policy[0]);
            var languageInstance = languageUnit.GetLocalInstance("Q1", "fbVlv2Out");
            languageInstance.SetDeclarationComment("Language selection");
            Check(Direct(languageInstance, policy[1]) == "Language selection", "Supported culture preference/order");
            Validate(languageUnit);
        }
        foreach (bool legacy in new[] { false, true }) {
            var unit = Unit("Fixture", "de-DE");
            var member = XElement.Parse("<Member Name='LC-B13' Datatype='&quot;fbMonAn&quot;' Accessibility='Public'><AttributeList/><Comment><MultiLanguageText Lang='de-DE'>Generic</MultiLanguageText><MultiLanguageText Lang='fr-FR'>Traduit</MultiLanguageText></Comment><Sections><Section Name='Input'><Member Name='FUNC_TEXT' Datatype='Bool'><Comment><MultiLanguageText Lang='de-DE'>Nested</MultiLanguageText></Comment></Member></Section></Sections></Member>");
            if (!legacy) foreach (var el in member.DescendantsAndSelf()) el.Name = Ns + el.Name.LocalName;
            Sections(unit).Elements(Ns + "Section").First(s => (string)s.Attribute("Name") == "Static").Add(member);
            var instance = new XmlLocalInstance(unit, member);
            unit.HasChanged = false;
            instance.SetDeclarationComment("Füllstand < & \"quotes\"");
            Check(unit.HasChanged, "Export/legacy update dirty");
            Check(Direct(instance, "de-DE") == "Füllstand < & \"quotes\"" && Direct(instance, "en-US") == null, "de-DE only culture");
            Check(Direct(instance, "fr-FR") == "Traduit", "Other translation retained");
            Check(member.Elements().First().Name == Ns + "AttributeList", "AttributeList ordering");
            Check((string)member.Attribute("Datatype") == "\"fbMonAn\"", "Datatype quoting preserved");
            Check(member.Descendants(Ns + "Member").First().Value == "Nested", "Nested text preserved");
            Validate(unit);
            var serialized = unit.Xml.ToString();
            unit.HasChanged = false;
            instance.SetDeclarationComment("Füllstand < & \"quotes\"");
            instance.SetDeclarationComment("  ");
            Check(!unit.HasChanged && serialized == unit.Xml.ToString(), "Declaration idempotence");
            var duplicate = new XElement(Ns + "Comment", new XElement(Ns + "MultiLanguageText", new XAttribute("Lang", "de-DE"), "duplicate"));
            member.Add(duplicate);
            instance.SetDeclarationComment("New");
            Check(member.Elements(Ns + "Comment").Count() == 1 && member.Elements(Ns + "Comment").Elements().Count(t => (string)t.Attribute("Lang") == "de-DE") == 1, "Duplicate normalization");
            Validate(unit);
        }
        var missing = new AnalogFixture(Unit("Missing", "en-US"));
        bool diagnosed = false;
        try { missing.SetDeviceDescription("Description"); } catch (InvalidOperationException e) { diagnosed = e.Message.Contains("Missing") && e.Message.Contains("Absent"); }
        Check(diagnosed && !missing.Unit.LocalInstance.ContainsKey("Absent"), "Missing declaration diagnostic; no phantom");
        missing.Unit.GetLocalInstance("Absent", "fbWrongType");
        diagnosed = false;
        try { missing.SetDeviceDescription("Description"); } catch (InvalidOperationException) { diagnosed = true; }
        Check(diagnosed && Direct(missing.Unit.LocalInstance["Absent"], "en-US") == null, "Mismatched FB type excluded");
        Console.WriteLine("PASS: namespace/schema, languages, escaping, ordering, preservation, duplicates, missing declaration");
    }
    class AnalogFixture : GeneratedObject {
        public AnalogFixture(XmlUnit unit) : base(unit, "Absent") { ConfigureInstanceDescription("fbMonAn", 0, ""); }
    }
}
