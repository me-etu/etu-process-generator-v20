using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.Schema;
using TIA_LIB.Generation;

public static class GeneratedPhaseModelTests
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    sealed class State
    {
        public Dictionary<string, long> Values = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        public List<string> Trace = new List<string>();
        public List<string> Writes = new List<string>();
        public long this[string name] { get { long value; return Values.TryGetValue(name, out value) ? value : 0; } set { Values[name] = value; Writes.Add(name + "=" + value); } }
        public bool Is(string name) { return this[name] != 0; }
        public State Clone() { return new State { Values = new Dictionary<string, long>(Values, StringComparer.OrdinalIgnoreCase) }; }
        public void Bit(string name, int bit, bool value) { this[name] = value ? this[name] | (1L << bit) : this[name] & ~(1L << bit); }
        public void Call(string instance, IDictionary<string, long> inputs)
        {
            foreach (var input in inputs) this[instance + "|" + input.Key] = input.Value;
            Trace.Add(instance + "(" + string.Join(",", inputs.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + p.Value)) + ")");
            // Deterministic stateful spies, not implementations of Siemens/library FBs.
            // Detect wrong call count/order and read-before/after-call timing.
            this[instance + "|calls"]++;
            if (instance.StartsWith("OpRequest_")) this[instance + "|Interface"] = this[instance + "|MESSAGE"] != 0 ? -1 : (this[instance + "|calls"] % 3);
            if (instance == "timer") this["timer|Start"] = Is("timer|Start") ? 0 : 1;
            if (instance == "TRANSFER") this["TRANSFER|ACTIVE"] = Is("TRANSFER|BIT_READY") ? 1 : 0;
        }
    }
    static string Key(SymbolReference symbol) { return string.Join("|", symbol.Components); }
    static long Evaluate(GeneratedExpression expression, State state)
    {
        switch (expression.Kind)
        {
            case GeneratedExpressionKind.Symbol: return state[Key(expression.Symbol)];
            case GeneratedExpressionKind.Bit: return (state[Key(expression.Symbol)] >> expression.BitIndex) & 1;
            case GeneratedExpressionKind.Literal: return expression.Literal == "TRUE" ? 1 : expression.Literal == "FALSE" ? 0 : long.Parse(expression.Literal);
            case GeneratedExpressionKind.Not: return Evaluate(expression.Operands[0], state) == 0 ? 1 : 0;
            case GeneratedExpressionKind.And: return expression.Operands.All(e => Evaluate(e, state) != 0) ? 1 : 0;
            case GeneratedExpressionKind.Or: return expression.Operands.Any(e => Evaluate(e, state) != 0) ? 1 : 0;
            case GeneratedExpressionKind.Equal: return Evaluate(expression.Operands[0], state) == Evaluate(expression.Operands[1], state) ? 1 : 0;
            case GeneratedExpressionKind.NotEqual: return Evaluate(expression.Operands[0], state) != Evaluate(expression.Operands[1], state) ? 1 : 0;
            default: throw new Exception("Unsupported model expression.");
        }
    }
    static void Execute(GeneratedRegion region, State state)
    {
        foreach (var statement in region.Statements)
        {
            if (statement.Assignment != null)
            {
                if (statement.Condition != null && Evaluate(statement.Condition, state) == 0) continue;
                var destination = statement.Assignment.Destination;
                long value = Evaluate(statement.Assignment.Value, state);
                if (destination.Kind == GeneratedExpressionKind.Bit) state.Bit(Key(destination.Symbol), destination.BitIndex, value != 0);
                else state[Key(destination.Symbol)] = value;
            }
            else state.Call(Key(statement.Call.Instance), statement.Call.Arguments.ToDictionary(a => a.Name, a => Evaluate(a.Expression, state)));
        }
        foreach (var child in region.Children) Execute(child, state);
    }
    static void Oracle(State s)
    {
        // Independent imperative transcription of the original template, preserving its
        // IF/ELSIF priority, CASE, sticky status bits and exact stateful call sequence.
        var commandBits = new Dictionary<string, int> { { "STOP", 12 }, { "IDLE", 11 }, { "HELD", 10 }, { "START", 16 }, { "ABORT", 14 }, { "PAUSE", 13 }, { "ABORTING", 21 }, { "STARTING", 20 }, { "STOPPING", 19 } };
        foreach (var command in commandBits) s[command.Key] = (s["InterfaceIn"] >> command.Value) & 1;
        if (s.Is("START") || s.Is("PAUSE"))
        {
            if (s.Is("STARTING"))
            {
                s["step"] = 10; s["count"] = 0;
                for (int i = 1; i <= 5; i++) s["Message_" + i + "|SIG"] = 0;
                s["TRANSFER_READY"] = 0;
            }
            switch (s["step"])
            {
                case 10: break; // original IF FALSE placeholder is unreachable
                case 20: s["COMPLETED"] = 1; break;
            }
        }
        else if (s.Is("STOP")) s["TRANSFER_READY"] = 0;
        else if (s.Is("ABORT")) s["COMPLETED"] = 0;
        for (int i = 1; i <= 5; i++) s.Call("Message_" + i, new Dictionary<string, long>());
        for (int i = 1; i <= 5; i++) s.Call("OpRequest_" + i, new Dictionary<string, long> { { "MESSAGE", s.Is("START") && s["step"] == -1 ? 1 : 0 }, { "TYPE", 0 } });
        s.Call("TRANSFER", new Dictionary<string, long> { { "BIT_READY", s.Is("TRANSFER_READY") && !s.Is("HELD") ? 1 : 0 } });
        if (!s.Is("START") || s.Is("START") && s["step"] != s["step_prev"]) s["timer|Start"] = 0;
        s.Call("timer", new Dictionary<string, long>());
        s.Bit("InterfaceOut", 24, false); s.Bit("InterfaceOut", 22, s.Is("COMPLETED")); s.Bit("InterfaceOut", 23, s.Is("STOP") || s.Is("IDLE"));
        s.Bit("InterfaceOut", 25, Enumerable.Range(1, 5).Any(i => s["OpRequest_" + i + "|Interface"] == -1));
        s.Call("AlarmBlock", new Dictionary<string, long> { { "SIG", s["HELD"] }, { "SD_1", s["ALARM_BLOCK|TEXTLIST_ID"] }, { "SD_2", s["ALARM_BLOCK|INDEX"] } });
        if (!s.Is("IDLE")) s.Bit("InterfaceInOut", 10, true);
        if (s.Is("HELD")) s.Bit("InterfaceInOut", 11, true);
        if (s.Is("START")) s.Bit("InterfaceInOut", 12, true);
        if (s.Is("STOP")) s.Bit("InterfaceInOut", 13, true);
        if (s.Is("ABORT")) s.Bit("InterfaceInOut", 14, true);
        if (s.Is("PAUSE")) s.Bit("InterfaceInOut", 15, true);
        if (s.Is("COMPLETED")) s.Bit("InterfaceInOut", 16, true);
        s["step_prev"] = s["step"];
    }
    sealed class Fbd
    {
        readonly Dictionary<int, XElement> parts;
        readonly XElement[] wires;
        readonly State state;
        static int Id(XElement e) { return (int)e.Attribute("UId"); }
        static IEnumerable<XElement> Named(XElement e, string name) { return e.Elements().Where(c => c.Name.LocalName == name); }
        public Fbd(XElement network, State state)
        {
            this.state = state;
            var graph = network.Descendants().Single(e => e.Name.LocalName == "FlgNet");
            parts = Named(graph, "Parts").Single().Elements().ToDictionary(Id);
            wires = Named(graph, "Wires").SelectMany(w => w.Elements()).ToArray();
        }
        long Access(XElement access)
        {
            if ((string)access.Attribute("Scope") == "LiteralConstant")
            {
                string value = access.Descendants().Single(e => e.Name.LocalName == "ConstantValue").Value;
                return value == "TRUE" ? 1 : value == "FALSE" ? 0 : long.Parse(value);
            }
            var components = access.Descendants().Where(e => e.Name.LocalName == "Component").ToArray();
            string key = string.Join("|", components.Select(c => (string)c.Attribute("Name")));
            var slice = (string)components.Last().Attribute("SliceAccessModifier");
            return slice == null ? state[key] : (state[key] >> int.Parse(slice.Substring(1))) & 1;
        }
        void Write(XElement access, long value)
        {
            var components = access.Descendants().Where(e => e.Name.LocalName == "Component").ToArray();
            string key = string.Join("|", components.Select(c => (string)c.Attribute("Name")));
            var slice = (string)components.Last().Attribute("SliceAccessModifier");
            if (slice == null) state[key] = value; else state.Bit(key, int.Parse(slice.Substring(1)), value != 0);
        }
        XElement Wire(int id, string pin) { return wires.Single(w => Named(w, "NameCon").Any(n => Id(n) == id && (string)n.Attribute("Name") == pin)); }
        long Pin(int id, string pin)
        {
            var wire = Wire(id, pin);
            var access = Named(wire, "IdentCon").SingleOrDefault();
            long value;
            if (access != null) value = Access(parts[Id(access)]);
            else if (Named(wire, "Powerrail").Any()) value = 1;
            else
            {
                var producer = Named(wire, "NameCon").Single(n => Id(n) != id);
                var node = parts[Id(producer)];
                string kind = (string)node.Attribute("Name");
                int count = kind == "Eq" || kind == "Ne" ? 2 : int.Parse(Named(node, "TemplateValue").Single(t => (string)t.Attribute("Name") == "Card").Value);
                var values = Enumerable.Range(1, count).Select(i => Pin(Id(node), "in" + i)).ToArray();
                switch (kind)
                {
                    case "A": value = values.All(v => v != 0) ? 1 : 0; break;
                    case "O": value = values.Any(v => v != 0) ? 1 : 0; break;
                    case "Eq": value = values[0] == values[1] ? 1 : 0; break;
                    case "Ne": value = values[0] != values[1] ? 1 : 0; break;
                    default: throw new Exception("Unexpected FBD producer " + kind);
                }
            }
            return Named(parts[id], "Negated").Any(n => (string)n.Attribute("Name") == pin) ? (value == 0 ? 1 : 0) : value;
        }
        public void Execute()
        {
            var executable = parts.Values.Where(p => p.Name.LocalName == "Call" || p.Name.LocalName == "Part" && !new[] { "A", "O", "Eq", "Ne" }.Contains((string)p.Attribute("Name"))).ToArray();
            Check(executable.Length <= 1, "Statements share an FBD execution network.");
            if (executable.Length == 0) return;
            var node = executable[0]; int id = Id(node); string instruction = (string)node.Attribute("Name");
            if (instruction == "Coil" || instruction == "SCoil" || instruction == "RCoil" || instruction == "Move")
            {
                if (instruction == "Move" && Pin(id, "en") == 0) return;
                long value = Pin(id, "in");
                if ((instruction == "SCoil" || instruction == "RCoil") && value == 0) return;
                if (instruction == "RCoil") value = 0;
                var destination = Named(Wire(id, instruction == "Move" ? "out1" : "operand"), "IdentCon").Single();
                Write(parts[Id(destination)], value); return;
            }
            var instance = node.Descendants().Single(e => e.Name.LocalName == "Instance");
            string name = instance.Descendants().Single(e => e.Name.LocalName == "Component").Attribute("Name").Value;
            var inputPins = wires.SelectMany(w => Named(w, "NameCon")).Where(n => Id(n) == id && (string)n.Attribute("Name") != "en").Select(n => (string)n.Attribute("Name")).ToArray();
            state.Call(name, inputPins.ToDictionary(p => p, p => Pin(id, p)));
        }
    }
    static void Equivalent(State expected, State actual, string context)
    {
        Check(expected.Values.Count == actual.Values.Count && expected.Values.All(p => actual[p.Key] == p.Value) && expected.Trace.SequenceEqual(actual.Trace) && expected.Writes.SequenceEqual(actual.Writes), "Phase semantic mismatch: " + context);
    }
    public static void Run(string root)
    {
        var model = new GeneratedPhaseModel();
        var fbd = new PhaseFbdRenderer().Render(model);
        var scl = new StructuredTextBodyRenderer().Render(model);
        Check(Regex.Replace(GeneratedNetworkModelTests.ReadStructuredText(scl), @"\s+", "") == Regex.Replace(model.RenderScl(), @"\s+", ""), "Phase SCL readable/token parity differs.");
        var template = XElement.Load(Path.Combine(root, "TIAOpenness/TIA_Lib/TIA_LIB/Xml/EmptyPhase.xml"));
        GeneratedPhaseModel.ValidateFreshTemplate(template);
        var changedTemplate = new XElement(template);
        changedTemplate.Descendants().First(e => e.Name.LocalName == "Token" && (string)e.Attribute("Text") == "OR").SetAttributeValue("Text", "AND");
        try { GeneratedPhaseModel.ValidateFreshTemplate(changedTemplate); throw new Exception("Changed/manual phase template was converted."); }
        catch (InvalidOperationException) { }
        var originalTitles = template.Descendants().Where(e => e.Name.LocalName == "StructuredText").Single().Elements().Where(e => e.Name.LocalName == "Text").Select(e => e.Value).ToArray();
        var emittedTitles = scl.Elements().Where(e => e.Name.LocalName == "Text").Select(e => e.Value).ToArray();
        Check(originalTitles.SequenceEqual(emittedTitles), "Phase region labels/order differ from the template.");
        var schema = new XmlSchemaSet();
        const string ns = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5";
        schema.Add(ns, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Siemens/Automation/Portal V21/PublicAPI/V21/Schemas/SW.PlcBlocks.LADFBD_v5.xsd"));
        foreach (var network in fbd) new XDocument(new XElement(network.Descendants(XName.Get("FlgNet", ns)).Single())).Validate(schema, (sender, error) => { throw error.Exception; });
        Check(!fbd.Any(n => n.Descendants().Any(e => e.Name.LocalName == "StructuredText")), "FBD phase contains SCL fallback.");
        var random = new Random(17);
        var interlockDescriptor = new TIA_LIB.OperationInterlockDescriptor("A2", "OP1", new[] { "PH-1", "PH-2" });
        var interlockModel = new GeneratedInterlockModel(interlockDescriptor);
        var interlockFbd = new PhaseFbdRenderer().Render(interlockModel);
        var interlockState = new State();
        foreach (var member in interlockDescriptor.BoolMembers) interlockState[member] = 1;
        foreach (var network in interlockFbd) new Fbd(network, interlockState).Execute();
        Check(interlockDescriptor.BoolMembers.All(m => interlockState[m] == 0) && interlockState.Trace.Count == 0, "FBD interlock starter semantics differ from G-014 SCL.");
        Check(interlockModel.Regions.Select(r => r.Title).SequenceEqual(new[] { "Function", "General Interlocks", "PH-1 Interlocks", "PH-2 Interlocks" }), "Interlock region order differs.");
        int[] bits = { 12, 11, 10, 16, 14, 13, 21, 20, 19 };
        int cases = 0;
        for (int commands = 0; commands < 512; commands++)
        foreach (int step in new[] { -1, 0, 10, 20 })
        {
            var initial = new State();
            for (int index = 0; index < bits.Length; index++) initial.Bit("InterfaceIn", bits[index], (commands & (1 << index)) != 0);
            initial["step"] = step; initial["step_prev"] = random.Next(2) == 0 ? step : 10; initial["count"] = random.Next(256);
            initial["COMPLETED"] = random.Next(2); initial["TRANSFER_READY"] = random.Next(2); initial["timer|Start"] = random.Next(2);
            initial["InterfaceOut"] = random.Next(); initial["InterfaceInOut"] = random.Next(); initial["ALARM_BLOCK|TEXTLIST_ID"] = 519; initial["ALARM_BLOCK|INDEX"] = 7;
            for (int i = 1; i <= 5; i++) initial["Message_" + i + "|SIG"] = random.Next(2);
            var expected = initial.Clone(); var neutral = initial.Clone(); var rendered = initial.Clone();
            for (int scan = 0; scan < 2; scan++)
            {
                Oracle(expected);
                foreach (var region in model.Regions) Execute(region, neutral);
                foreach (var network in fbd) new Fbd(network, rendered).Execute();
                Equivalent(expected, neutral, commands + "/" + step + "/" + scan + "/model");
                Equivalent(expected, rendered, commands + "/" + step + "/" + scan + "/FBD");
                cases++;
            }
        }
        Console.WriteLine("PASS: " + cases + " phase scans against an independent template oracle and emitted FBD interpreter; branches, steps, sticky bits, call order, timer/alarm arguments, exact labels and FlgNet XSD. PLC instruction import/runtime is not tested.");
    }
}
