using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TIA_LIB.Xml;

namespace TIA_LIB.SignalStaging
{
    public class SignalStagingInventory
    {
        private readonly Dictionary<string, UnitSignals> _units = new Dictionary<string, UnitSignals>();

        public IEnumerable<UnitSignals> Units
        {
            get { return _units.Values.OrderBy(unit => unit.SafeName); }
        }

        public void AddInput(string unitName, string signalName, string datatype, string comment = "")
        {
            Add(unitName, SignalDirection.Input, signalName, datatype, comment);
        }

        public void AddOutput(string unitName, string signalName, string datatype, string comment = "")
        {
            Add(unitName, SignalDirection.Output, signalName, datatype, comment);
        }

        public void Add(string unitName, SignalDirection direction, string signalName, string datatype, string comment = "")
        {
            var safeUnit = SafeName(unitName);
            UnitSignals unit;
            if (!_units.TryGetValue(safeUnit, out unit))
            {
                unit = new UnitSignals(unitName, safeUnit);
                _units.Add(safeUnit, unit);
            }

            unit.Add(direction, signalName, datatype, comment);
        }

        public IEnumerable<XmlUdt> CreateUdts()
        {
            foreach (var unit in Units)
            {
                yield return new XmlUdt("hwIN_" + unit.SafeName, unit.Inputs.Select(signal => signal.ToXmlMember()));
                yield return new XmlUdt("hwOUT_" + unit.SafeName, unit.Outputs.Select(signal => signal.ToXmlMember()));
            }
        }

        public XmlDatablock CreateDbIo()
        {
            var db = new XmlDatablock("dbIO");

            foreach (var unit in Units)
            {
                var unitMember = new XmlDataMember(unit.SafeName, "Struct", "Generated hardware IO staging for " + unit.OriginalName);
                unitMember.Children.Add(new XmlDataMember("IN", "hwIN_" + unit.SafeName));
                unitMember.Children.Add(new XmlDataMember("OUT", "hwOUT_" + unit.SafeName));
                db.SetMember(unitMember);
            }

            return db;
        }

        public void LogPlan()
        {
            Console.WriteLine("Signal staging mode: Generated DB/UDT staging");
            Console.WriteLine("Will generate DB: dbIO");

            var udts = Units
                .SelectMany(unit => new[] { "hwIN_" + unit.SafeName, "hwOUT_" + unit.SafeName })
                .ToList();
            Console.WriteLine("Will generate UDTs: " + string.Join(", ", udts));
            Console.WriteLine("Will add unit interfaces: hwIN, hwOUT");

            foreach (var unit in Units)
            {
                Console.WriteLine("Will wire Plant call fb" + unit.OriginalName + ": hwIN := dbIO." + unit.SafeName + ".IN, hwOUT := dbIO." + unit.SafeName + ".OUT");
            }

            Console.WriteLine("Will use unit-local references: hwIN.<SafeMember>, hwOUT.<SafeMember>");
            Console.WriteLine("Existing generated names are imported with override only for those exact DB/UDT artifact names; unrelated DBs/UDTs are not deleted.");
        }

        public static SignalReference InputReference(string signalName)
        {
            if (PlcProject.StagingMode == SignalStagingMode.MarkerMemory)
            {
                return new SignalReference(signalName, true);
            }

            return new SignalReference("hwIN|" + SafeMemberName(signalName), false);
        }

        public static SignalReference OutputReference(string signalName)
        {
            if (PlcProject.StagingMode == SignalStagingMode.MarkerMemory)
            {
                return new SignalReference(signalName, true);
            }

            return new SignalReference("hwOUT|" + SafeMemberName(signalName), false);
        }

        public static string SafeMemberName(string signalName)
        {
            string member = signalName;

            if (member.StartsWith("IN_", StringComparison.OrdinalIgnoreCase))
            {
                member = member.Substring(3);
            }
            else if (member.StartsWith("CTRL_", StringComparison.OrdinalIgnoreCase))
            {
                member = member.Substring(5);
            }

            return SafeName(member);
        }

        public static string SafeName(string name)
        {
            string safe = Regex.Replace(name ?? "", "[^A-Za-z0-9_]", "_");
            safe = Regex.Replace(safe, "_+", "_").Trim('_');

            if (string.IsNullOrWhiteSpace(safe))
            {
                safe = "Signal";
            }

            if (!char.IsLetter(safe[0]) && safe[0] != '_')
            {
                safe = "_" + safe;
            }

            return safe;
        }

        public class UnitSignals
        {
            private readonly Dictionary<string, HardwareSignal> _inputs = new Dictionary<string, HardwareSignal>();
            private readonly Dictionary<string, HardwareSignal> _outputs = new Dictionary<string, HardwareSignal>();

            public UnitSignals(string originalName, string safeName)
            {
                OriginalName = originalName;
                SafeName = safeName;
            }

            public string OriginalName { get; private set; }
            public string SafeName { get; private set; }
            public IEnumerable<HardwareSignal> Inputs { get { return _inputs.Values.OrderBy(signal => signal.MemberName); } }
            public IEnumerable<HardwareSignal> Outputs { get { return _outputs.Values.OrderBy(signal => signal.MemberName); } }

            public void Add(SignalDirection direction, string signalName, string datatype, string comment)
            {
                var signal = new HardwareSignal(signalName, SafeMemberName(signalName), datatype, direction, comment);
                var map = direction == SignalDirection.Input ? _inputs : _outputs;

                if (!map.ContainsKey(signal.MemberName))
                {
                    map.Add(signal.MemberName, signal);
                }
            }
        }

        public class HardwareSignal
        {
            public HardwareSignal(string signalName, string memberName, string datatype, SignalDirection direction, string comment)
            {
                SignalName = signalName;
                MemberName = memberName;
                Datatype = datatype;
                Direction = direction;
                Comment = comment;
            }

            public string SignalName { get; private set; }
            public string MemberName { get; private set; }
            public string Datatype { get; private set; }
            public SignalDirection Direction { get; private set; }
            public string Comment { get; private set; }

            public XmlDataMember ToXmlMember()
            {
                return new XmlDataMember(MemberName, Datatype, Comment);
            }
        }
    }

    public enum SignalDirection
    {
        Input,
        Output
    }
}
