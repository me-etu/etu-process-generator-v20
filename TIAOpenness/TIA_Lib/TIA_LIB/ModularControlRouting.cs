using System;
using System.Linq;
using TIA_LIB.SignalStaging;
using TIA_LIB.Xml;
using TIA_LIB.Xml.Network;

namespace TIA_LIB.Generation
{
    // Prepared offline; activation belongs to G-017's finalized plan/preflight pass.
    internal static class ModularControlRouting
    {
        internal static void RequireInput(XmlBlock block, string name, string datatype)
        {
            var sections = block.Xml.Element("SW.Blocks.FB")?.Element("AttributeList")?.Element("Interface")?.Elements().FirstOrDefault(e => e.Name.LocalName == "Sections");
            if (sections == null) throw new InvalidOperationException("Missing interface for " + block.Name + ".");
            var existing = sections.Elements().SelectMany(s => s.Elements()).Where(m => m.Name.LocalName == "Member" &&
                string.Equals((string)m.Attribute("Name"), name, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (existing.Length > 1 || existing.Any(m => (string)m.Parent.Attribute("Name") != "Input" ||
                (string)m.Attribute("Name") != name || ((string)m.Attribute("Datatype"))?.Trim('"') != datatype.Trim('"')))
                throw new InvalidOperationException("Interface collision for " + block.Name + "." + name + "; expected Input " + datatype + ". Preserve and reconcile the existing declaration.");
            block.GetInterfaceMember("Input", name, datatype);
        }
        static XmlCall Require(XmlBlock block, string title, string instance, string type, string scope)
        {
            var network = block.FindNetwork(title);
            XmlCall call;
            if (network == null || !network.Calls.TryGetValue(instance, out call) || call.BlockName != type ||
                call.BlockType != "FB" || call.InstType != scope)
                throw new InvalidOperationException("Modular control routing requires " + type + " instance '" + instance +
                    "' in block '" + block.Name + "', network '" + title + "'. Reconcile the existing call explicitly.");
            return call;
        }
        static void Bind(XmlCall call, string pin, string reference, bool global = false)
        {
            call.Network.ConfigureBooleanInput(call, pin, new SignalReference(reference, global), true);
        }
        static bool IsCyclicReset(XmlCall call)
        {
            var end = call.Network.Xml.Descendants().FirstOrDefault(e => e.Name.LocalName == "NameCon" &&
                (string)e.Attribute("UId") == call.UId.ToString() && (string)e.Attribute("Name") == "RESET");
            var ident = end?.Parent.Elements().FirstOrDefault(e => e.Name.LocalName == "IdentCon");
            XmlAccess source;
            if (ident == null || !call.Network.Access.TryGetValue((int)ident.Attribute("UId"), out source)) return false;
            var symbol = source as XmlGlobal;
            return symbol != null && (string)symbol.Xml.Attribute("Scope") == "GlobalVariable" && symbol.Components.SequenceEqual(new[] { "RESET_CYCL" });
        }
        internal static void Prepare(XmlPlant plant)
        {
            foreach (var unit in plant.Units.Values)
            {
                var plantNetwork = plant.FindNetwork(unit.Name) ?? plant.FindUnitCallNetwork(unit.Name);
                if (plantNetwork == null) throw new InvalidOperationException("Missing Plant call for unit " + unit.Name + ".");
                if (plant.Networks.Values.SelectMany(n => n.Calls.Values).Count(c => c.BlockName == "fb" + unit.Name && c.InstName == unit.Name) != 1)
                    throw new InvalidOperationException("Ambiguous Plant invocation count for Unit " + unit.Name + ". Preserve and reconcile duplicate/manual callers explicitly.");
                var unitCall = Require(plant, plantNetwork.Name, unit.Name, "fb" + unit.Name, "GlobalVariable");
                foreach (var binding in ModularControlContract.PlantToUnit())
                {
                    RequireInput(unit, binding.Input, "Bool");
                    Bind(unitCall, binding.Input, string.Join("|", binding.Source.Components), true);
                }
                var selector = Require(unit, "Unit sfc control", "sfc_" + unit.Name, "fbControlSfc", "LocalVariable");
                Bind(selector, "HSS", "iHSS");
                Bind(selector, "Reset", "iReset");
                foreach (var device in unit.Devices.Values)
                foreach (var call in device.ConfiguredDeviceCalls())
                {
                    // RESET_CYCL has separate established semantics from HMI.RESET.
                    if (!IsCyclicReset(call)) Bind(call, "RESET", "iReset");
                    if (new[] { "fbVlv2Out", "fbVlvCtrl", "fbMotS", "fbMotAn" }.Contains(call.BlockName)) Bind(call, "PROT", "iHSS");
                }
                foreach (var operation in unit.Operations.Values)
                {
                    var opCall = Require(unit, operation.Name, operation.Name, operation.Name, "LocalVariable");
                    foreach (var binding in ModularControlContract.UnitToOperation())
                    {
                        RequireInput(operation, binding.Input, "Bool");
                        Bind(opCall, binding.Input, string.Join("|", binding.Source.Components));
                    }
                    Bind(Require(operation, operation.Name, "sfc_" + operation.Name, "fbControlSfc", "LocalVariable"), "HSS", "HSS");
                    // No new Reset behavior is invented for the operation selector.
                    foreach (var phase in operation.Phases.Keys)
                    {
                        var controller = Require(operation, "Sfc control " + phase, "sfc_" + phase, "fbControlSfc", "LocalVariable");
                        Bind(controller, "HSS", "HSS");
                        Bind(controller, "Reset", "Delayed_Reset");
                    }
                }
            }
        }
    }
}
