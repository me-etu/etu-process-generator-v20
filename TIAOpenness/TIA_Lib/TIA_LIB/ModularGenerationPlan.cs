using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml.Linq;
using TIA_LIB.SignalStaging;

namespace TIA_LIB.Generation
{
    // Folder/ownership policy is pure; Portal lookups and imports stay in runtime.
    public static class UnitTypeFolderPolicy
    {
        public static string[] Target(string typeName, IEnumerable<string> units)
        {
            var names = units.Select(u => new UnitConfigurationPlan(u, new ConfigurationLeaf[0])).ToArray();
            UnitConfigurationPlan.ValidateUnitNames(names);
            var owner = names.SingleOrDefault(u => typeName == "hwIN_" + u.SafeUnit ||
                typeName == "hwOUT_" + u.SafeUnit || typeName == u.UdtName);
            if (owner == null) throw new InvalidOperationException("No completed Unit owns generated type " + typeName + ".");
            return new[] { owner.SafeUnit };
        }
        public static XElement Capture(string owner, string name, string[] folder, XElement exported)
        {
            return new XElement("EtuOwnedType", new XAttribute("Version", "2"), new XAttribute("Owner", owner),
                new XAttribute("Name", name), new XAttribute("ExportHash", ManagedArtifactOwnership.Fingerprint(exported)),
                new XElement("Folder", folder.Select(p => new XElement("Group", p))));
        }
        public static void Validate(XElement record, string owner, string name, string[] currentFolder, string[] targetFolder, XElement exported)
        {
            if (record == null || record.Name != "EtuOwnedType" || (string)record.Attribute("Owner") != owner || (string)record.Attribute("Name") != name)
                throw new InvalidOperationException("Existing UDT '" + name + "' has no verified project/PLC ownership; reconcile explicitly.");
            if ((string)record.Attribute("Version") == "1" && currentFolder.Length == 0)
                throw new InvalidOperationException("Existing root UDT '" + name + "' requires folder reconciliation. The installed PlcType API exposes no verified identity-preserving move operation; automatic delete/recreate is disabled. The prior v1 ledger has no content fingerprint. Review type-folder-plan.xml and backups; fresh Unit types support nested creation.");
            if ((string)record.Attribute("Version") != "2" || !currentFolder.SequenceEqual(targetFolder) ||
                !record.Elements("Folder").SelectMany(e => e.Elements("Group")).Select(e => e.Value).SequenceEqual(currentFolder))
                throw new InvalidOperationException("Existing UDT '" + name + "' moved or has an incompatible ownership folder. Preserve and reconcile explicitly.");
            if ((string)record.Attribute("ExportHash") != ManagedArtifactOwnership.Fingerprint(exported))
                throw new InvalidOperationException("Existing UDT '" + name + "' changed after generation; preserve its manual members/comments/values and reconcile explicitly.");
        }
    }
    // Planning foundation for G-017. Not selected by the live generator until both
    // renderers, library-contract checks and migration preflight are implemented.
    public enum DeviceCategory
    {
        Digital_Sensors, Analog_Sensors, Digital_Valves, Analog_Valves,
        Motors_Direct, Motors_Control, PID_Controllers
    }

    public sealed class SymbolReference
    {
        public bool IsGlobal { get; }
        public ReadOnlyCollection<string> Components { get; }
        public SymbolReference(bool isGlobal, params string[] components)
        {
            if (components == null || components.Length == 0 || components.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("A symbolic reference requires explicit nonempty components.");
            IsGlobal = isGlobal;
            Components = Array.AsReadOnly((string[])components.Clone());
        }
    }

    public sealed class ControlBinding
    {
        public string Input { get; }
        public string Datatype { get { return "Bool"; } }
        public SymbolReference Source { get; }
        public ControlBinding(string input, SymbolReference source) { Input = input; Source = source; }
    }

