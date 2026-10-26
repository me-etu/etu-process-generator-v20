using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml.Linq;
using TIA_LIB.Xml;
using TIA_LIB.Xml.Network;

namespace TIA_LIB.Generation
{
    // Read-only inventory. Applying a migration is deliberately a separate operation.
    public sealed class ConfigurationMigrationPlan
    {
        public UnitConfigurationPlan Configuration { get; }
        public ReadOnlyCollection<string> ExistingLegacyMembers { get; }
        public bool RequiresMigration { get { return ExistingLegacyMembers.Count != 0; } }
        public string InterfaceDirection { get { return "InOut"; } }
        public ReadOnlyCollection<string> Preconditions(bool externalConsumersReviewed, bool offlineBackupCaptured, bool onlineValuesAddressed)
        {
            var issues = new List<string>();
            if (RequiresMigration)
            {
                if (!externalConsumersReviewed) issues.Add("External HMI/scripts/PLC consumers of legacy configuration paths have not been completely reviewed.");
                if (!offlineBackupCaptured) issues.Add("An offline configuration backup has not been captured and verified.");
                if (!onlineValuesAddressed) issues.Add("Online actual-value capture/protection has not been explicitly addressed; offline start values are not online actual values.");
            }
            return issues.AsReadOnly();
        }
        ConfigurationMigrationPlan(UnitConfigurationPlan configuration, IEnumerable<string> existing)
        {
            Configuration = configuration;
            ExistingLegacyMembers = existing.ToList().AsReadOnly();
        }
        public static ConfigurationMigrationPlan Inspect(XmlUnit unit, XElement originalCfgDataSection, XElement ownedUnitType = null)
        {
            var leaves = new List<ConfigurationLeaf>();
            var existing = new List<string>();
            string safeUnit = SignalStaging.SignalStagingInventory.SafeName(unit.Name);
            var nested = originalCfgDataSection?.Elements().SingleOrDefault(e => e.Name.LocalName == "Member" &&
                string.Equals((string)e.Attribute("Name"), safeUnit, StringComparison.OrdinalIgnoreCase));
            XElement ownedSection = null;
            if (ownedUnitType != null)
            {
                string expected = "udtCfg_" + safeUnit;
                if (nested == null || (string)nested.Attribute("Name") != safeUnit || ((string)nested.Attribute("Datatype"))?.Trim('"') != expected ||
                    (string)ownedUnitType.Element("SW.Types.PlcStruct")?.Element("AttributeList")?.Element("Name") != expected)
                    throw new InvalidOperationException("Owned configuration type/member identity differs for " + unit.Name + ".");
                ownedSection = ownedUnitType.Descendants().FirstOrDefault(e => e.Name.LocalName == "Sections")?.Elements().SingleOrDefault(e =>
                    e.Name.LocalName == "Section" && (string)e.Attribute("Name") == "None");
                if (ownedSection == null) throw new InvalidOperationException("Missing owned configuration type fields for " + unit.Name + ".");
            }
            // Network XML order is used instead of Dictionary enumeration order.
            var networks = unit.Xml.Descendants("SW.Blocks.CompileUnit").ToArray();
            var devices = unit.Devices.Values.OrderBy(d => Array.FindIndex(networks, n => n == unit.FindNetwork(d.TagName)?.Xml));
            foreach (var device in devices)
            foreach (var call in device.ConfiguredDeviceCalls())
            {
                XmlParameter parameter;
                if (!call.Parameters.TryGetValue("CFG", out parameter)) continue;
                if (parameter.Section != "Input")
                    throw new InvalidOperationException("Configuration direction requires review for " + unit.Name + "/" + call.InstName + ": " + parameter.Section + ".");
                string oldName = unit.Name + "_" + call.InstName;
                var endpoints = call.Network.Xml.Descendants().Where(e => e.Name.LocalName == "NameCon" &&
                    (string)e.Attribute("UId") == call.UId.ToString() && (string)e.Attribute("Name") == "CFG").ToArray();
                if (endpoints.Length != 1) throw new InvalidOperationException("Expected one CFG endpoint for " + unit.Name + "/" + call.InstName + ".");
                var ids = endpoints[0].Parent.Elements().Where(e => e.Name.LocalName == "IdentCon").ToArray();
                XmlAccess access;
                if (ids.Length != 1 || !call.Network.Access.TryGetValue((int)ids[0].Attribute("UId"), out access))
                    throw new InvalidOperationException("Unsupported CFG source for " + unit.Name + "/" + call.InstName + ".");
                var symbol = access as XmlGlobal;
                bool legacy = symbol != null && (string)symbol.Xml.Attribute("Scope") == "GlobalVariable" && symbol.Components.SequenceEqual(new[] { "cfgData", oldName });
                bool local = symbol != null && (string)symbol.Xml.Attribute("Scope") == "LocalVariable" && symbol.Components.SequenceEqual(new[] { "CFG", call.InstName });
                if (!legacy && !local) throw new InvalidOperationException("CFG ownership differs for " + unit.Name + "/" + call.InstName + "; reconcile the manual source explicitly.");

                var memberSource = ownedSection ?? originalCfgDataSection;
                string memberName = ownedSection == null ? oldName : call.InstName;
                var members = memberSource == null ? new XElement[0] : memberSource.Elements().Where(e =>
                    e.Name.LocalName == "Member" && (string)e.Attribute("Name") == memberName).ToArray();
                if (members.Length > 1) throw new InvalidOperationException("Duplicate legacy CFG member " + oldName + ".");
                XElement member;
                if (members.Length == 1)
                {
                    member = members[0];
                    if (((string)member.Attribute("Datatype"))?.Trim('"') != parameter.Type.Trim('"'))
                        throw new InvalidOperationException("Configuration type collision for " + oldName + ".");
                    if (ownedSection == null) existing.Add(oldName);
                }
                else
                {
                    if (local && ownedSection == null) throw new InvalidOperationException("Existing local CFG requires its unit UDT/member snapshot for " + unit.Name + "/" + call.InstName + ".");
                    member = new XElement("Member", new XAttribute("Name", oldName), new XAttribute("Datatype", parameter.Type));
                }
                leaves.Add(new ConfigurationLeaf(unit.Name, call.InstName, member));
            }
            var configuration = new UnitConfigurationPlan(unit.Name, leaves);
            if (originalCfgDataSection != null && configuration.HasConfiguration)
            {
                var collision = originalCfgDataSection.Elements().FirstOrDefault(e => e.Name.LocalName == "Member" &&
                    string.Equals((string)e.Attribute("Name"), configuration.SafeUnit, StringComparison.OrdinalIgnoreCase));
                if (collision != null && ownedSection == null)
                    throw new InvalidOperationException("Existing cfgData unit member '" + configuration.SafeUnit +
                        "' needs explicit UDT/value/manual-field reconciliation before rebuilding configuration.");
            }
            return new ConfigurationMigrationPlan(configuration, existing);
        }
    }
}
