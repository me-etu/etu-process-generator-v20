using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace TIA_LIB.Generation
{
    // Tokenized SCL body, following the repo's working StructuredText/v3 template.
    // The enclosing block/interface stays XML, preserving its multilingual comments,
    // instruction versions, accessibility and expanded instance declaration metadata.
    public sealed class StructuredTextBodyRenderer
    {
        static readonly XNamespace Namespace = "http://www.siemens.com/automation/Openness/SW/NetworkSource/StructuredText/v3";
        int uid = 20;
        XElement Node(string name, params object[] children)
        {
            return new XElement(Namespace + name, new XAttribute("UId", ++uid), children);
        }
        XElement Token(string text) { return Node("Token", new XAttribute("Text", text)); }
        XElement Blank() { return Node("Blank", new XAttribute("Num", 1)); }
        XElement Line() { return Node("NewLine", new XAttribute("Num", 1)); }
        XElement Symbol(SymbolReference reference)
        {
            var symbol = Node("Symbol");
            foreach (var name in reference.Components)
            {
                SiemensSource.Identifier(name);
                if (symbol.HasElements) symbol.Add(Token("."));
                symbol.Add(Node("Component", new XAttribute("Name", name)));
            }
            return Node("Access", new XAttribute("Scope", reference.IsGlobal ? "GlobalVariable" : "LocalVariable"), symbol);
        }
        IEnumerable<XElement> Expression(GeneratedExpression expression)
        {
            switch (expression.Kind)
            {
                case GeneratedExpressionKind.Symbol: return new[] { Symbol(expression.Symbol) };
                case GeneratedExpressionKind.Bit:
                    var bitAccess = Symbol(expression.Symbol);
                    bitAccess.Element(Namespace + "Symbol").Add(Token("."), Token("%X" + expression.BitIndex));
                    return new[] { bitAccess };
                case GeneratedExpressionKind.Literal:
                    return new[] { Node("Access", new XAttribute("Scope", "LiteralConstant"), Node("Constant", Node("ConstantValue", expression.Literal))) };
                case GeneratedExpressionKind.Not:
                    return new[] { Token("NOT"), Blank(), Token("(") }.Concat(Expression(expression.Operands[0])).Concat(new[] { Token(")") });
                case GeneratedExpressionKind.Equal:
                case GeneratedExpressionKind.NotEqual:
                    return new[] { Token("(") }.Concat(Expression(expression.Operands[0]))
                        .Concat(new[] { Blank(), Token(expression.Kind == GeneratedExpressionKind.Equal ? "=" : "<>"), Blank() })
                        .Concat(Expression(expression.Operands[1])).Concat(new[] { Token(")") });
                case GeneratedExpressionKind.And:
                case GeneratedExpressionKind.Or:
                    var tokens = new List<XElement> { Token("(") };
                    foreach (var operand in expression.Operands)
                    {
                        if (tokens.Count != 1) tokens.AddRange(new[] { Blank(), Token(expression.Kind == GeneratedExpressionKind.And ? "AND" : "OR"), Blank() });
                        tokens.AddRange(Expression(operand));
                    }
                    tokens.Add(Token(")")); return tokens;
                default: throw new InvalidOperationException("Unsupported structured expression.");
            }
        }
        void Indent(XElement destination, int depth) { if (depth != 0) destination.Add(Node("Blank", new XAttribute("Num", depth * 4))); }
        void Comment(XElement destination, string text, int depth)
        {
            foreach (string line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                Indent(destination, depth);
                destination.Add(Node("LineComment", new XAttribute("Inserted", "false"), new XAttribute("NoClosingBracket", "false"), Node("Text", " " + line)), Line());
            }
        }
        void Region(XElement destination, string title, int depth)
        { Indent(destination, depth); destination.Add(Token("REGION"), Blank(), Node("Text", SiemensSource.RegionTitle(title)), Line()); }
        void EndRegion(XElement destination, int depth, bool nonempty = true)
        { if (nonempty) destination.Add(Line()); Indent(destination, depth); destination.Add(Token("END_REGION"), Line()); }

        // Prove only closed Boolean trees. Symbols (including ENO), comparisons and
        // unknown forms are never evaluated or inferred from a producer's EN.
        internal static bool? ConstantEnable(GeneratedExpression expression)
        {
            if (expression.Kind == GeneratedExpressionKind.Literal && expression.Datatype == "Bool")
            {
                if (expression.Literal == "TRUE") return true;
                if (expression.Literal == "FALSE") return false;
            }
            if (expression.Kind == GeneratedExpressionKind.Not && expression.Operands.Count == 1)
            { var value = ConstantEnable(expression.Operands[0]); return value.HasValue ? (bool?)!value.Value : null; }
            if ((expression.Kind == GeneratedExpressionKind.And || expression.Kind == GeneratedExpressionKind.Or) && expression.Operands.Count > 0)
            {
                var values = expression.Operands.Select(ConstantEnable).ToArray();
                if (values.Any(v => !v.HasValue)) return null;
                return expression.Kind == GeneratedExpressionKind.And ? values.All(v => v.Value) : values.Any(v => v.Value);
            }
            return null;
        }
        static int DisplayLength(SymbolReference reference)
        {
            return (reference.IsGlobal ? 0 : 1) + reference.Components.Sum(c =>
                System.Text.RegularExpressions.Regex.IsMatch(c, @"\A[A-Za-z_][A-Za-z_0-9]*\z") ? c.Length : c.Length + 2) + reference.Components.Count - 1;
        }
        void Invocation(XElement destination, GeneratedInvocation invocation, int depth, bool instruction = false)
        {
            bool conditional = ConstantEnable(invocation.Enable) != true;
            if (conditional) { Indent(destination, depth); destination.Add(Token("IF"), Blank(), Expression(invocation.Enable), Blank(), Token("THEN"), Line()); depth++; }
            Indent(destination, depth);
            var call = instruction ? Node("Instruction") : Node("CallInfo", new XAttribute("BlockType", "FB"), new XAttribute("Name", invocation.BlockType));
            if (!instruction) call.Add(Node("Instance", new XAttribute("Scope", invocation.Instance.IsGlobal ? "GlobalVariable" : "LocalVariable"),
                invocation.Instance.Components.Select(c => Node("Component", new XAttribute("Name", c)))));
            call.Add(Token("("));
            bool first = true;
            foreach (var argument in invocation.Arguments)
            {
                if (!first) call.Add(Token(","), Line(), Node("Blank", new XAttribute("Num", depth * 4 + DisplayLength(invocation.Instance) + 1)));
                first = false;
                call.Add(Node("Parameter", new XAttribute("Name", argument.Name), Blank(),
                    Token(argument.Direction == GeneratedPinDirection.Output ? "=>" : ":="), Blank(), Expression(argument.Expression)));
            }
            call.Add(Token(")"));
            if (instruction) destination.Add(Symbol(invocation.Instance));
            destination.Add(Node("Access", new XAttribute("Scope", "Call"), call), Token(";"), Line());
            if (conditional) { depth--; Indent(destination, depth); destination.Add(Token("END_IF"), Token(";"), Line()); }
            foreach (var assignment in invocation.FollowingAssignments)
            { Indent(destination, depth); destination.Add(Expression(assignment.Destination), Blank(), Token(":="), Blank(), Expression(assignment.Value), Token(";"), Line()); }
        }
        static string Purpose(GeneratedLogicalNetwork network)
        {
            switch (network.Role)
            {
                case GeneratedNetworkRole.UnitStatus: return "Unit status";
                case GeneratedNetworkRole.UnitPreconditions: return "Unit preconditions for recipe mode";
                case GeneratedNetworkRole.UnitControl: return "Unit status control";
                case GeneratedNetworkRole.OperationControl: return "Operation status control";
                case GeneratedNetworkRole.OperationInterlocks: return "Interlocks for devices and phases assigned to this operation";
                case GeneratedNetworkRole.PhasePreconditions: return "Please assign all pre-conditions for this phase here";
                case GeneratedNetworkRole.PhaseControl: return "Status control of this phase with its non-technical parameters";
                case GeneratedNetworkRole.PhaseCall: return "Phase sequence logic";
                default: return network.Description;
            }
        }
        public XElement Render(GeneratedBlockBody body)
        {
            var result = new XElement(Namespace + "StructuredText");
            bool devices = false, operations = false, completedOperations = false;
            DeviceCategory? category = null;
            foreach (var network in body.Networks)
            {
                if (devices && !network.Category.HasValue)
                { EndRegion(result, 1); EndRegion(result, 0); devices = false; category = null; }
                if (operations && network.Role != GeneratedNetworkRole.OperationCall)
                { EndRegion(result, 0); operations = false; completedOperations = true; }
                if (network.Category.HasValue)
                {
                    if (!devices)
                    { if (result.HasElements) result.Add(Line()); Region(result, "Devices", 0); Comment(result, "All assigned IOs to this unit", 1); devices = true; }
                    if (category != network.Category)
                    {
                        if (category.HasValue) { EndRegion(result, 1); result.Add(Line()); }
                        category = network.Category; Region(result, category.Value.ToString(), 1);
                    }
                    else result.Add(Line());
                }
                else if (result.HasElements) result.Add(Line());
                if (network.Role == GeneratedNetworkRole.OperationCall && !operations)
                {
                    if (completedOperations) throw new InvalidOperationException("Operation calls are not contiguous; reconcile the model without changing scan order.");
                    Region(result, "Operations", 0); Comment(result, "Operations under this unit", 1); operations = true;
                }
                int depth = devices ? 2 : operations ? 1 : 0;
                if (network.Role == GeneratedNetworkRole.PhasePreconditions)
                { Region(result, "*****************", depth); EndRegion(result, depth, false); result.Add(Line()); }
                Region(result, network.Body.Title, depth);
                var comments = network.Body.Comments;
                foreach (string comment in comments) Comment(result, comment, depth + 1);
                string purpose = Purpose(network);
                if (comments.Count == 0 && !string.IsNullOrWhiteSpace(purpose)) Comment(result, purpose, depth + 1);
                bool first = true;
                foreach (var call in network.Body.Invocations)
                { if (!first) result.Add(Line()); first = false; Invocation(result, call, depth + 1); }
                string previousDevice = null;
                foreach (var assignment in network.Body.Assignments)
                {
                    string device = assignment.Destination.Symbol.Components.Count > 1 ? assignment.Destination.Symbol.Components[1] : null;
                    if (network.Role == GeneratedNetworkRole.ModeService && previousDevice != null && previousDevice != device) result.Add(Line());
                    Indent(result, depth + 1);
                    DestinationAssignment(result, assignment);
                    previousDevice = device;
                }
                EndRegion(result, depth, comments.Count != 0 || !string.IsNullOrWhiteSpace(purpose) || network.Body.Invocations.Count + network.Body.Assignments.Count != 0);
            }
            if (devices) { EndRegion(result, 1); EndRegion(result, 0); }
            if (operations) EndRegion(result, 0);
            return Renumber(result);
        }
        void DestinationAssignment(XElement destination, GeneratedAssignment assignment)
        { destination.Add(Expression(assignment.Destination), Blank(), Token(":="), Blank(), Expression(assignment.Value), Token(";"), Line()); }
        static XElement Renumber(XElement result)
        {
            int id = 20;
            foreach (var element in result.Descendants()) if (element.Attribute("UId") != null) element.SetAttributeValue("UId", ++id);
            return result;
        }
        // Readable diagnostic source from the same typed token tree. Always quote
        // identifiers here; Portal may omit optional quotes in its source export.
        public static string ReadSource(XElement element)
        {
            switch (element.Name.LocalName)
            {
                case "Token": return (string)element.Attribute("Text");
                case "Blank": return new string(' ', (int)element.Attribute("Num"));
                case "NewLine": return string.Concat(Enumerable.Repeat(Environment.NewLine, (int)element.Attribute("Num")));
                case "Text": case "ConstantValue": return element.Value;
                case "ConstantType": return "";
                case "Component": return SiemensSource.Identifier((string)element.Attribute("Name"));
                case "Parameter": return SiemensSource.Identifier((string)element.Attribute("Name")) + string.Concat(element.Elements().Select(ReadSource));
                case "LineComment": return "//" + element.Value;
                case "Access": return ((string)element.Attribute("Scope") == "LocalVariable" ? "#" : "") + string.Concat(element.Elements().Select(ReadSource));
                case "CallInfo":
                    var instance = element.Elements().Single(e => e.Name.LocalName == "Instance");
                    return ((string)instance.Attribute("Scope") == "LocalVariable" ? "#" : "") + string.Join(".", instance.Elements().Select(ReadSource)) +
                        string.Concat(element.Elements().Where(e => e != instance).Select(ReadSource));
                default: return string.Concat(element.Elements().Select(ReadSource));
            }
        }
        public XElement Render(GeneratedStructuredBody model)
        {
            var result = new XElement(Namespace + "StructuredText");
            foreach (var region in model.Regions)
            { if (result.HasElements) result.Add(Line()); GeneratedRegion(result, region, 0); }
            return Renumber(result);
        }
        void GeneratedRegion(XElement destination, GeneratedRegion region, int depth)
        {
            Region(destination, region.Title, depth);
            foreach (string comment in region.Comments) Comment(destination, comment, depth + 1);
            bool previousCall = false;
            foreach (var statement in region.Statements)
            {
                if (statement.Assignment == null)
                { if (previousCall) destination.Add(Line()); Invocation(destination, statement.Call, depth + 1, statement.Instruction); previousCall = true; continue; }
                previousCall = false;
                if (statement.Condition != null) { Indent(destination, depth + 1); destination.Add(Token("IF"), Blank(), Expression(statement.Condition), Blank(), Token("THEN"), Line()); }
                Indent(destination, depth + (statement.Condition == null ? 1 : 2));
                destination.Add(Expression(statement.Assignment.Destination), Blank(), Token(":="), Blank(), Expression(statement.Assignment.Value), Token(";"), Line());
                if (statement.Condition != null) { Indent(destination, depth + 1); destination.Add(Token("END_IF"), Token(";"), Line()); }
            }
            foreach (var child in region.Children) { destination.Add(Line()); GeneratedRegion(destination, child, depth + 1); }
            EndRegion(destination, depth, region.Comments.Count + region.Statements.Count + region.Children.Count != 0);
        }
    }
}
