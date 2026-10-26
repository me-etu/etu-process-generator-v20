using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml.Linq;
using TIA_LIB.Xml;

namespace TIA_LIB.Generation
{
    public static class UnitConfigurationCallAudit
    {
        public static bool ValidateCaller(XElement document, string caller, string plant, string unitBlock, string unitInstance, bool instanceOfUnit = false)
        {
            if (!References(document, unitBlock, unitInstance) && !instanceOfUnit) return false;
            if (caller != plant) throw new InvalidOperationException("External/manual Unit caller requires reconciliation: " + caller + " -> " + unitBlock);
            var calls = document.Descendants().Where(e => e.Name.LocalName == "CallInfo" && (string)e.Attribute("Name") == unitBlock).ToArray();
            if (calls.Length != 1 || !calls[0].Elements().Any(e => e.Name.LocalName == "Instance" && (string)e.Attribute("Scope") == "GlobalVariable" &&
                e.Elements().Count(c => c.Name.LocalName == "Component") == 1 && (string)e.Elements().FirstOrDefault(c => c.Name.LocalName == "Component")?.Attribute("Name") == unitInstance))
                throw new InvalidOperationException("Ambiguous/manual Plant callers require Unit CFG reconciliation: " + unitBlock);
            return true;
        }
        public static bool References(XElement document, string unitBlock, string unitInstance)
        {
            return document.Descendants().Any(e =>
                (e.Name.LocalName == "CallInfo" && string.Equals((string)e.Attribute("Name"), unitBlock, StringComparison.OrdinalIgnoreCase)) ||
                (e.Name.LocalName == "Member" && string.Equals(((string)e.Attribute("Datatype"))?.Trim('"'), unitBlock, StringComparison.OrdinalIgnoreCase)) ||
                (e.Name.LocalName == "InstanceOfName" && string.Equals(e.Value, unitBlock, StringComparison.OrdinalIgnoreCase)) ||
                ((e.Name.LocalName == "Instance" || e.Name.LocalName == "Access") && (string)e.Attribute("Scope") == "GlobalVariable" &&
                    string.Equals((string)e.Descendants().FirstOrDefault(c => c.Name.LocalName == "Component")?.Attribute("Name"), unitInstance, StringComparison.OrdinalIgnoreCase)));
        }
    }
    public sealed class UnitModeTarget
    {
        public string Instance { get; }
        public string BlockType { get; }
        public string ConfigurationType { get; }
        public bool Service { get; }
        internal UnitModeTarget(string instance, string block, string configuration, bool service)
        { Instance = instance; BlockType = block; ConfigurationType = configuration; Service = service; }
    }

    // Inventory and ordered standalone assignments, shared by both body backends.
    // No Portal calls, interface changes, DB writes or registration occur here.
    public sealed class UnitModePlan
    {
        public string Unit { get; }
        public ReadOnlyCollection<UnitModeTarget> Targets { get; }
        public ReadOnlyCollection<GeneratedAssignment> Off { get; }
        public ReadOnlyCollection<GeneratedAssignment> Service { get; }
        readonly XNamespace sourceNamespace;
        readonly string culture;
        static GeneratedExpression Local(params string[] path) { return GeneratedExpression.Reference(new SymbolReference(false, path), "Bool"); }
        public UnitModePlan(XmlUnit unit)
        {
            Unit = unit.Name; culture = unit.GetCommentCulture();
            var networks = unit.Xml.Descendants("SW.Blocks.CompileUnit").ToArray();
            sourceNamespace = unit.Networks.Values.FirstOrDefault()?.SourceNamespace ??
                (XNamespace)"http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v4";
            var targets = new List<UnitModeTarget>();
            foreach (var device in unit.Devices.Values.OrderBy(d => Array.FindIndex(networks, n => n == unit.FindNetwork(d.TagName)?.Xml)))
            {
                var call = device.ConfiguredDeviceCalls().First();
                if (call.InstName != device.TagName) throw new InvalidOperationException("Main mode instance ownership differs: " + Unit + "/" + device.TagName);
                string cfg;
                switch (call.BlockName)
                {
                    case "fbVlv2Out": cfg = "typeValveCfg"; break;
                    case "fbVlvCtrl": cfg = "typeValveSpCfg"; break;
                    case "fbMotS": cfg = "typeMotorCfg"; break;
                    case "fbMotAn": cfg = "typeMotorSpCfg"; break;
                    case "fbPIDCtrl": cfg = "typePIDCfg"; break;
                    case "fbMonAn": case "fbMonDi": continue;
                    default: throw new InvalidOperationException("Unsupported configured mode device: " + Unit + "/" + device.TagName + "/" + call.BlockName);
                }
                Xml.Network.XmlParameter parameter;
                if (!call.Parameters.TryGetValue("CFG", out parameter) || parameter.Section != "Input" || parameter.Type.Trim('"') != cfg)
                    throw new InvalidOperationException("Mode CFG contract differs: " + Unit + "/" + call.InstName + ", expected Input " + cfg);
                targets.Add(new UnitModeTarget(call.InstName, call.BlockName, cfg, call.BlockName != "fbPIDCtrl"));
            }
            if (targets.Select(t => t.Instance).Distinct(StringComparer.OrdinalIgnoreCase).Count() != targets.Count)
                throw new InvalidOperationException("Duplicate mode inventory in " + Unit);
            Targets = targets.AsReadOnly();
            Off = targets.Select(t => new GeneratedAssignment(Local(t.Instance, "OFF"), Local("iMode_Off"))).ToList().AsReadOnly();
            Service = targets.Where(t => t.Service).SelectMany(t => new[] {
                new GeneratedAssignment(Local("CFG", t.Instance, "FEATURE", "IsAutoModeDefault"), GeneratedExpression.Boolean(GeneratedExpressionKind.Not, Local("iMode_Service"))),
                new GeneratedAssignment(Local("CFG", t.Instance, "FEATURE", "IsInterlockOff"), Local("iMode_Service")) }).ToList().AsReadOnly();
        }
        public GeneratedBlockBody Apply(GeneratedBlockBody original)
        {
            if (original.Networks.Any(n => n.Body.Title == "MODE Off" || n.Body.Title == "MODE Service"))
                throw new InvalidOperationException("Conflicting MODE network in " + Unit + "; preserve and reconcile ownership, never sweep by title.");
            var networks = original.Networks.ToList();
            int lastDevice = networks.FindLastIndex(n => n.Category.HasValue);
            int operation = networks.FindIndex(n => n.Role == GeneratedNetworkRole.OperationCall);
            if (lastDevice >= 0 && operation >= 0 && operation < lastDevice)
                throw new InvalidOperationException("Unit " + Unit + " has Operations before Devices; reconcile ordering without moving existing calls.");
            int insert = lastDevice >= 0 ? lastDevice + 1 : operation >= 0 ? operation : networks.Count;
            var modes = new List<GeneratedLogicalNetwork>();
            if (Off.Count != 0) modes.Add(Network("MODE Off", "Setting actuators to Off mode", Off, GeneratedNetworkRole.ModeOff));
            if (Service.Count != 0) modes.Add(Network("MODE Service", "Setting actuators to Service mode", Service, GeneratedNetworkRole.ModeService));
            networks.InsertRange(insert, modes);
            return new GeneratedBlockBody(networks);
        }
        GeneratedLogicalNetwork Network(string title, string comment, IEnumerable<GeneratedAssignment> assignments, GeneratedNetworkRole role)
        { return new GeneratedLogicalNetwork(GeneratedNetworkModel.FromAssignments(Unit + "/" + title, title, comment, assignments, sourceNamespace, culture), role: role); }

