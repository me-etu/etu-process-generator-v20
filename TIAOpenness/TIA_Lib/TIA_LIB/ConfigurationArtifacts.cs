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
        public string UdtName { get; }
        public XElement Udt { get { return udt == null ? null : new XElement(udt); } }
        public XElement DatablockMember { get { return member == null ? null : new XElement(member); } }
        public ReadOnlyCollection<string> RequiredTypes { get; }

        public ConfigurationArtifacts(UnitConfigurationPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            UdtName = plan.UdtName;
            RequiredTypes = plan.Leaves.Select(l => l.Datatype.Trim('"')).Distinct(StringComparer.OrdinalIgnoreCase).ToList().AsReadOnly();
            if (!plan.HasConfiguration) return;
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
            if (clone.Elements().Any(e => e.Name.LocalName == "Member" &&
                string.Equals((string)e.Attribute("Name"), name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Configuration member '" + name + "' already exists; reconcile the owned UDT and instance overrides before applying the proposal.");
            clone.Add(new XElement(member));
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
            unit.GetInterfaceMember("Input", "CFG", "\"" + plan.UdtName + "\"");
            foreach (var call in calls)
                call.Network.ConfigureSymbolInput(call, "CFG", call.Parameters["CFG"].Type, new SignalReference("CFG|" + call.InstName, false), true);
            plantNetwork.ConfigureSymbolInput(unitCall, "CFG", "\"" + plan.UdtName + "\"",
                new SignalReference("cfgData|" + plan.SafeUnit, true), true);
        }
    }
}