    public static class ModularControlContract
    {
        public static ReadOnlyCollection<ControlBinding> PlantToUnit()
        {
            return Array.AsReadOnly(new[] {
                new ControlBinding("iReset", new SymbolReference(true, "HMI", "RESET")),
                new ControlBinding("iReset_Delayed", new SymbolReference(true, "HMI", "RESET_DELAYED")),
                new ControlBinding("iMode_Off", new SymbolReference(true, "HMI", "MODE_OFF")),
                new ControlBinding("iMode_Service", new SymbolReference(true, "HMI", "MODE_SERV")),
                new ControlBinding("iHSS", new SymbolReference(true, "F_Safe", "HSS"))
            });
        }
        public static ReadOnlyCollection<ControlBinding> UnitToOperation()
        {
            return Array.AsReadOnly(new[] {
                new ControlBinding("HSS", new SymbolReference(false, "iHSS")),
                new ControlBinding("Delayed_Reset", new SymbolReference(false, "iReset_Delayed"))
            });
        }
    }

    public sealed class ConfigurationLeaf
    {
        readonly XElement member;
        public string Instance { get; }
        public string Datatype { get { return (string)member.Attribute("Datatype"); } }
        public SymbolReference OldPath { get; }
        public SymbolReference NewPath { get; }
        public SymbolReference LocalPath { get { return new SymbolReference(false, "CFG", Instance); } }
        public XElement Member { get { return new XElement(member); } }
        public ConfigurationLeaf(string unit, string instance, XElement existingMember)
        {
            if (string.IsNullOrWhiteSpace(instance) || existingMember == null || string.IsNullOrWhiteSpace((string)existingMember.Attribute("Datatype")))
                throw new ArgumentException("Configuration requires an exact instance and typed member.");
            Instance = instance;
            // Ownership comes from declarations, never by splitting an old symbol on '_'.
            OldPath = new SymbolReference(true, "cfgData", unit + "_" + instance);
            NewPath = new SymbolReference(true, "cfgData", SignalStagingInventory.SafeName(unit), instance);
            member = new XElement(existingMember);
            member.SetAttributeValue("Name", instance);
        }
    }

    public sealed class UnitConfigurationPlan
    {
        public string Unit { get; }
        public string SafeUnit { get; }
        public string UdtName { get { return "udtCfg_" + SafeUnit; } }
        public bool HasConfiguration { get { return Leaves.Count != 0; } }
        public ReadOnlyCollection<ConfigurationLeaf> Leaves { get; }
        public UnitConfigurationPlan(string unit, IEnumerable<ConfigurationLeaf> leaves)
        {
            if (string.IsNullOrWhiteSpace(unit)) throw new ArgumentException("Unit identity is required.");
            Unit = unit;
            SafeUnit = SignalStagingInventory.SafeName(unit);
            var ordered = new List<ConfigurationLeaf>();
            var byInstance = new Dictionary<string, ConfigurationLeaf>(StringComparer.OrdinalIgnoreCase);
            foreach (var leaf in leaves)
            {
                if (leaf.OldPath.Components[1] != unit + "_" + leaf.Instance)
                    throw new InvalidOperationException("Configuration ownership differs for " + unit + "/" + leaf.Instance + ".");
                ConfigurationLeaf previous;
                if (byInstance.TryGetValue(leaf.Instance, out previous))
                {
                    if (previous.Instance != leaf.Instance || !XNode.DeepEquals(previous.Member, leaf.Member))
                        throw new InvalidOperationException("Configuration collision in " + unit + ": " + leaf.Instance + ".");
                    continue;
                }
                byInstance.Add(leaf.Instance, leaf);
                ordered.Add(leaf);
            }
            Leaves = ordered.AsReadOnly();
        }
        public static void ValidateUnitNames(IEnumerable<UnitConfigurationPlan> units)
        {
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var unit in units)
            {
                string previous;
                if (names.TryGetValue(unit.SafeUnit, out previous))
                    throw new InvalidOperationException("Unit UDT/member collision: " + previous + " and " + unit.Unit + " -> " + unit.SafeUnit + ".");
                names.Add(unit.SafeUnit, unit.Unit);
            }
        }
    }

    public sealed class ScheduledNetwork
    {
        public string Identity { get; }
        public string Title { get; }
        public DeviceCategory Category { get; }
        public ReadOnlyCollection<string> Reads { get; }
        public ReadOnlyCollection<string> Writes { get; }
        public ScheduledNetwork(string identity, string title, DeviceCategory category, IEnumerable<string> reads, IEnumerable<string> writes)
        {
            if (string.IsNullOrWhiteSpace(identity)) throw new ArgumentException("Stable network identity is required.");
            if (string.IsNullOrWhiteSpace(title) || title.Any(c => char.IsControl(c)) || title.Contains("//") || title.Contains("(*") || title.Contains("*)"))
                throw new ArgumentException("Invalid region title for " + identity + ".");
            if (!Enum.IsDefined(typeof(DeviceCategory), category)) throw new ArgumentOutOfRangeException(nameof(category));
            Identity = identity;
            Title = title;
            Category = category;
            Reads = Array.AsReadOnly(reads.ToArray());
            Writes = Array.AsReadOnly(writes.ToArray());
        }
    }

