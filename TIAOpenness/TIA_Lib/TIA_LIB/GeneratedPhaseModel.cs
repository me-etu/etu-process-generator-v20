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
        internal GeneratedRegion(string title, IEnumerable<GeneratedStatement> statements, params GeneratedRegion[] children)
        { Title = title; Statements = statements.ToList().AsReadOnly(); Children = Array.AsReadOnly(children); }
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
    // Authoritative model of the audited NEW EmptyPhase template. It never parses or
    // converts an existing manual phase. Branches with no effect and IF FALSE bodies
    // are omitted; all stateful calls and conditional/sticky writes are retained.
    public sealed class GeneratedPhaseModel : GeneratedStructuredBody
    {
        public const string ModelVersion = "EmptyPhase-1";
        const string AuditedBodyHash = "7DB014371985189CB1B959225CA98DBAD84EAA7D65B5E635B5ABA74AEB5CB510";
        public static void ValidateFreshTemplate(XElement document)
        {
            var bodies = document.Descendants("SW.Blocks.CompileUnit").ToArray();
            var sources = bodies.SelectMany(b => b.Descendants().Where(e => e.Name.LocalName == "StructuredText")).ToArray();
            if (bodies.Length != 1 || sources.Length != 1 || ManagedArtifactOwnership.Fingerprint(new XElement("Document",
                new XElement("SW.Blocks.CompileUnit", new XElement(sources[0])))) != AuditedBodyHash)
                throw new InvalidOperationException("Phase body differs from audited " + ModelVersion + ". Preserve the existing/manual phase or audit the changed template before rendering it in another language.");
        }
        static SymbolReference Local(params string[] components) { return new SymbolReference(false, components); }
        static GeneratedExpression Value(string name, string type = "Bool") { return GeneratedExpression.Reference(Local(name.Split('|')), type); }
        static GeneratedExpression True() { return GeneratedExpression.Constant("Bool", "TRUE"); }
        static GeneratedExpression False() { return GeneratedExpression.Constant("Bool", "FALSE"); }
        static GeneratedExpression Integer(int value) { return GeneratedExpression.Constant("Int", value.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
        static GeneratedExpression And(params GeneratedExpression[] operands) { return GeneratedExpression.Boolean(GeneratedExpressionKind.And, operands); }
        static GeneratedExpression Or(params GeneratedExpression[] operands) { return GeneratedExpression.Boolean(GeneratedExpressionKind.Or, operands); }
        static GeneratedExpression Not(GeneratedExpression operand) { return GeneratedExpression.Boolean(GeneratedExpressionKind.Not, operand); }
        static GeneratedExpression Equal(string name, int value) { return GeneratedExpression.Compare(true, "Int", Value(name, "Int"), Integer(value)); }
        static GeneratedStatement Assign(string destination, GeneratedExpression value, GeneratedExpression condition = null, string type = "Bool")
        { return new GeneratedStatement(new GeneratedAssignment(Value(destination, type), value), condition); }
        static GeneratedStatement Bit(string destination, int bit, GeneratedExpression value, GeneratedExpression condition = null)
        { return new GeneratedStatement(new GeneratedAssignment(GeneratedExpression.Bit(Local(destination), bit), value), condition); }
        static GeneratedStatement Call(string instance, string type, bool instruction, params GeneratedArgument[] arguments)
        { return new GeneratedStatement(new GeneratedInvocation("Phase/" + instance, type, Local(instance), True(), arguments, new GeneratedAssignment[0]), instruction); }
        static GeneratedArgument Input(string name, string type, GeneratedExpression value) { return new GeneratedArgument(name, type, GeneratedPinDirection.Input, value); }
        public GeneratedPhaseModel() : base(CreateRegions()) { }
        static IEnumerable<GeneratedRegion> CreateRegions()
        {
            var commands = new List<GeneratedStatement>();
            string[] names = { "STOP", "IDLE", "HELD", "START", "ABORT", "PAUSE", "ABORTING", "STARTING", "STOPPING" };
            int[] bits = { 12, 11, 10, 16, 14, 13, 21, 20, 19 };
            for (int index = 0; index < names.Length; index++) commands.Add(Assign(names[index], GeneratedExpression.Bit(Local("InterfaceIn"), bits[index])));
            var active = Or(Value("START"), Value("PAUSE"));
            var starting = And(active, Value("STARTING"));
            var function = new List<GeneratedStatement> { Assign("step", Integer(10), starting, "Int"), Assign("count", GeneratedExpression.Constant("USInt", "0"), starting, "USInt") };
            for (int index = 1; index <= 5; index++) function.Add(Assign("Message_" + index + "|SIG", False(), starting));
            // STOP is an ELSIF: START/PAUSE retains priority over STOP and ABORT.
            function.Add(Assign("TRANSFER_READY", False(), Or(starting, And(Not(active), Value("STOP")))));
            function.Add(Assign("COMPLETED", False(), And(Not(active), Not(Value("STOP")), Value("ABORT"))));
            var step20 = new GeneratedRegion("S20 -   ", new[] { Assign("COMPLETED", True(), And(active, Equal("step", 20))) });
            var messages = Enumerable.Range(1, 5).Select(i => Call("Message_" + i, "Program_Alarm", true)).ToArray();
            var requests = Enumerable.Range(1, 5).Select(i => Call("OpRequest_" + i, "fbOpMsg", false,
                Input("MESSAGE", "Bool", And(Value("START"), Equal("step", -1))), Input("TYPE", "USInt", GeneratedExpression.Constant("USInt", "0")))).ToArray();
            var transfer = Call("TRANSFER", "fbTransfer", false, Input("BIT_READY", "Bool", And(Value("TRANSFER_READY"), Not(Value("HELD")))));
            var timer = new[] { Assign("timer|Start", False(), Or(Not(Value("START")), And(Value("START"), GeneratedExpression.Compare(false, "Int", Value("step", "Int"), Value("step_prev", "Int"))))), Call("timer", "fbTimer", false) };
            var status = new List<GeneratedStatement> {
                Bit("InterfaceOut", 24, False()), Bit("InterfaceOut", 22, Value("COMPLETED")), Bit("InterfaceOut", 23, Or(Value("STOP"), Value("IDLE"))),
                Bit("InterfaceOut", 25, Or(Enumerable.Range(1, 5).Select(i => Equal("OpRequest_" + i + "|Interface", -1)).ToArray())),
                Call("AlarmBlock", "Program_Alarm", true, Input("SIG", "Bool", Value("HELD")),
                    Input("SD_1", "Variant", Value("ALARM_BLOCK|TEXTLIST_ID", "UInt")), Input("SD_2", "Variant", Value("ALARM_BLOCK|INDEX", "USInt"))) };
            GeneratedExpression[] conditions = { Not(Value("IDLE")), Value("HELD"), Value("START"), Value("STOP"), Value("ABORT"), Value("PAUSE"), Value("COMPLETED") };
            for (int index = 0; index < conditions.Length; index++) status.Add(Bit("InterfaceInOut", 10 + index, True(), conditions[index]));
            status.Add(Assign("step_prev", Value("step", "Int"), null, "Int"));
            return new[] { new GeneratedRegion("COMMANDS", commands),
                new GeneratedRegion("FUNCTION", function, new GeneratedRegion("S10 -   ", new GeneratedStatement[0]), step20),
                new GeneratedRegion("MESSAGES", messages), new GeneratedRegion("OPERATOR REQUEST", requests),
                new GeneratedRegion("TRANSFER", new[] { transfer }), new GeneratedRegion("TIMER", timer), new GeneratedRegion("STATUS", status) };
        }
    }
}
