using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TIA_LIB
{
    public enum OperationInterlockLanguage { Fbd, Scl }

    // Independent of signal staging. A model cannot change language after construction starts.
    public sealed class OperationInterlockConfiguration
    {
        private OperationInterlockLanguage language;
        private bool frozen;
        public OperationInterlockLanguage Language
        {
            get { return language; }
            set
            {
                if (!Enum.IsDefined(typeof(OperationInterlockLanguage), value))
                    throw new ArgumentOutOfRangeException(nameof(value));
                if (frozen && value != language)
                    throw new InvalidOperationException("Select the operation interlock language before constructing the plant model.");
                language = value;
            }
        }
        public void Freeze() { frozen = true; }

        public static OperationInterlockLanguage Select(TextReader input, TextWriter output)
        {
            // Compatibility selector. PlcProject.InterlockLanguage now selects the
            // general backend; the old type must not advertise interlock-only scope.
            return Generation.GenerationConfiguration.Select(input, output) == Generation.GenerationLanguage.Scl
                ? OperationInterlockLanguage.Scl : OperationInterlockLanguage.Fbd;
        }
    }

    // Pure, immutable upload-time inventory. No Siemens objects or template file dependency.
    public sealed class OperationInterlockDescriptor
    {
        public string Unit { get; }
        public string Operation { get; }
        public string BlockName { get { return Operation + "_Interlocks"; } }
        public string Identity { get { return Unit + "/" + Operation; } }
        public string ArtifactStem
        {
            get
            {
                using (var hash = System.Security.Cryptography.SHA256.Create())
                    return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Identity))).Replace("-", "") + "_" + BlockName;
            }
        }
        public ReadOnlyCollection<string> Phases { get; }
        public ReadOnlyCollection<string> BoolMembers { get; }

        public OperationInterlockDescriptor(string unit, string operation, IEnumerable<string> phases, bool validateIdentifiers = true)
        {
            Unit = unit;
            Operation = operation;
            if (validateIdentifiers) ValidateIdentifier(BlockName, "operation " + Identity);
            var ordered = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var phase in phases)
            {
                if (validateIdentifiers)
                {
                    ValidateIdentifier(phase, "AddPH(\"" + unit + "\", \"" + operation + "\", \"" + phase + "\")");
                    ValidateIdentifier(phase + "_PRECOND_50", "phase member in " + Identity + "/" + phase);
                }
                if (seen.Add(phase)) ordered.Add(phase);
            }
            Phases = ordered.AsReadOnly();
            var members = new List<string>();
            if (ordered.Count != 0)
            {
                members.AddRange(Preconditions(""));
                foreach (string phase in ordered) members.AddRange(Preconditions(phase + "_"));
            }
            BoolMembers = members.AsReadOnly();
        }

        private static void ValidateIdentifier(string name, string origin)
        {
            // Keep identities intact. Hyphens require quoted source identifiers, not renaming.
            // The bounded ASCII subset excludes quotes, control characters and source injection.
            if (name == null || name.Length > 128 || !Regex.IsMatch(name, @"\A[A-Za-z_][A-Za-z0-9_-]*\z"))
                throw new InvalidOperationException("Unsupported SCL identifier '" + name + "' from " + origin + ". Use ASCII letters, digits, underscores and hyphens (maximum 128 characters, starting with a letter or underscore).");
        }

        private static string SourceIdentifier(string name)
        {
            return name.IndexOf('-') >= 0 ? "\"" + name + "\"" : name;
        }

        private static IEnumerable<string> Preconditions(string prefix)
        {
            return Enumerable.Range(1, 50).Select(i => prefix + "PRECOND_" + i.ToString("D2", CultureInfo.InvariantCulture));
        }

        public string RenderScl()
        {
            if (Phases.Count == 0) return ""; // AddOP alone must not introduce an interlock.
            var text = new StringBuilder();
            Action<string> line = s => text.Append(s).Append('\n');
            line("FUNCTION_BLOCK \"" + BlockName + "\"");
            line("{ S7_Optimized_Access := 'TRUE' }");
            line("VERSION : 0.1");
            line("   VAR");
            line("      State {InstructionName := 'Program_Alarm'; LibVersion := '1.0'} : Program_Alarm;");
            foreach (string member in BoolMembers) line("      " + SourceIdentifier(member) + " : Bool;");
            line("   END_VAR");
            line("");
            line("BEGIN");
            line("   REGION Function");
            line("      // here all interlocks for this operation will be defined");
            line("      // Later FALSE assignments execute every scan. Adjust them when adding calculated interlocks.");
            line("   END_REGION");
            line("");
            line("   REGION General Interlocks");
            foreach (string member in Preconditions("")) line("      #" + SourceIdentifier(member) + " := FALSE;");
            line("   END_REGION");
            foreach (string phase in Phases)
            {
                line("");
                line("   REGION " + phase + " Interlocks");
                foreach (string member in Preconditions(phase + "_")) line("      #" + SourceIdentifier(member) + " := FALSE;");
                line("   END_REGION");
            }
            line("END_FUNCTION_BLOCK");
            return text.ToString();
        }

        public void ValidateExisting(XElement document, OperationInterlockLanguage language)
        {
            var block = document.Elements("SW.Blocks.FB").SingleOrDefault();
            var attributes = block == null ? null : block.Element("AttributeList");
            string expected = language == OperationInterlockLanguage.Scl ? "SCL" : "FBD";
            if (attributes == null || (string)attributes.Element("Name") != BlockName ||
                (string)attributes.Element("ProgrammingLanguage") != expected)
                Incompatible("expected an FB named " + BlockName + " in " + expected);
            if (language == OperationInterlockLanguage.Fbd) return; // Retain legacy additive FBD interface handling.
            if ((string)attributes.Element("MemoryLayout") != "Optimized") Incompatible("expected optimized access");
            var sections = attributes.Element("Interface")?.Elements().FirstOrDefault(e => e.Name.LocalName == "Sections");
            if (sections == null) Incompatible("missing interface");
            foreach (string direction in new[] { "Input", "Output", "InOut" })
            {
                var section = sections.Elements().SingleOrDefault(e => (string)e.Attribute("Name") == direction);
                if (section == null || section.Elements().Any()) Incompatible("expected empty " + direction);
            }
            var statics = sections.Elements().SingleOrDefault(e => (string)e.Attribute("Name") == "Static");
            if (statics == null) Incompatible("missing Static section");
            foreach (string name in new[] { "State" }.Concat(BoolMembers))
            {
                var matches = statics.Elements().Where(e => string.Equals((string)e.Attribute("Name"), name, StringComparison.OrdinalIgnoreCase)).ToList();
                string type = name == "State" ? "Program_Alarm" : "Bool";
                if (matches.Count != 1 || (string)matches[0].Attribute("Datatype") != type ||
                    (string)matches[0].Attribute("Accessibility") != "Public") Incompatible("missing/incompatible Static " + name + " : " + type);
                if (name == "State" && (string)matches[0].Attribute("Version") != "1.0") Incompatible("State requires Program_Alarm version 1.0");
                if (name == "State")
                {
                    foreach (string flag in new[] { "ExternalAccessible", "ExternalVisible", "ExternalWritable", "SetPoint" })
                    {
                        var value = matches[0].Elements().Where(e => e.Name.LocalName == "AttributeList")
                            .Elements().SingleOrDefault(e => (string)e.Attribute("Name") == flag);
                        if (value == null || !string.Equals(value.Value, "true", StringComparison.OrdinalIgnoreCase))
                            Incompatible("State requires " + flag + "=true, as in the FBD interface");
                    }
                }
            }
        }

        private void Incompatible(string reason)
        {
            throw new InvalidOperationException("Operation interlock " + Identity + "/" + BlockName + ": " + reason + ". No conversion or source/interface merge is performed. Reconcile the existing block explicitly; it has not been replaced.");
        }
    }

    public sealed class OperationInterlockExisting
    {
        public string Path { get; set; }
        public bool InTargetFolder { get; set; }
        public XElement Document { get; set; }
    }

    public interface IOperationInterlockBackend
    {
        IEnumerable<OperationInterlockExisting> Find(OperationInterlockDescriptor descriptor);
        void Generate(OperationInterlockDescriptor descriptor);
    }

    // All inspections finish before any generation. Completion is recorded only after success.
    public sealed class OperationInterlockBatch
    {
        private readonly IOperationInterlockBackend backend;
        private readonly HashSet<OperationInterlockDescriptor> pending = new HashSet<OperationInterlockDescriptor>();
        private bool failed;
        public OperationInterlockBatch(IEnumerable<OperationInterlockDescriptor> descriptors, IOperationInterlockBackend backend,
            OperationInterlockLanguage language)
        {
            this.backend = backend;
            var requests = descriptors.Where(d => d.Phases.Count != 0).ToList();
            foreach (var group in requests.GroupBy(d => d.BlockName, StringComparer.OrdinalIgnoreCase))
                if (group.Count() > 1) throw new InvalidOperationException("CPU-global operation interlock name collision '" + group.Key + "': " + string.Join(", ", group.Select(d => d.Identity)));
            foreach (var descriptor in requests)
            {
                var existing = backend.Find(descriptor).ToList();
                if (existing.Count > 1 || existing.Any(b => !b.InTargetFolder))
                    throw new InvalidOperationException("Operation interlock " + descriptor.Identity + "/" + descriptor.BlockName + " conflicts with CPU block(s): " + string.Join(", ", existing.Select(b => b.Path)) + ". Reconcile explicitly; blocks will not be moved or replaced.");
                if (existing.Count == 1) descriptor.ValidateExisting(existing[0].Document, language);
                else pending.Add(descriptor);
            }
        }

        public void Upload(OperationInterlockDescriptor descriptor)
        {
            if (failed) throw new InvalidOperationException("Operation interlock generation previously failed. Stop this run and reconcile the retained source diagnostics before retrying.");
            if (!pending.Contains(descriptor)) return;
            try { backend.Generate(descriptor); pending.Remove(descriptor); }
            catch { failed = true; throw; }
        }
    }
}
