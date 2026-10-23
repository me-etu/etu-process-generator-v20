using System;
using System.IO;

namespace TIA_LIB.Generation
{
    public enum GenerationLanguage { Fbd, Scl }

    public sealed class GenerationConfiguration
    {
        GenerationLanguage language;
        bool frozen, generalSelected, legacySelected;
        GenerationLanguage legacyLanguage;
        string migrationApprovalPath;
        public string MigrationApprovalPath
        {
            get { return migrationApprovalPath; }
            set
            {
                if (frozen && value != migrationApprovalPath) throw new InvalidOperationException("Select the configuration migration approval before constructing the plant model.");
                migrationApprovalPath = value;
            }
        }
        public static string MigrationApprovalArgument(string[] args)
        {
            if (args == null || args.Length == 0) return null;
            if (args.Length != 2 || args[0] != "--configuration-migration-approval" || string.IsNullOrWhiteSpace(args[1]))
                throw new ArgumentException("Usage: --configuration-migration-approval <reviewed-approval.xml>");
            return Path.GetFullPath(args[1]);
        }
        public GenerationLanguage Language
        {
            get { return language; }
            set
            {
                Validate(value);
                if (legacySelected && legacyLanguage != value) throw new InvalidOperationException("The general language conflicts with the compatibility operation-interlock language. Select one consistent language before construction.");
                Set(value); generalSelected = true;
            }
        }
        // Compatibility only: the old API now means the same language for generated
        // executable bodies. It is not a second per-block backend switch.
        public OperationInterlockLanguage CompatibilityInterlockLanguage
        {
            get { return language == GenerationLanguage.Scl ? OperationInterlockLanguage.Scl : OperationInterlockLanguage.Fbd; }
            set
            {
                if (!Enum.IsDefined(typeof(OperationInterlockLanguage), value)) throw new ArgumentOutOfRangeException(nameof(value));
                var selected = value == OperationInterlockLanguage.Scl ? GenerationLanguage.Scl : GenerationLanguage.Fbd;
                if (generalSelected && selected != language) throw new InvalidOperationException("The compatibility operation-interlock language conflicts with the selected general language.");
                Set(selected); legacySelected = true; legacyLanguage = selected;
            }
        }
        static void Validate(GenerationLanguage value)
        { if (!Enum.IsDefined(typeof(GenerationLanguage), value)) throw new ArgumentOutOfRangeException(nameof(value)); }
        void Set(GenerationLanguage value)
        {
            if (frozen && language != value) throw new InvalidOperationException("Select the language for code generation before constructing the plant model.");
            language = value;
        }
        public void Freeze() { frozen = true; }
        public static GenerationLanguage Select(TextReader input, TextWriter output)
        {
            if (input == null || output == null) throw new ArgumentNullException("Language selection requires input and output.");
            while (true)
            {
                output.WriteLine("Select language for code generation:");
                output.WriteLine("1. FBD (default)"); output.WriteLine("2. SCL"); output.Write("Choice [1]: ");
                string choice = input.ReadLine();
                if (string.IsNullOrWhiteSpace(choice) || choice.Trim() == "1") return GenerationLanguage.Fbd;
                if (choice.Trim() == "2") return GenerationLanguage.Scl;
                output.WriteLine("Invalid choice. Enter 1 or 2.");
            }
        }
    }
}