        public void ValidateDefinitions(Func<string, XElement> block, Func<string, XElement> type)
        {
            foreach (var target in Targets)
            {
                string context = Unit + "/" + target.Instance;
                var definition = block(target.BlockType);
                var sections = definition?.Descendants().FirstOrDefault(e => e.Name.LocalName == "Sections");
                var members = sections?.Elements().Where(s => (string)s.Attribute("Name") == "InOut").SelectMany(s => s.Elements())
                    .Where(m => (string)m.Attribute("Name") == "OFF").ToArray();
                if (members == null || members.Length != 1 || (string)members[0].Attribute("Datatype") != "Bool")
                    throw new InvalidOperationException("Mode target requires writable InOut Bool " + context + ".OFF in " + target.BlockType);
                if (target.Service)
                    foreach (string field in new[] { "IsAutoModeDefault", "IsInterlockOff" })
                        RequireBoolPath(type(target.ConfigurationType), new[] { "FEATURE", field }, type, context + "/CFG.FEATURE." + field);
            }
        }
        static void RequireBoolPath(XElement document, string[] path, Func<string, XElement> type, string context)
        {
            var sections = document?.Descendants().FirstOrDefault(e => e.Name.LocalName == "Sections");
            var fields = StructFields(sections);
            foreach (string component in path)
            {
                var members = fields.Where(m => (string)m.Attribute("Name") == component).ToArray();
                if (members.Length != 1) throw new InvalidOperationException("Missing/ambiguous writable mode path: " + context);
                var member = members[0];
                if (component == path.Last())
                {
                    if ((string)member.Attribute("Datatype") != "Bool") throw new InvalidOperationException("Expected writable Bool at mode path: " + context);
                    return;
                }
                // Openness exports inline Struct fields as direct Member children.
                // Expanded/configuration fixtures may instead wrap them in Sections.
                // Accept either exact child layout; never search arbitrary descendants.
                var direct = member.Elements().Where(e => e.Name.LocalName == "Member").ToArray();
                var wrapped = member.Elements().Where(e => e.Name.LocalName == "Sections").ToArray();
                if (wrapped.Length > 1 || (direct.Length != 0 && wrapped.Length != 0))
                    throw new InvalidOperationException("Ambiguous writable mode structure: " + context);
                if (direct.Length != 0) fields = direct;
                else if (wrapped.Length == 1) fields = StructFields(wrapped[0]);
                else
                {
                    string name = ((string)member.Attribute("Datatype"))?.Trim('"');
                    if (string.IsNullOrWhiteSpace(name) || name == "Struct") throw new InvalidOperationException("Missing writable mode structure: " + context);
                    fields = StructFields(type(name)?.Descendants().FirstOrDefault(e => e.Name.LocalName == "Sections"));
                }
            }
        }
        static XElement[] StructFields(XElement sections)
        {
            return sections == null ? new XElement[0] : sections.Elements().Where(s => s.Name.LocalName == "Section" && (string)s.Attribute("Name") == "None")
                .SelectMany(s => s.Elements()).Where(m => m.Name.LocalName == "Member").ToArray();
        }
    }
}
