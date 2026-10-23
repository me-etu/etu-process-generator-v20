using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TIA_LIB.Generation
{
    public enum GeneratedPinDirection { Input, Output, InOut }

    public enum GeneratedExpressionKind { Symbol, Literal, And, Or, Not, Equal, NotEqual, Bit }

    public sealed class GeneratedExpression
    {
        public GeneratedExpressionKind Kind { get; }
        public SymbolReference Symbol { get; }
        public string Datatype { get; }
        public string Literal { get; }
        public int BitIndex { get; }
        public ReadOnlyCollection<GeneratedExpression> Operands { get; }
        GeneratedExpression(GeneratedExpressionKind kind, SymbolReference symbol, string datatype, string literal, IEnumerable<GeneratedExpression> operands, int bitIndex = 0)
        { Kind = kind; Symbol = symbol; Datatype = datatype; Literal = literal; Operands = operands.ToList().AsReadOnly(); BitIndex = bitIndex; }
        internal static GeneratedExpression Reference(SymbolReference symbol, string datatype = null) { return new GeneratedExpression(GeneratedExpressionKind.Symbol, symbol, datatype, null, new GeneratedExpression[0]); }
        internal static GeneratedExpression Constant(string type, string value) { return new GeneratedExpression(GeneratedExpressionKind.Literal, null, type, value, new GeneratedExpression[0]); }
        internal static GeneratedExpression Boolean(GeneratedExpressionKind kind, params GeneratedExpression[] operands)
        {
            if (kind == GeneratedExpressionKind.Not && operands.Length == 1 && operands[0].Kind == GeneratedExpressionKind.Not) return operands[0].Operands[0];
            return new GeneratedExpression(kind, null, "Bool", null, operands);
        }
        internal static GeneratedExpression Compare(bool equal, string type, GeneratedExpression first, GeneratedExpression second)
        { return new GeneratedExpression(equal ? GeneratedExpressionKind.Equal : GeneratedExpressionKind.NotEqual, null, type, null, new[] { first, second }); }
        internal static GeneratedExpression Bit(SymbolReference symbol, int bit)
        {
            if (bit < 0 || bit > 31) throw new ArgumentOutOfRangeException(nameof(bit));
            return new GeneratedExpression(GeneratedExpressionKind.Bit, symbol, "Bool", null, new GeneratedExpression[0], bit);
        }
        public string RenderScl()
        {
            switch (Kind)
            {
                case GeneratedExpressionKind.Symbol: return SiemensSource.Reference(Symbol);
                case GeneratedExpressionKind.Literal: return Literal;
                case GeneratedExpressionKind.Not: return "NOT (" + Operands[0].RenderScl() + ")";
                case GeneratedExpressionKind.And: return "(" + string.Join(" AND ", Operands.Select(e => e.RenderScl())) + ")";
                case GeneratedExpressionKind.Or: return "(" + string.Join(" OR ", Operands.Select(e => e.RenderScl())) + ")";
                case GeneratedExpressionKind.Equal:
                case GeneratedExpressionKind.NotEqual: return "(" + Operands[0].RenderScl() + (Kind == GeneratedExpressionKind.Equal ? " = " : " <> ") + Operands[1].RenderScl() + ")";
                case GeneratedExpressionKind.Bit: return SiemensSource.Reference(Symbol) + ".%X" + BitIndex;
                default: throw new InvalidOperationException("Unsupported expression kind.");
            }
        }
    }

    public sealed class GeneratedAssignment
    {
        public GeneratedExpression Destination { get; }
        public GeneratedExpression Value { get; }
        internal GeneratedAssignment(GeneratedExpression destination, GeneratedExpression value) { Destination = destination; Value = value; }
        public string RenderScl() { return Destination.RenderScl() + " := " + Value.RenderScl() + ";"; }
    }

    public sealed class GeneratedArgument
    {
        public string Name { get; }
        public string Datatype { get; }
        public GeneratedPinDirection Direction { get; }
        public GeneratedExpression Expression { get; }
        internal GeneratedArgument(string name, string datatype, GeneratedPinDirection direction, GeneratedExpression expression)
        { Name = name; Datatype = datatype; Direction = direction; Expression = expression; }
    }

    public sealed class GeneratedInvocation
    {
        public string Identity { get; }
        public string BlockType { get; }
        public SymbolReference Instance { get; }
        public GeneratedExpression Enable { get; }
        public ReadOnlyCollection<GeneratedArgument> Arguments { get; }
        public ReadOnlyCollection<GeneratedAssignment> FollowingAssignments { get; }
        internal GeneratedInvocation(string identity, string type, SymbolReference instance, GeneratedExpression enable,
            IEnumerable<GeneratedArgument> arguments, IEnumerable<GeneratedAssignment> assignments)
        {
            Identity = identity; BlockType = type; Instance = instance; Enable = enable;
            Arguments = arguments.ToList().AsReadOnly(); FollowingAssignments = assignments.ToList().AsReadOnly();
        }
    }

    public static class SiemensSource
    {
        public static string Identifier(string value)
        {
            // Always quote symbols: keywords and punctuation need no separate spelling policy.
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128 || value.Any(c => char.IsControl(c) || c == '"'))
                throw new InvalidOperationException("Unsupported PLC symbol '" + value + "'.");
            return "\"" + value + "\"";
        }
        public static string Reference(SymbolReference reference)
        {
            return (reference.IsGlobal ? "" : "#") + string.Join(".", reference.Components.Select(Identifier));
        }
        public static string RegionTitle(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl) || value.Contains("//") || value.Contains("(*") || value.Contains("*)"))
                throw new InvalidOperationException("Unsupported region title '" + value + "'.");
            return value;
        }
    }

    // Adapter for the generator's supported FB/A/O network grammar, not a manual-code
    // decompiler. The owner must establish provenance before calling this adapter.
    // Both renderers use this validated snapshot; neither edits the caller's XML.
    public sealed class GeneratedNetworkModel
    {
        readonly XElement fbd;
        public string Identity { get; }
        public string Title { get; }
        public ReadOnlyCollection<GeneratedInvocation> Invocations { get; }
        public ReadOnlyCollection<string> Reads { get; }
        public ReadOnlyCollection<string> Writes { get; }
        GeneratedNetworkModel(string identity, string title, XElement original, IEnumerable<GeneratedInvocation> calls,
            IEnumerable<string> reads, IEnumerable<string> writes)
        {
            Identity = identity; Title = SiemensSource.RegionTitle(title); fbd = new XElement(original);
            Invocations = calls.ToList().AsReadOnly();
            Reads = reads.Distinct(StringComparer.OrdinalIgnoreCase).ToList().AsReadOnly();
            Writes = writes.Distinct(StringComparer.OrdinalIgnoreCase).ToList().AsReadOnly();
        }
        public XElement RenderFbd() { return new XElement(fbd); }
        public string RenderScl()
        {
            var source = new StringBuilder();
            source.AppendLine("REGION " + Title);
            foreach (var call in Invocations)
            {
                bool enabled = call.Enable.Kind != GeneratedExpressionKind.Literal || call.Enable.Literal != "TRUE";
                if (enabled) source.AppendLine("    IF " + call.Enable.RenderScl() + " THEN");
                string indent = enabled ? "        " : "    ";
                source.Append(indent).Append(SiemensSource.Reference(call.Instance)).Append('(');
                source.Append(string.Join(", ", call.Arguments.Select(a => SiemensSource.Identifier(a.Name) +
                    (a.Direction == GeneratedPinDirection.Output ? " => " : " := ") + a.Expression.RenderScl())));
                source.AppendLine(");");
                if (enabled) source.AppendLine("    END_IF;");
                // Copying an FB output is distinct from executing the stateful FB. Fan-out
                // and negated destinations must never result in another invocation.
                foreach (var assignment in call.FollowingAssignments) source.AppendLine("    " + assignment.RenderScl());
            }
            source.AppendLine("END_REGION");
            return source.ToString();
        }

        public static GeneratedNetworkModel FromGeneratedFbd(string identity, string title, XElement compileUnit, IEnumerable<string> allowedBlockTypes)
        {
            return new Reader(identity, title, compileUnit, allowedBlockTypes).Build();
        }

        sealed class Reader
        {
            readonly string identity, title;
            readonly XElement original;
            readonly Dictionary<string, XElement> nodes;
            readonly XElement[] wires;
            readonly XElement[] calls;
            readonly HashSet<string> allowed;
            readonly List<string> reads = new List<string>(), writes = new List<string>();
            readonly Dictionary<string, HashSet<string>> dependencies = new Dictionary<string, HashSet<string>>();
            readonly HashSet<string> evaluating = new HashSet<string>();
            readonly HashSet<XElement> consumedWires = new HashSet<XElement>();
            string currentCall;
            InvalidOperationException Invalid(string detail) { return new InvalidOperationException("Generated network '" + identity + "' (" + title + "): " + detail + ". Preserve and reconcile unsupported/manual logic explicitly."); }
            static IEnumerable<XElement> Children(XElement e, string name) { return e.Elements().Where(n => n.Name.LocalName == name); }
            static string Attr(XElement e, string name) { return (string)e.Attribute(name); }
            static string Key(XElement e) { return Attr(e, "UId"); }
            public Reader(string identity, string title, XElement xml, IEnumerable<string> allowedTypes)
            {
                this.identity = identity; this.title = title; original = new XElement(xml);
                allowed = new HashSet<string>(allowedTypes, StringComparer.Ordinal);
                var sources = original.Descendants().Where(e => e.Name.LocalName == "FlgNet").ToArray();
                if (sources.Length != 1) throw Invalid("expected one FBD FlgNet body");
                var source = sources[0];
                var parts = Children(source, "Parts").Single();
                if (source.Elements().Any(e => e.Name.LocalName != "Parts" && e.Name.LocalName != "Wires")) throw Invalid("unknown FlgNet content");
                var elements = parts.Elements().ToArray();
                if (elements.Any(e => e.Name.LocalName != "Access" && e.Name.LocalName != "Call" && e.Name.LocalName != "Part")) throw Invalid("unknown generated part");
                if (elements.Any(e => Key(e) == null) || elements.Select(Key).Distinct().Count() != elements.Length) throw Invalid("duplicate/missing part identity");
                nodes = elements.ToDictionary(Key);
                wires = Children(source, "Wires").Single().Elements().ToArray();
                calls = elements.Where(e => e.Name.LocalName == "Call").ToArray();
                foreach (var call in calls) dependencies.Add(Key(call), new HashSet<string>());
                foreach (var logic in elements.Where(e => e.Name.LocalName == "Part"))
                    if (!new[] { "A", "O" }.Contains(Attr(logic, "Name"))) throw Invalid("unsupported instruction " + Attr(logic, "Name"));
                foreach (var wire in wires)
                {
                    if (wire.Name.LocalName != "Wire" || wire.Elements().Any(e => !new[] { "IdentCon", "NameCon", "OpenCon", "Powerrail" }.Contains(e.Name.LocalName)))
                        throw Invalid("unknown wire/connector");
                    foreach (var endpoint in wire.Elements().Where(e => e.Name.LocalName == "NameCon" || e.Name.LocalName == "IdentCon"))
                    {
                        if (!nodes.ContainsKey(Key(endpoint))) throw Invalid("dangling wire endpoint " + Key(endpoint));
                        var node = nodes[Key(endpoint)];
                        if (endpoint.Name.LocalName == "IdentCon" && node.Name.LocalName != "Access") throw Invalid("IdentCon is not an access");
                        if (endpoint.Name.LocalName != "NameCon") continue;
                        string pin = Attr(endpoint, "Name");
                        if (node.Name.LocalName == "Call")
                        {
                            if (Parameter(node, pin) == null && !string.Equals(pin, "en", StringComparison.OrdinalIgnoreCase)) throw Invalid("unknown call pin " + pin);
                        }
                        else if (node.Name.LocalName == "Part")
                        {
                            int index, count;
                            var card = Children(node, "TemplateValue").SingleOrDefault(e => Attr(e, "Name") == "Card");
                            if (pin != "out" && (pin == null || !pin.StartsWith("in", StringComparison.Ordinal) || !int.TryParse(pin.Substring(2), out index) ||
                                card == null || !int.TryParse(card.Value, out count) || index < 1 || index > count)) throw Invalid("unknown Boolean pin " + pin);
                        }
                        else throw Invalid("NameCon is not a call/Boolean instruction");
                    }
                }
            }
            XElement Info(XElement call) { return Children(call, "CallInfo").Single(); }
            XElement Parameter(XElement call, string pin)
            {
                return Children(Info(call), "Parameter").SingleOrDefault(p => string.Equals(Attr(p, "Name"), pin, StringComparison.OrdinalIgnoreCase));
            }
            bool Negated(XElement node, string pin)
            {
                return Children(node, "Negated").Any(n => string.Equals(Attr(n, "Name"), pin, StringComparison.OrdinalIgnoreCase));
            }
            XElement[] Connections(string uid, string pin)
            {
                return wires.Where(w => w.Elements().Any(e => e.Name.LocalName == "NameCon" && Key(e) == uid &&
                    string.Equals(Attr(e, "Name"), pin, StringComparison.OrdinalIgnoreCase))).ToArray();
            }
            SymbolReference Instance(XElement call)
            {
                var info = Info(call);
                if (Attr(info, "BlockType") != "FB" || !allowed.Contains(Attr(info, "Name"))) throw Invalid("unsupported or unregistered call " + Attr(info, "Name"));
                var instance = Children(info, "Instance").Single();
                var scope = Attr(instance, "Scope");
                if (scope != "LocalVariable" && scope != "GlobalVariable") throw Invalid("unsupported instance scope " + scope);
                return new SymbolReference(scope == "GlobalVariable", Children(instance, "Component").Select(c => Attr(c, "Name")).ToArray());
            }
            GeneratedExpression Access(XElement access, bool destination)
            {
                string scope = Attr(access, "Scope");
                if (scope == "LiteralConstant")
                {
                    if (destination) throw Invalid("constant is used as a destination");
                    var constant = Children(access, "Constant").Single();
                    string type = Children(constant, "ConstantType").Single().Value;
                    string value = Children(constant, "ConstantValue").Single().Value;
                    if (type == "Bool" && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("false", StringComparison.OrdinalIgnoreCase))) return GeneratedExpression.Constant(type, value.ToUpperInvariant());
                    if (type == "String" && !value.Any(char.IsControl) && Regex.IsMatch(value, @"\A'(?:[^'$]|\$(?:[LNPRT$'""]|[0-9A-Fa-f]{2}))*'\z"))
                        return GeneratedExpression.Constant(type, value);
                    // Reject source text rather than embedding unvalidated literal strings.
                    if (!Regex.IsMatch(type, @"\A(?:U?S?Int|D?Int|U?D?Word|Byte|L?Real|Time)\z", RegexOptions.IgnoreCase) ||
                        !Regex.IsMatch(value, @"\A[+-]?(?:[0-9]+(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?|(?:[A-Za-z0-9]+#)[A-Za-z0-9_.+-]+)\z"))
                        throw Invalid("unsupported literal " + type + " / " + value);
                    return GeneratedExpression.Constant(type, value);
                }
                if (scope != "LocalVariable" && scope != "GlobalVariable") throw Invalid("unsupported access scope " + scope);
                var symbol = Children(access, "Symbol").Single();
                if (symbol.Elements().Any(e => e.Name.LocalName != "Component") || access.Elements().Any(e => e.Name.LocalName != "Symbol"))
                    throw Invalid("unsupported structured access");
                var components = Children(symbol, "Component").ToArray();
                if (components.Any(c => c.HasElements || c.Attributes().Any(a => a.Name.LocalName != "Name"))) throw Invalid("unsupported indexed/bit/typed access");
                var expression = GeneratedExpression.Reference(new SymbolReference(scope == "GlobalVariable", components.Select(c => Attr(c, "Name")).ToArray()));
                (destination ? writes : reads).Add(expression.RenderScl());
                return expression;
            }
            bool IsOutput(XElement endpoint)
            {
                if (endpoint.Name.LocalName != "NameCon") return false;
                var node = nodes[Key(endpoint)];
                if (node.Name.LocalName == "Part") return string.Equals(Attr(endpoint, "Name"), "out", StringComparison.OrdinalIgnoreCase);
                if (node.Name.LocalName != "Call") throw Invalid("NameCon does not refer to an instruction");
                var parameter = Parameter(node, Attr(endpoint, "Name"));
                return parameter != null && Attr(parameter, "Section") == "Output";
            }
            GeneratedExpression WireValue(XElement wire)
            {
                var outputs = wire.Elements().Where(IsOutput).ToArray();
                if (outputs.Length > 1) throw Invalid("multiple output drivers");
                if (outputs.Length == 1)
                {
                    var output = outputs[0]; var node = nodes[Key(output)];
                    string pin = Attr(output, "Name");
                    if (node.Name.LocalName == "Part") return Logic(node);
                    string producer = Key(node);
                    if (producer == currentCall) throw Invalid("a call output feeds its own input");
                    dependencies[currentCall].Add(producer);
                    var expression = Output(node, pin);
                    return Negated(node, pin) ? GeneratedExpression.Boolean(GeneratedExpressionKind.Not, expression) : expression;
                }
                var identifiers = Children(wire, "IdentCon").ToArray();
                if (identifiers.Length == 1) return Access(nodes[Key(identifiers[0])], false);
                if (identifiers.Length == 0 && Children(wire, "Powerrail").Count() == 1) return GeneratedExpression.Constant("Bool", "TRUE");
                throw Invalid("missing/multiple source operands");
            }
            GeneratedExpression Output(XElement node, string pin)
            {
                var instance = Instance(node);
                return GeneratedExpression.Reference(new SymbolReference(instance.IsGlobal, instance.Components.Concat(new[] { pin }).ToArray()));
            }
            GeneratedExpression Input(XElement node, string pin, bool enable = false)
            {
                var connections = Connections(Key(node), pin);
                if (connections.Length == 0) return null;
                if (connections.Length != 1) throw Invalid("multiple wires at " + Key(node) + "." + pin);
                var wire = connections[0];
                consumedWires.Add(wire);
                if (Children(wire, "OpenCon").Any())
                {
                    if (wire.Elements().Count() != 2) throw Invalid("shared open connector");
                    if (!enable) return null;
                    var openEnable = GeneratedExpression.Constant("Bool", "TRUE");
                    return Negated(node, pin) ? GeneratedExpression.Boolean(GeneratedExpressionKind.Not, openEnable) : openEnable;
                }
                var expression = WireValue(wire);
                return Negated(node, pin) ? GeneratedExpression.Boolean(GeneratedExpressionKind.Not, expression) : expression;
            }
            GeneratedExpression Logic(XElement node)
            {
                if (!evaluating.Add(Key(node))) throw Invalid("cyclic Boolean expression");
                try
                {
                    var card = Children(node, "TemplateValue").SingleOrDefault(e => Attr(e, "Name") == "Card");
                    int count;
                    if (card == null || !int.TryParse(card.Value, out count) || count < 1) throw Invalid("invalid Boolean cardinality");
                    var terms = Enumerable.Range(1, count).Select(i => Input(node, "in" + i) ?? throw Invalid("unconnected Boolean input")).ToArray();
                    return GeneratedExpression.Boolean(Attr(node, "Name") == "A" ? GeneratedExpressionKind.And : GeneratedExpressionKind.Or, terms);
                }
                finally { evaluating.Remove(Key(node)); }
            }
            public GeneratedNetworkModel Build()
            {
                var invocations = new Dictionary<string, GeneratedInvocation>();
                var instanceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var call in calls)
                {
                    currentCall = Key(call);
                    var instance = Instance(call);
                    string instanceName = SiemensSource.Reference(instance);
                    if (!instanceNames.Add(instanceName)) throw Invalid("repeated stateful instance " + instanceName);
                    // The instance itself represents state: grouping may not reverse a
                    // reader/writer of that instance even if no explicit output wire exists.
                    reads.Add(instanceName); writes.Add(instanceName);
                    var arguments = new List<GeneratedArgument>();
                    var assignments = new List<GeneratedAssignment>();
                    var enable = Input(call, "en", true) ?? GeneratedExpression.Constant("Bool", "TRUE");
                    foreach (var parameter in Children(Info(call), "Parameter"))
                    {
                        string pin = Attr(parameter, "Name"), type = Attr(parameter, "Type");
                        if (string.Equals(pin, "en", StringComparison.OrdinalIgnoreCase)) continue;
                        GeneratedPinDirection direction;
                        if (!Enum.TryParse(Attr(parameter, "Section"), out direction) || !Enum.IsDefined(typeof(GeneratedPinDirection), direction)) throw Invalid("unsupported parameter direction");
                        if (direction == GeneratedPinDirection.Output)
                        {
                            var connections = Connections(Key(call), pin);
                            if (connections.Length > 1) throw Invalid("multiple output wires at " + instanceName + "." + pin);
                            if (connections.Length == 1) consumedWires.Add(connections[0]);
                            if (connections.Length == 0 || Children(connections[0], "OpenCon").Any()) continue;
                            var wire = connections[0];
                            consumedWires.Add(wire);
                            if (wire.Elements().Where(IsOutput).Count() != 1) throw Invalid("ambiguous output wire");
                            foreach (var id in Children(wire, "IdentCon"))
                            {
                                var destination = Access(nodes[Key(id)], true);
                                var output = Output(call, pin);
                                assignments.Add(new GeneratedAssignment(destination, Negated(call, pin) ? GeneratedExpression.Boolean(GeneratedExpressionKind.Not, output) : output));
                            }
                        }
                        else
                        {
                            var expression = Input(call, pin);
                            if (expression == null) continue;
                            if (direction == GeneratedPinDirection.InOut)
                            {
                                var wire = Connections(Key(call), pin).Single();
                                var ids = Children(wire, "IdentCon").ToArray();
                                if (ids.Length != 1 || wire.Elements().Any(IsOutput) || Negated(call, pin)) throw Invalid("InOut requires an unnegated writable symbol");
                                expression = Access(nodes[Key(ids[0])], true);
                            }
                            arguments.Add(new GeneratedArgument(pin, type, direction, expression));
                        }
                    }
                    invocations.Add(Key(call), new GeneratedInvocation(identity + "/" + Key(call), Attr(Info(call), "Name"), instance, enable, arguments, assignments));
                }
                if (wires.Any(w => !consumedWires.Contains(w))) throw Invalid("wire is outside the supported call/Boolean grammar");
                var ordered = new List<GeneratedInvocation>();
                var remaining = calls.Select(Key).ToList();
                var completed = new HashSet<string>();
                while (remaining.Count != 0)
                {
                    string next = remaining.FirstOrDefault(id => dependencies[id].All(completed.Contains));
                    if (next == null) throw Invalid("cyclic call dependencies");
                    ordered.Add(invocations[next]); completed.Add(next); remaining.Remove(next);
                }
                return new GeneratedNetworkModel(identity, title, original, ordered, reads, writes);
            }
        }
    }
}