    public static class DeviceSchedule
    {
        public static ReadOnlyCollection<ScheduledNetwork> Group(IEnumerable<ScheduledNetwork> networks)
        {
            var original = networks.ToArray();
            if (original.Select(n => n.Identity).Distinct(StringComparer.Ordinal).Count() != original.Length)
                throw new InvalidOperationException("Repeated stable network identity.");
            var grouped = original.OrderBy(n => n.Category).ToArray(); // Stable within category.
            ValidateOrder(original, grouped.Select(n => n.Identity));
            return Array.AsReadOnly(grouped);
        }
        public static void ValidateOrder(IEnumerable<ScheduledNetwork> networks, IEnumerable<string> proposedIdentities)
        {
            var original = networks.ToArray();
            var order = proposedIdentities.ToArray();
            if (original.Select(n => n.Identity).Distinct().Count() != original.Length || order.Distinct().Count() != order.Length ||
                !new HashSet<string>(original.Select(n => n.Identity)).SetEquals(order))
                throw new InvalidOperationException("Device schedule is not a complete identity-preserving permutation.");
            var positions = order.Select((id, i) => new { Identity = id, Index = i }).ToDictionary(n => n.Identity, n => n.Index);
            for (int first = 0; first < original.Length; first++)
            for (int second = first + 1; second < original.Length; second++)
            {
                var a = original[first];
                var b = original[second];
                bool hazard = Overlap(a.Writes, b.Reads.Concat(b.Writes)) || Overlap(a.Reads, b.Writes);
                if (hazard && positions[a.Identity] > positions[b.Identity])
                    throw new InvalidOperationException("Device grouping changes scan dependencies: '" + a.Title + "' (" + a.Identity +
                        ") must remain before '" + b.Title + "' (" + b.Identity + "). Reconcile the schedule explicitly.");
            }
        }

        static bool Overlap(IEnumerable<string> first, IEnumerable<string> second)
        {
            // An FB invocation writes its instance state, including all output fields.
            // A field read therefore conflicts with a write to its enclosing instance.
            return first.Any(a => second.Any(b => string.Equals(a, b, StringComparison.OrdinalIgnoreCase) ||
                a.StartsWith(b + ".", StringComparison.OrdinalIgnoreCase) || b.StartsWith(a + ".", StringComparison.OrdinalIgnoreCase)));
        }
    }

    public sealed class ArtifactDependency
    {
        public string Identity { get; }
        public ReadOnlyCollection<string> Requires { get; }
        public ArtifactDependency(string identity, params string[] requires)
        {
            Identity = identity;
            Requires = Array.AsReadOnly((string[])requires.Clone());
        }
    }

    public static class ArtifactSchedule
    {
        public static ReadOnlyCollection<ArtifactDependency> Order(IEnumerable<ArtifactDependency> artifacts)
        {
            var remaining = artifacts.ToList();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var artifact in remaining)
                if (string.IsNullOrWhiteSpace(artifact.Identity) || !ids.Add(artifact.Identity))
                    throw new InvalidOperationException("Missing or colliding artifact identity: " + artifact.Identity + ".");
            foreach (var artifact in remaining)
            foreach (var required in artifact.Requires)
                if (!ids.Contains(required)) throw new InvalidOperationException("Missing dependency " + required + " required by " + artifact.Identity + ".");
            var ready = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<ArtifactDependency>();
            while (remaining.Count != 0)
            {
                var next = remaining.FirstOrDefault(a => a.Requires.All(ready.Contains));
                if (next == null) throw new InvalidOperationException("Artifact dependency cycle: " + string.Join(", ", remaining.Select(a => a.Identity)) + ".");
                result.Add(next);
                ready.Add(next.Identity);
                remaining.Remove(next);
            }
            return result.AsReadOnly();
        }
    }
}
