using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace TIA_LIB.Generation
{
    public sealed class GeneratedStatement
    {
        public GeneratedAssignment Assignment { get; }
        public GeneratedExpression Condition { get; }
        public GeneratedInvocation Call { get; }
        public bool Instruction { get; }
        internal GeneratedStatement(GeneratedAssignment assignment, GeneratedExpression condition) { Assignment = assignment; Condition = condition; }
        internal GeneratedStatement(GeneratedInvocation call, bool instruction) { Call = call; Instruction = instruction; }
    }
    public sealed class GeneratedRegion
    {
        public string Title { get; }
        public ReadOnlyCollection<GeneratedStatement> Statements { get; }
        public ReadOnlyCollection<GeneratedRegion> Children { get; }
        public ReadOnlyCollection<string> Comments { get; }
        internal GeneratedRegion(string title, IEnumerable<GeneratedStatement> statements, params GeneratedRegion[] children)
            : this(title, statements, new string[0], children) { }
        internal GeneratedRegion(string title, IEnumerable<GeneratedStatement> statements, IEnumerable<string> comments, params GeneratedRegion[] children)
        { Title = title; Statements = statements.ToList().AsReadOnly(); Comments = comments.ToList().AsReadOnly(); Children = Array.AsReadOnly(children); }
    }
    public class GeneratedStructuredBody
    {
        public ReadOnlyCollection<GeneratedRegion> Regions { get; }
        public GeneratedStructuredBody(IEnumerable<GeneratedRegion> regions) { Regions = regions.ToList().AsReadOnly(); }
        public string RenderScl()
        {
            var text = new StringBuilder();
            foreach (var region in Regions) RenderRegion(region, text);
            return text.ToString();
        }
        static void RenderRegion(GeneratedRegion region, StringBuilder text)
        {
            text.AppendLine("REGION " + region.Title);
            foreach (string comment in region.Comments) text.AppendLine("// " + comment);
            foreach (var statement in region.Statements)
            {
                if (statement.Assignment != null)
                {
                    if (statement.Condition != null) text.AppendLine("IF " + statement.Condition.RenderScl() + " THEN");
                    text.AppendLine(statement.Assignment.RenderScl());
                    if (statement.Condition != null) text.AppendLine("END_IF;");
                }
                else
                {
                    var call = statement.Call;
                    text.Append(SiemensSource.Reference(call.Instance)).Append('(').Append(string.Join(", ", call.Arguments.Select(a => SiemensSource.Identifier(a.Name) + " := " + a.Expression.RenderScl()))).AppendLine(");");
                }
            }
            foreach (var child in region.Children) RenderRegion(child, text);
            text.AppendLine("END_REGION");
        }
    }
    // Phases are an editable SCL template in BOTH generation modes by explicit
    // user decision. Preserve its control flow, comments and inactive placeholders;
    // execution equivalence of a flattened starter is not template equivalence.
    public static class GeneratedPhaseModel
    {
        public const string ModelVersion = "EmptyPhase-1-preserved";
        const string AuditedBodyHash = "7DB014371985189CB1B959225CA98DBAD84EAA7D65B5E635B5ABA74AEB5CB510";
        public static void ValidateFreshTemplate(XElement document)
        {
            var bodies = document.Descendants("SW.Blocks.CompileUnit").ToArray();
            var sources = bodies.SelectMany(b => b.Descendants().Where(e => e.Name.LocalName == "StructuredText")).ToArray();
            if (bodies.Length != 1 || sources.Length != 1 || ManagedArtifactOwnership.Fingerprint(new XElement("Document",
                new XElement("SW.Blocks.CompileUnit", new XElement(sources[0])))) != AuditedBodyHash)
                throw new InvalidOperationException("Phase body differs from audited " + ModelVersion + ". Preserve the existing/manual phase or reconcile its template explicitly before regenerating.");
        }
        public static XElement PreserveCompileUnit(XElement document)
        {
            ValidateFreshTemplate(document);
            return new XElement(document.Descendants("SW.Blocks.CompileUnit").Single());
        }
        internal sealed class LibraryPin
        {
            internal readonly string Block, Name, Datatype;
            internal LibraryPin(string block, string name, string datatype) { Block = block; Name = name; Datatype = datatype; }
        }
        // Contracts used by the unchanged template; this inventory does not render
        // a replacement phase body or change its external helper call sequence.
        internal static IEnumerable<LibraryPin> RequiredPins
        {
            get
            {
                yield return new LibraryPin("fbOpMsg", "MESSAGE", "Bool");
                yield return new LibraryPin("fbOpMsg", "TYPE", "USInt");
                yield return new LibraryPin("fbTransfer", "BIT_READY", "Bool");
            }
        }
    }
}
