using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml.Linq;
using TIA_LIB.SignalStaging;
using TIA_LIB.Xml;

namespace TIA_LIB.Generation
{
    // Pure serialization: no Portal, file writes, registration or migration application.
    public sealed class ConfigurationArtifacts
    {
        static readonly XNamespace Interface = "http://www.siemens.com/automation/Openness/SW/Interface/v5";
        readonly XElement udt;
        readonly XElement member;
        readonly XElement originalMember;
        public string UdtName { get; }
        public XElement Udt { get { return udt == null ? null : new XElement(udt); } }
        public XElement DatablockMember { get { return member == null ? null : new XElement(member); } }
        public ReadOnlyCollection<string> RequiredTypes { get; }

        public ConfigurationArtifacts(UnitConfigurationPlan plan) : this(plan, null, null) { }

        // The caller must establish managed ownership before using existing documents.
        // This method proposes an additive update; it never adopts or imports live types.
        public ConfigurationArtifacts(UnitConfigurationPlan plan, XElement ownedType, XElement existingMember)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if ((ownedType == null) != (existingMember == null)) throw new ArgumentException("Existing configuration requires both the owned UDT and its DB member snapshot.");
            UdtName = plan.UdtName;
            RequiredTypes = plan.Leaves.Select(l => l.Datatype.Trim('"')).Distinct(StringComparer.OrdinalIgnoreCase).ToList().AsReadOnly();
            if (!plan.HasConfiguration)
            {
                if (ownedType != null) throw new InvalidOperationException("Existing Unit configuration cannot be removed by an empty declaration inventory.");
                return;
            }
            // Keep declaration order and all per-leaf metadata. No XmlDataMember flattening.
            var leaves = plan.Leaves.Select(l => Qualify(l.Member)).ToArray();
            var attributes = new XElement("AttributeList",
                new XElement("Interface", new XElement(Interface + "Sections",
                    new XElement(Interface + "Section", new XAttribute("Name", "None"), leaves))),
                new XElement("Name", plan.UdtName));
#if TIA_V21
            const string version = "V21";
            attributes.Add(new XElement("Namespace"));
#else
            const string version = "V17";
#endif
            udt = new XElement("Document", new XElement("Engineering", new XAttribute("version", version)),
                new XElement("SW.Types.PlcStruct", new XAttribute("ID", "0"), attributes));
            // Expanded instance overrides carry the offline values and individual remanence.
            // The enclosing member does not force all leaves to the same retention policy.
            member = new XElement(Interface + "Member", new XAttribute("Name", plan.SafeUnit),
                new XAttribute("Datatype", "\"" + plan.UdtName + "\""),
                new XElement(Interface + "Sections", new XElement(Interface + "Section", new XAttribute("Name", "None"),
                    plan.Leaves.Select(l => Qualify(l.Member)))));
            if (ownedType != null)
            {
                var structures = ownedType.Elements("SW.Types.PlcStruct").ToArray();
                if (structures.Length != 1 || (string)structures[0].Element("AttributeList")?.Element("Name") != plan.UdtName ||
                    (string)existingMember.Attribute("Name") != plan.SafeUnit || ((string)existingMember.Attribute("Datatype"))?.Trim('"') != plan.UdtName)
                    throw new InvalidOperationException("Existing configuration UDT/member identity differs for " + plan.Unit + ".");
                udt = new XElement(ownedType);
                member = Qualify(existingMember);
                originalMember = new XElement(existingMember);
                var typeSections = udt.Descendants(Interface + "Sections").FirstOrDefault();
                var typeSection = typeSections?.Elements(Interface + "Section").SingleOrDefault(s => (string)s.Attribute("Name") == "None");
                if (typeSection == null) throw new InvalidOperationException("Missing owned configuration UDT fields for " + plan.Unit + ".");
                var memberSections = member.Element(Interface + "Sections");
                if (memberSections == null) { memberSections = new XElement(Interface + "Sections"); member.Add(memberSections); }
                var memberSection = memberSections.Elements(Interface + "Section").SingleOrDefault(s => (string)s.Attribute("Name") == "None");
                if (memberSection == null) { memberSection = new XElement(Interface + "Section", new XAttribute("Name", "None")); memberSections.Add(memberSection); }
                ValidateFields(typeSection); ValidateFields(memberSection);
                foreach (var leaf in plan.Leaves)
                {
                    var previous = typeSection.Elements(Interface + "Member").SingleOrDefault(m => string.Equals((string)m.Attribute("Name"), leaf.Instance, StringComparison.OrdinalIgnoreCase));
                    if (previous != null) RequireCompatible(previous, leaf);
                    else typeSection.Add(Qualify(leaf.Member));
                    var value = memberSection.Elements(Interface + "Member").SingleOrDefault(m => string.Equals((string)m.Attribute("Name"), leaf.Instance, StringComparison.OrdinalIgnoreCase));
                    if (value != null) RequireCompatible(value, leaf);
                    // Existing overrides win. Newly added fields start from the modeled
                    // offline defaults; no existing values are replaced from declarations.
                    else memberSection.Add(Qualify(leaf.Member));
                }
                RequiredTypes = typeSection.Elements(Interface + "Member").Select(m => ((string)m.Attribute("Datatype"))?.Trim('"'))
                    .Where(t => t != null).Distinct(StringComparer.OrdinalIgnoreCase).ToList().AsReadOnly();
            }
        }

