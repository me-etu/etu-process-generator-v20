using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace TIA_LIB.Generation
{
    // Whole-block ownership format v1. A matching ledger authorizes regeneration
    // of generated logic only while the last exported block remains unchanged.
    // Manual edits stop regeneration; no REGION regex or implicit legacy adoption.
    public sealed class ManagedArtifactOwnership
    {
        readonly XElement shadow;
        readonly string exportedHash;
        public string Identity { get; }
        public string Language { get; }
        public string BlockName { get; }
        ManagedArtifactOwnership(string identity, string language, string blockName, string hash, XElement model)
        {
            if (string.IsNullOrWhiteSpace(identity) || string.IsNullOrWhiteSpace(blockName)) throw new ArgumentException("Managed identity and block name are required.");
            if (language != "FBD" && language != "SCL") throw new InvalidOperationException("Unsupported managed block language " + language + ".");
            Identity = identity; Language = language; BlockName = blockName; exportedHash = hash; shadow = new XElement(model);
        }
        static XElement Block(XElement document)
        {
            var blocks = document.Elements("SW.Blocks.FB").ToArray();
            if (blocks.Length != 1) throw new InvalidOperationException("Managed artifact requires one FB document.");
            return blocks[0];
        }
        static string Name(XElement document) { return (string)Block(document).Element("AttributeList")?.Element("Name"); }
        static string BodyLanguage(XElement document) { return (string)Block(document).Element("AttributeList")?.Element("ProgrammingLanguage"); }
        public static ManagedArtifactOwnership Capture(string identity, string language, XElement exportedBlock, XElement generatedModel)
        {
            if (exportedBlock == null || generatedModel == null) throw new ArgumentNullException("A post-import export and generated model are required.");
            if (BodyLanguage(exportedBlock) != language || Name(exportedBlock) != Name(generatedModel))
                throw new InvalidOperationException("Managed capture block identity/language differs from the imported artifact.");
            return new ManagedArtifactOwnership(identity, language, Name(exportedBlock), Fingerprint(exportedBlock), generatedModel);
        }
        public XElement Restore(string identity, string language, XElement currentExport)
        {
            if (identity != Identity || language != Language || Name(currentExport) != BlockName || BodyLanguage(currentExport) != Language)
                throw new InvalidOperationException("Managed artifact identity/language differs for '" + identity + "'. Explicit reconciliation is required; no automatic conversion.");
            if (Fingerprint(currentExport) != exportedHash)
                throw new InvalidOperationException("Managed artifact '" + Identity + "' changed after the recorded generation. Preserve its manual logic/interface/comments and reconcile explicitly before regenerating.");
            // Reuse current, verified compiler-expanded interface/defaults and metadata.
            // Only the owned body is restored to the generator's editable representation.
            var restored = new XElement(currentExport);
            var block = Block(restored);
            var sourceBlock = Block(shadow);
            var destinationList = block.Element("ObjectList");
            var sourceList = sourceBlock.Element("ObjectList");
            if (destinationList == null || sourceList == null) throw new InvalidOperationException("Missing managed ObjectList for " + Identity + ".");
            var oldBodies = destinationList.Elements("SW.Blocks.CompileUnit").ToArray();
            var bodies = sourceList.Elements("SW.Blocks.CompileUnit").Select(e => new XElement(e)).ToArray();
            if (oldBodies.Length == 0 || bodies.Length == 0) throw new InvalidOperationException("Missing managed executable body for " + Identity + ".");
            oldBodies[0].AddBeforeSelf(bodies);
            foreach (var old in oldBodies) old.Remove();
            block.Element("AttributeList").Element("ProgrammingLanguage").Value = BodyLanguage(shadow);
            return restored;
        }
        public XElement ToXml()
        {
            return new XElement("EtuManagedArtifact", new XAttribute("Version", "1"),
                new XAttribute("Identity", Identity), new XAttribute("Language", Language), new XAttribute("Block", BlockName),
                new XAttribute("ExportHash", exportedHash), new XAttribute("ModelHash", Fingerprint(shadow)),
                new XElement("EditableModel", new XElement(shadow)));
        }
        public static ManagedArtifactOwnership Read(XElement ledger)
        {
            if (ledger.Name != "EtuManagedArtifact" || (string)ledger.Attribute("Version") != "1" ||
                ledger.Elements().Count() != 1 || ledger.Elements("EditableModel").Count() != 1)
                throw new InvalidOperationException("Unsupported or malformed managed ownership format.");
            var models = ledger.Element("EditableModel").Elements().ToArray();
            if (models.Length != 1) throw new InvalidOperationException("Expected one managed editable model.");
            string hash = (string)ledger.Attribute("ExportHash");
            if (hash == null || hash.Length != 64 || hash.Any(c => !Uri.IsHexDigit(c)) ||
                (string)ledger.Attribute("ModelHash") != Fingerprint(models[0]) || (string)ledger.Attribute("Block") != Name(models[0]))
                throw new InvalidOperationException("Managed ledger model/checksum differs; preserve the current PLC block and reconcile the ledger.");
            return new ManagedArtifactOwnership((string)ledger.Attribute("Identity"), (string)ledger.Attribute("Language"),
                (string)ledger.Attribute("Block"), hash, models[0]);
        }
        public static string Fingerprint(XElement document)
        {
            // Object IDs and per-network UIds are serialization identities, not PLC logic.
            // Keep every other field, including comments, declarations, start values and
            // instruction versions. Unrecognized export changes reject conservatively.
            var root = new XElement(document);
            root.Elements("DocumentInfo").Remove();
            foreach (var network in root.Descendants("SW.Blocks.CompileUnit"))
            {
                var ids = new Dictionary<string, string>();
                foreach (var attr in network.DescendantsAndSelf().Attributes("UId"))
                {
                    string replacement;
                    if (!ids.TryGetValue(attr.Value, out replacement)) { replacement = (ids.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture); ids.Add(attr.Value, replacement); }
                    attr.Value = replacement;
                }
            }
            foreach (var element in root.DescendantsAndSelf())
            {
                var attributes = element.Attributes().Where(a => !a.IsNamespaceDeclaration && a.Name.LocalName != "ID")
                    .OrderBy(a => a.Name.ToString(), StringComparer.Ordinal).Select(a => new XAttribute(a)).ToArray();
                element.ReplaceAttributes(attributes);
            }
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting)))).Replace("-", "");
        }
    }
}
