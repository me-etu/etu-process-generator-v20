using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.ExternalSources;
using TIA_LIB.Xml;

namespace TIA_LIB
{
    // The only side-effecting part of SCL generation. Never used by offline fixtures.
    internal sealed class OperationInterlockPortalBackend : IOperationInterlockBackend
    {
        private readonly Dictionary<OperationInterlockDescriptor, XmlInterlock> targets;
        public OperationInterlockPortalBackend(Dictionary<OperationInterlockDescriptor, XmlInterlock> targets)
        {
            this.targets = targets;
        }

        private IEnumerable<OperationInterlockExisting> FindIn(PlcBlockComposition blocks,
            PlcBlockUserGroupComposition groups, string path, PlcBlockUserGroup target, string name)
        {
            foreach (var block in blocks)
            {
                if (!string.Equals(block.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
                // Use a unique export file; never consume or overwrite another run's artifact.
                string file = Path.Combine(Path.GetTempPath(), "G014-" + Guid.NewGuid().ToString("N") + ".xml");
                System.Xml.Linq.XElement document;
                try
                {
                    block.Export(new FileInfo(file), ExportOptions.WithDefaults);
                    document = System.Xml.Linq.XElement.Load(file);
                }
                finally { if (File.Exists(file)) File.Delete(file); }
                yield return new OperationInterlockExisting
                {
                    Path = path + "/" + block.Name,
                    InTargetFolder = Equals(block.Parent, target),
                    Document = document
                };
            }
            foreach (var group in groups)
                foreach (var found in FindIn(group.Blocks, group.Groups, path + "/" + group.Name, target, name))
                    yield return found;
        }

        public IEnumerable<OperationInterlockExisting> Find(OperationInterlockDescriptor descriptor)
        {
            var root = SiemensPortal.Current.PlcSoftware.BlockGroup;
            return FindIn(root.Blocks, root.Groups, root.Name, targets[descriptor].UserGroup, descriptor.BlockName);
        }

        public void Generate(OperationInterlockDescriptor descriptor)
        {
            var target = targets[descriptor];
            // Recheck immediately before source generation: Siemens generation can overwrite blocks.
            if (Find(descriptor).Any())
                throw new InvalidOperationException("A block appeared after preflight for " + descriptor.Identity + "/" + descriptor.BlockName + ". Generation stopped; rerun preflight.");
            string directory = Path.Combine(Directory.GetCurrentDirectory(), "OperationInterlockSources",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string file = Path.Combine(directory, descriptor.BlockName + ".scl");
            string diagnostics = Path.Combine(directory, "diagnostics.txt");
            File.WriteAllText(file, descriptor.RenderScl(), Encoding.ASCII);
            string sourceName = "G014_" + Guid.NewGuid().ToString("N") + ".scl";
            PlcExternalSource source = null;
            var log = new StringBuilder("Operation interlock " + descriptor.Identity + "/" + descriptor.BlockName + Environment.NewLine + "Source: " + file + Environment.NewLine);
            try
            {
                source = SiemensPortal.Current.PlcSoftware.ExternalSourceGroup.ExternalSources.CreateFromFile(sourceName, file);
                // Installed V21 help 37231359755/131792485771.htm documents exact folder placement.
                // KeepOnError retains evidence. Compile explicitly to collect errors AND warnings.
                var generated = source.GenerateBlocksFromSource(target.UserGroup, GenerateBlockOption.KeepOnError);
                var block = generated.OfType<PlcBlock>().SingleOrDefault();
                if (generated.Count != 1 || block == null || block.Name != descriptor.BlockName || !Equals(block.Parent, target.UserGroup))
                    throw new InvalidOperationException("Unexpected source generation result or target folder for " + descriptor.Identity);
                var result = block.GetService<ICompilable>().Compile();
                AppendMessages(result.Messages, log);
                log.AppendLine("Errors: " + result.ErrorCount + "; warnings: " + result.WarningCount);
                File.WriteAllText(diagnostics, log.ToString());
                Console.WriteLine(log.ToString());
                if (result.ErrorCount != 0 || result.State == CompilerResultState.Error)
                    throw new InvalidOperationException("Siemens SCL compilation failed.");
                // Verify actual generated interface, including Program_Alarm identity/version.
                descriptor.ValidateExisting(Find(descriptor).Single().Document, OperationInterlockLanguage.Scl);
                source.Delete(); // Only the uniquely named source created here, only on success.
            }
            catch (Exception error)
            {
                log.AppendLine(error.ToString());
                File.WriteAllText(diagnostics, log.ToString());
                throw new InvalidOperationException("Operation interlock " + descriptor.Identity + "/" + descriptor.BlockName + " failed. Source and diagnostics retained at " + directory + "; Portal source: " + sourceName + ". Dependent callers were not imported. Reconcile explicitly before retrying.", error);
            }
        }

        private static void AppendMessages(IEnumerable<CompilerResultMessage> messages, StringBuilder log)
        {
            foreach (var message in messages)
            {
                log.AppendLine(message.State + " " + message.Path + ": " + message.Description);
                AppendMessages(message.Messages, log);
            }
        }
    }
}
