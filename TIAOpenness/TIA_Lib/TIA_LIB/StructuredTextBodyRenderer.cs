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
        void Region(XElement destination, string title)
        { destination.Add(Token("REGION"), Blank(), Node("Text", SiemensSource.RegionTitle(title)), Line()); }
        void EndRegion(XElement destination) { destination.Add(Token("END_REGION"), Line()); }
        void Invocation(XElement destination, GeneratedInvocation invocation, bool instruction = false)
        {
            bool conditional = invocation.Enable.Kind != GeneratedExpressionKind.Literal || invocation.Enable.Literal != "TRUE";
            if (conditional) destination.Add(Token("IF"), Blank(), Expression(invocation.Enable), Blank(), Token("THEN"), Line());
            var call = instruction ? Node("Instruction") : Node("CallInfo", new XAttribute("BlockType", "FB"), new XAttribute("Name", invocation.BlockType));
            if (!instruction) call.Add(Node("Instance", new XAttribute("Scope", invocation.Instance.IsGlobal ? "GlobalVariable" : "LocalVariable"),
                invocation.Instance.Components.Select(c => Node("Component", new XAttribute("Name", c)))));
            call.Add(Token("("));
            bool first = true;
            foreach (var argument in invocation.Arguments)
            {
                if (!first) call.Add(Token(","), Blank());
                first = false;
                call.Add(Node("Parameter", new XAttribute("Name", argument.Name),
                    Token(argument.Direction == GeneratedPinDirection.Output ? "=>" : ":="), Blank(), Expression(argument.Expression)));
            }
            call.Add(Token(")"));
            if (instruction) destination.Add(Symbol(invocation.Instance));
            destination.Add(Node("Access", new XAttribute("Scope", "Call"), call), Token(";"), Line());
            if (conditional) destination.Add(Token("END_IF"), Token(";"), Line());
            foreach (var assignment in invocation.FollowingAssignments)
                destination.Add(Expression(assignment.Destination), Blank(), Token(":="), Blank(), Expression(assignment.Value), Token(";"), Line());
        }
        public XElement Render(GeneratedBlockBody body)
        {
            var result = new XElement(Namespace + "StructuredText");
            bool devices = false;
            DeviceCategory? category = null;
            foreach (var network in body.Networks)
            {
                if (network.Category.HasValue)
                {
                    if (!devices) { Region(result, "Devices"); devices = true; }
                    if (category != network.Category)
                    {
                        if (category.HasValue) EndRegion(result);
                        category = network.Category; Region(result, category.Value.ToString());
                    }
                }
                else if (devices) { EndRegion(result); EndRegion(result); devices = false; category = null; }
                Region(result, network.Body.Title);
                foreach (var call in network.Body.Invocations) Invocation(result, call);
                EndRegion(result);
            }
            if (devices) { EndRegion(result); EndRegion(result); }
            // A renderer instance can be reused without introducing run-dependent IDs.
            int id = 20;
            foreach (var element in result.Descendants())
                if (element.Attribute("UId") != null) element.SetAttributeValue("UId", ++id);
            return result;
        }
        public XElement Render(GeneratedStructuredBody model)
        {
            var result = new XElement(Namespace + "StructuredText");
            foreach (var region in model.Regions) GeneratedRegion(result, region);
            int id = 20;
            foreach (var element in result.Descendants()) if (element.Attribute("UId") != null) element.SetAttributeValue("UId", ++id);
            return result;
        }
        void GeneratedRegion(XElement destination, GeneratedRegion region)
        {
            Region(destination, region.Title);
            foreach (var statement in region.Statements)
            {
                if (statement.Assignment == null) { Invocation(destination, statement.Call, statement.Instruction); continue; }
                if (statement.Condition != null) destination.Add(Token("IF"), Blank(), Expression(statement.Condition), Blank(), Token("THEN"), Line());
                destination.Add(Expression(statement.Assignment.Destination), Blank(), Token(":="), Blank(), Expression(statement.Assignment.Value), Token(";"), Line());
                if (statement.Condition != null) destination.Add(Token("END_IF"), Token(";"), Line());
            }
            foreach (var child in region.Children) GeneratedRegion(destination, child);
            EndRegion(destination);
        }
    }
}
