using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml.Linq;

namespace TIA_LIB.Generation
{
    // Each statement gets its own network so inter-statement reads retain scan order.
    // No generated SCL helper is called from this FBD representation.
    public sealed class PhaseFbdRenderer
    {
        static readonly XNamespace Namespace = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5";
        int uid;
        XElement parts, wires;
        XElement Node(string name, params object[] children) { return new XElement(Namespace + name, children); }
        XElement Pin(int id, string name) { return Node("NameCon", new XAttribute("UId", id), new XAttribute("Name", name)); }
        XElement Ident(int id) { return Node("IdentCon", new XAttribute("UId", id)); }
        void Wire(params XElement[] endpoints) { wires.Add(Node("Wire", new XAttribute("UId", ++uid), endpoints)); }
        XElement Template(string name, string type, string value) { return Node("TemplateValue", new XAttribute("Name", name), new XAttribute("Type", type), value); }
        int Access(GeneratedExpression expression)
        {
            int id = ++uid;
            if (expression.Kind == GeneratedExpressionKind.Literal)
            {
                parts.Add(Node("Access", new XAttribute("UId", id), new XAttribute("Scope", "LiteralConstant"),
                    Node("Constant", Node("ConstantType", expression.Datatype), Node("ConstantValue", expression.Literal))));
                return id;
            }
            if (expression.Kind != GeneratedExpressionKind.Symbol && expression.Kind != GeneratedExpressionKind.Bit)
                throw new InvalidOperationException("An FBD operand requires a symbol or literal.");
            var symbol = Node("Symbol");
            foreach (string component in expression.Symbol.Components) symbol.Add(Node("Component", new XAttribute("Name", component)));
            if (expression.Kind == GeneratedExpressionKind.Bit)
                symbol.Elements().Last().SetAttributeValue("SliceAccessModifier", "x" + expression.BitIndex);
            parts.Add(Node("Access", new XAttribute("UId", id), new XAttribute("Scope", expression.Symbol.IsGlobal ? "GlobalVariable" : "LocalVariable"), symbol));
            return id;
        }
        int Part(string name, params object[] content)
        {
            int id = ++uid;
            parts.Add(Node("Part", new XAttribute("Name", name), new XAttribute("UId", id), content));
            return id;
        }
        void Input(int destination, string pin, GeneratedExpression value)
        {
            if (value.Kind == GeneratedExpressionKind.Symbol || value.Kind == GeneratedExpressionKind.Bit || value.Kind == GeneratedExpressionKind.Literal)
            { Wire(Ident(Access(value)), Pin(destination, pin)); return; }
            if (value.Kind == GeneratedExpressionKind.Not)
            {
                var operand = value.Operands[0];
                if (operand.Kind == GeneratedExpressionKind.And || operand.Kind == GeneratedExpressionKind.Or)
                {
                    // TIA rejects input inversion on a gate-to-gate connection.
                    // Push it through the expression using De Morgan's law.
                    Input(destination, pin, GeneratedExpression.Boolean(operand.Kind == GeneratedExpressionKind.And ? GeneratedExpressionKind.Or : GeneratedExpressionKind.And,
                        operand.Operands.Select(o => GeneratedExpression.Boolean(GeneratedExpressionKind.Not, o)).ToArray()));
                }
                else if (operand.Kind == GeneratedExpressionKind.Equal || operand.Kind == GeneratedExpressionKind.NotEqual)
                    Input(destination, pin, GeneratedExpression.Compare(operand.Kind != GeneratedExpressionKind.Equal, operand.Datatype, operand.Operands[0], operand.Operands[1]));
                else if (operand.Kind == GeneratedExpressionKind.Literal)
                {
                    if (operand.Literal != "TRUE" && operand.Literal != "FALSE") throw new InvalidOperationException("Expected a Bool literal under FBD NOT.");
                    Input(destination, pin, GeneratedExpression.Constant("Bool", operand.Literal == "TRUE" ? "FALSE" : "TRUE"));
                }
                else if (operand.Kind == GeneratedExpressionKind.Symbol || operand.Kind == GeneratedExpressionKind.Bit)
                {
                    var target = parts.Elements().Single(e => (int)e.Attribute("UId") == destination);
                    if (target.Name.LocalName == "Part" && ((string)target.Attribute("Name") == "A" || (string)target.Attribute("Name") == "O") && pin.StartsWith("in", StringComparison.Ordinal))
                    {
                        target.Add(Node("Negated", new XAttribute("Name", pin)));
                        Input(destination, pin, operand);
                    }
                    else
                    {
                        // Coils/enables/call pins receive a positive gate output;
                        // the inversion belongs to the direct symbolic operand.
                        int inverter = Part("A", Template("Card", "Cardinality", "2"), Node("Negated", new XAttribute("Name", "in1")));
                        Input(inverter, "in1", operand);
                        Input(inverter, "in2", GeneratedExpression.Constant("Bool", "TRUE"));
                        Wire(Pin(inverter, "out"), Pin(destination, pin));
                    }
                }
                else throw new InvalidOperationException("Unsupported FBD NOT operand " + operand.Kind + ".");
                return;
            }
            string instruction;
            object[] templates;
            if (value.Kind == GeneratedExpressionKind.And || value.Kind == GeneratedExpressionKind.Or)
            {
                instruction = value.Kind == GeneratedExpressionKind.And ? "A" : "O";
                templates = new object[] { Template("Card", "Cardinality", value.Operands.Count.ToString()) };
            }
            else if (value.Kind == GeneratedExpressionKind.Equal || value.Kind == GeneratedExpressionKind.NotEqual)
            {
                instruction = value.Kind == GeneratedExpressionKind.Equal ? "Eq" : "Ne";
                templates = new object[] { Template("SrcType", "Type", value.Datatype), Template("Card", "Cardinality", "2") };
            }
            else throw new InvalidOperationException("Unsupported FBD expression " + value.Kind + ".");
            int producer = Part(instruction, templates);
            for (int index = 0; index < value.Operands.Count; index++) Input(producer, "in" + (index + 1), value.Operands[index]);
            Wire(Pin(producer, "out"), Pin(destination, pin));
        }
        void Statement(GeneratedStatement statement)
        {
            if (statement.Assignment != null)
            {
                var assignment = statement.Assignment;
                var value = assignment.Value;
                bool boolean = assignment.Destination.Datatype == "Bool";
                if (boolean)
                {
                    if (statement.Condition != null)
                    {
                        // Set/reset coils perform no destination write on a false guard.
                        // Retain sticky bits without introducing unconditional old-value
                        // assignments or another stateful helper invocation.
                        if (value.Kind != GeneratedExpressionKind.Literal || (value.Literal != "TRUE" && value.Literal != "FALSE"))
                            throw new InvalidOperationException("Conditional nonconstant Bool assignment requires an explicit supported FBD lowering.");
                        int guarded = Part(value.Literal == "TRUE" ? "SCoil" : "RCoil");
                        Input(guarded, "in", statement.Condition); Wire(Pin(guarded, "operand"), Ident(Access(assignment.Destination)));
                    }
                    else
                    {
                        int coil = Part("Coil"); Input(coil, "in", value); Wire(Pin(coil, "operand"), Ident(Access(assignment.Destination)));
                    }
                }
                else
                {
                    // MOVE infers operand types from its typed connections. Unlike
                    // comparison instructions it has no Type template parameter;
                    // Openness rejects that parameter even though the XSD allows it.
                    int move = Part("Move", Template("Card", "Cardinality", "1"));
                    Input(move, "in", value);
                    if (statement.Condition == null) Wire(Node("Powerrail"), Pin(move, "en"));
                    else Input(move, "en", statement.Condition);
                    Wire(Pin(move, "out1"), Ident(Access(assignment.Destination)));
                }
                return;
            }
            var call = statement.Call;
            int id = ++uid;
            var instance = Node("Instance", new XAttribute("UId", ++uid), new XAttribute("Scope", "LocalVariable"),
                call.Instance.Components.Select(c => Node("Component", new XAttribute("Name", c))));
            if (statement.Instruction)
            {
                parts.Add(Node("Part", new XAttribute("Name", call.BlockType), new XAttribute("UId", id), new XAttribute("Version", "1.0"), instance));
            }
            else
            {
                parts.Add(Node("Call", new XAttribute("UId", id), Node("CallInfo", new XAttribute("Name", call.BlockType), new XAttribute("BlockType", "FB"),
                    instance, call.Arguments.Select(a => Node("Parameter", new XAttribute("Name", a.Name), new XAttribute("Section", a.Direction.ToString()), new XAttribute("Type", a.Datatype))))));
            }
            Wire(Node("Powerrail"), Pin(id, "en"));
            foreach (var argument in call.Arguments) Input(id, argument.Name, argument.Expression);
        }
        static XElement Presentation(string id, string composition, string value)
        {
            return new XElement("MultilingualText", new XAttribute("ID", id), new XAttribute("CompositionName", composition),
                new XElement("ObjectList", new XElement("MultilingualTextItem", new XAttribute("ID", id + "A"), new XAttribute("CompositionName", "Items"),
                    new XElement("AttributeList", new XElement("Culture", "en-US"), new XElement("Text", value)))));
        }
        XElement Network(GeneratedRegion region, GeneratedStatement statement, string path, int index)
        {
            uid = 20; parts = Node("Parts"); wires = Node("Wires");
            if (statement != null) Statement(statement);
            var graph = Node("FlgNet", parts);
            if (wires.HasElements) graph.Add(wires);
            return new XElement("SW.Blocks.CompileUnit", new XAttribute("ID", index.ToString("X")), new XAttribute("CompositionName", "CompileUnits"),
                // A presentation-only network uses the same empty NetworkSource as
                // EmptyNetworkWithOutCall.xml. TIA rejects FlgNet containing Parts
                // but no executable graph, even though that shape passes the XSD.
                new XElement("AttributeList", new XElement("NetworkSource", statement == null ? null : graph), new XElement("ProgrammingLanguage", "FBD")),
                new XElement("ObjectList", Presentation("1", "Title", region.Title), Presentation("2", "Comment", "[ETU region: " + path + "]")));
        }
        void Region(GeneratedRegion region, string prefix, List<XElement> result)
        {
            string path = prefix.Length == 0 ? region.Title : prefix + "/" + region.Title;
            if (region.Statements.Count == 0) result.Add(Network(region, null, path, result.Count + 1));
            foreach (var statement in region.Statements) result.Add(Network(region, statement, path, result.Count + 1));
            foreach (var child in region.Children) Region(child, path, result);
        }
        public ReadOnlyCollection<XElement> Render(GeneratedStructuredBody model)
        {
            var result = new List<XElement>();
            foreach (var region in model.Regions) Region(region, "", result);
            return result.AsReadOnly();
        }
    }
}