        static void ValidateFields(XElement section)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in section.Elements())
                if (field.Name != Interface + "Member" || string.IsNullOrEmpty((string)field.Attribute("Name")) || !names.Add((string)field.Attribute("Name")))
                    throw new InvalidOperationException("Duplicate or unsupported existing configuration field.");
        }
        static void RequireCompatible(XElement member, ConfigurationLeaf leaf)
        {
            if ((string)member.Attribute("Name") != leaf.Instance || ((string)member.Attribute("Datatype"))?.Trim('"') != leaf.Datatype.Trim('"'))
                throw new InvalidOperationException("Existing configuration field spelling/type differs for " + leaf.Instance + ". Reconcile explicitly; values are preserved.");
        }

        static XElement Qualify(XElement original)
        {
            var clone = new XElement(original);
            foreach (var element in clone.DescendantsAndSelf())
            {
                if (element.Name.Namespace != XNamespace.None && element.Name.Namespace != Interface)
                    throw new InvalidOperationException("Unsupported configuration metadata namespace: " + element.Name + ".");
                element.Attributes().Where(a => a.IsNamespaceDeclaration).Remove();
                element.Name = Interface + element.Name.LocalName;
            }
            return clone;
        }

        // Return a proposal, leaving the input/legacy/manual members byte-for-byte intact.
        public XElement ProposeDatablockSection(XElement originalSection)
        {
            if (originalSection == null) throw new ArgumentNullException(nameof(originalSection));
            var clone = new XElement(originalSection);
            if (member == null) return clone;
            string name = (string)member.Attribute("Name");
            var matches = clone.Elements().Where(e => e.Name.LocalName == "Member" && string.Equals((string)e.Attribute("Name"), name, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 0)
            {
                if (matches.Length != 1 || originalMember == null ||
                    (!XNode.DeepEquals(matches[0], originalMember) && !XNode.DeepEquals(matches[0], member)))
                    throw new InvalidOperationException("Configuration member '" + name + "' already exists or changed; reconcile the owned UDT and instance overrides before applying the proposal.");
                matches[0].ReplaceWith(new XElement(member));
            }
            else
            {
                if (originalMember != null) throw new InvalidOperationException("Existing configuration member disappeared after planning: " + name + ".");
                clone.Add(new XElement(member));
            }
            return clone;
        }

        internal static void Bind(XmlPlant plant, XmlUnit unit, UnitConfigurationPlan plan)
        {
            if (unit.Name != plan.Unit) throw new InvalidOperationException("Configuration Unit ownership differs.");
            if (!plan.HasConfiguration) return;
            // Inspect() has already established exact CFG ownership/directions.
            var calls = unit.Devices.Values.SelectMany(d => d.ConfiguredDeviceCalls()).Where(c => c.Parameters.ContainsKey("CFG")).ToArray();
            var names = new HashSet<string>(calls.Select(c => c.InstName), StringComparer.Ordinal);
            if (!names.SetEquals(plan.Leaves.Select(l => l.Instance)))
                throw new InvalidOperationException("Configuration inventory changed after planning for " + plan.Unit + ".");
            var plantNetwork = plant.FindNetwork(unit.Name) ?? plant.FindUnitCallNetwork(unit.Name);
            if (plantNetwork == null) throw new InvalidOperationException("Missing Plant call for configuration " + plan.Unit + ".");
            var unitCall = plantNetwork.Calls.Values.SingleOrDefault(c => c.InstName == unit.Name && c.BlockName == "fb" + unit.Name && c.InstType == "GlobalVariable");
            if (unitCall == null) throw new InvalidOperationException("Missing exact Plant Unit call for configuration " + plan.Unit + ".");
            ModularControlRouting.RequireInput(unit, "CFG", "\"" + plan.UdtName + "\"");
            foreach (var call in calls)
                call.Network.ConfigureSymbolInput(call, "CFG", call.Parameters["CFG"].Type, new SignalReference("CFG|" + call.InstName, false), true);
            plantNetwork.ConfigureSymbolInput(unitCall, "CFG", "\"" + plan.UdtName + "\"",
                new SignalReference("cfgData|" + plan.SafeUnit, true), true);
        }
    }
}
