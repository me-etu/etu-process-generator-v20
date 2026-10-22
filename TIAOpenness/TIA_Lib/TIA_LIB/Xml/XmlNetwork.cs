using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TIA_LIB.Xml.Network;
using TIA_LIB.SignalStaging;

namespace TIA_LIB.Xml
{
    public class XmlNetwork
    {
        public XmlNetwork(XmlBlock block, string titel, string description = "", bool empty = false)
        {
           
            Block = block;
            Block.Networks.Add(titel, this);

            Xml = XElement.Load(!empty ? "Xml/EmptyNetwork.xml" : "Xml/EmptyNetworkWithOutCall.xml");
            Name = titel;

            _NetworkSource = Xml.Descendants("NetworkSource").FirstOrDefault();
            ReadSourceNamespace();

            Parts = _NetworkSource.Descendants(_Namespace + "Parts").FirstOrDefault();
            _Wires = _NetworkSource.Descendants(_Namespace + "Wires").FirstOrDefault();

            var title = Xml.Descendants("MultilingualText").Where(el => el.Attribute("CompositionName").Value == "Title").FirstOrDefault();

            foreach (var text in title.Descendants("Text"))
            {
                text.Value = Name;
            }

            SetComment(description);

            Block.HasChanged = true;
        }

        public XmlNetwork(XmlBlock block, XElement xml)
        {
            Block = block;
            Xml = xml;

            _NetworkSource = Xml.Descendants("NetworkSource").FirstOrDefault();
            ReadSourceNamespace();

            //SUCHEN DEKLARIERTE VARIABLES
            foreach (var value in _NetworkSource.Descendants(_Namespace + "Access"))
            {
                if (value.Attribute("Scope").Value == "LiteralConstant")
                {
                    new XmlConstant(this, value);
                }
                if (value.Attribute("Scope").Value == "GlobalVariable")
                {
                    new XmlGlobal(this, value);
                }
                if (value.Attribute("Scope").Value == "LocalVariable")
                {
                    new XmlGlobal(this, value);
                }
            }

            //SUCHEN LOGIC BLOCKS
            Parts = _NetworkSource.Descendants(_Namespace + "Parts").FirstOrDefault();
           
            if (Parts != null)
            {
                foreach (var part in Parts.Descendants(_Namespace + "Part"))
                {
                    new XmlLogic(this, part);
                }

                //SUCHEN CALLS
                foreach (var call in Parts.Descendants(_Namespace + "Call"))
                {
                    new XmlCall(this, call);   
                }
            }

            //SUCHEN WIRES
            _Wires = _NetworkSource.Descendants(_Namespace + "Wires").FirstOrDefault();

            if (_Wires != null)
            {
                foreach (var xwire in _Wires.Elements())
                {
                    new XmlWire(this, xwire);
                }
            }

            //SET NETWORK NAME
            var title = Xml.Descendants("MultilingualText").Where(el => el.Attribute("CompositionName").Value == "Title").FirstOrDefault();
            Name = title.Descendants("Text").FirstOrDefault().Value;

            if(!Block.Networks.ContainsKey(Name)) Block.Networks.Add(Name, this);
        }

        public void SetComment(string description)
        {
            if (string.IsNullOrWhiteSpace(description)) return;

            var comment = Xml.Descendants("MultilingualText")
                .Where(el => el.Attribute("CompositionName") != null && el.Attribute("CompositionName").Value == "Comment")
                .FirstOrDefault();
            if (comment == null) return;

            bool changed = false;
            foreach (var text in comment.Descendants("Text"))
            {
                if (text.Value != description)
                {
                    text.Value = description;
                    changed = true;
                }
            }

            if (changed)
            {
                Block.HasChanged = true;
            }
        }

        public XmlBlock Block;
        public int MaxUId = 20;
        public string Name;
        public XmlCall GetCall(string blockName, string blockType, string instType = null, string instName = null)
        {
            XmlCall call;

            Calls.TryGetValue(instName, out call);

            if(call == null)
            {
                if (Parts == null || _Wires == null)
                    throw new InvalidOperationException("Cannot add call '" + blockName + "' to network '" + Name +
                        "' in " + Block.FileName + ": no editable FBD Parts/Wires containers. Source namespace: " +
                        SourceNamespace + ". Preserve the block and inspect its exported network language/structure.");
                call = new XmlCall(this, blockName, blockType, instType, instName);
            }

            return call;
        }

        public XmlCall FindCall(string blockName, string blockType, string instType = null, string instName = null)
        {
            XmlCall call;

            Calls.TryGetValue(instName, out call);
    
            return call;
        }
        public void SetParameter(XmlCall call, string name, string section, string type, string value, bool isGlobal = false, bool isInstanceDB = false, bool neg = false)
        {
            if (value.Contains("|"))
            {
                //var networkSource = call.Network.Xml.Descendants("NetworkSource").FirstOrDefault();
                //var deleteWire = networkSource.Descendants(_Namespace + "NameCon").Where(el => el.Attribute("Name").Value != null && el.Attribute("Name").Value == name).FirstOrDefault();
                //if (deleteWire != null) deleteWire.Parent.Remove();
            }


            //XmlParameter param;
            //
            //if(!call.Parameters.TryGetValue(name, out param))
            //{
            //    param = new XmlParameter(call, name, section, type, neg);
            //}
            if (call == null) return;

            var param = call.GetParameter(name, section, type, neg);

            SetPin(param, type, value, isGlobal);
        }
        public void SetParameter(XmlCall call, string name, string section, XmlPin pin, bool neg = false)
        {

            var param = call.GetParameter(name, section, "Bool", neg);

            SetParameter(param, pin);
        }
        public void SetParameter(XmlParameter param, XmlPin pin)
        {
            XmlWire wire;

            foreach(var pair in Wires)
            {
                wire = pair.Value;

                if(wire.Contains(pin))
                {
                    wire.AddNameCon(param);
                    return;
                }
            }

            wire = new XmlWire(this);
            wire.AddNameCon(pin);
            wire.AddNameCon(param);
            _Wires.Add(wire.Xml);
        }
        public void SetParameter(XmlCall call, string name, string section, string type)
        {
            var param = call.GetParameter(name, section, type);
            
            if (!OpenConWires.ContainsKey(param.Name.ToLower()))
            {
                var wire = new XmlWire(this);
                wire.AddOpenCon(param);
                _Wires.Add(wire.Xml);
            }           
        }
        public XmlAccess GetPinValue(XmlPin pin)
        {
            foreach (var pair in Wires)
            {
                var wire = pair.Value;
                XmlAccess value;
                if (wire.TryGetPinValue(pin, out value))
                {
                    return value;
                }
            }
            return null;
        }
        public void SetPin(XmlLogic logic, string pinName, string type, string value, bool isLocal = false, bool isGlobal = false, bool neg = false)
        {
            var pin = logic.GetPin(pinName, neg);
            SetPin(pin, type, value, isGlobal, isLocal);
        }
        public void SetPin(XmlPin pin, string type, string value, bool isGlobal = false, bool isLocal = false)
        {
            var values = value.Split('|');          
            XmlAccess xvalue = null;
            string uid;

            if (values.Count() == 1 && !isGlobal)
            {
                if (UIdIdentCon.TryGetValue(pin.UId + "|" + pin.Name, out uid))
                {
                    if (!Access.TryGetValue(Int32.Parse(uid), out xvalue))
                    {
                        if(!isLocal) xvalue = new XmlConstant(this, type, value);
                        if (isLocal) xvalue = new XmlGlobal(this, value, null, type);
                        //xvalue = new XmlConstant(this, type, value);
                        SetPin(pin, xvalue);
                        return;
                    }

                    xvalue.Set(value);
                    return;
                }
                if (!isLocal) xvalue = new XmlConstant(this, type, value);
                if (isLocal) xvalue = new XmlGlobal(this, value, null, type);
                //xvalue = new XmlConstant(this, type, value);

                XmlWire wire;

                if(OpenConWires.TryGetValue(pin.UId + "|" + pin.Name, out wire))
                {
                    wire.Xml.Remove();
                }

                SetPin(pin, xvalue);

            }
            else if (values.Count() > 1 || isGlobal)
            {             
                if (UIdIdentCon.TryGetValue(pin.UId + "|" + pin.Name, out uid))
                {
                    if (!Access.TryGetValue(Int32.Parse(uid), out xvalue))
                    {
                        if(values.Count() > 1) xvalue = new XmlGlobal(this, values, type, isGlobal);
                        if (values.Count() == 1 && isGlobal) xvalue = new XmlGlobal(this, values[0], null, type, isGlobal);
                        SetPin(pin, xvalue);
                        return;
                    }
                    else
                    {
                        ;
                    }

                    xvalue.Set(value);
                    
                    return;
                }

                if (values.Count() > 1) xvalue = new XmlGlobal(this, values, type, isGlobal);
                if (values.Count() == 1 && isGlobal) xvalue = new XmlGlobal(this, values[0], null, type, isGlobal);

                XmlWire wire;

               try
               {
                   if (OpenConWires.TryGetValue(pin.UId + "|" + pin.Name, out wire))
                   {
                        wire.Xml.Remove();
                   }
               }
               catch { }

               SetPin(pin, xvalue);
            }
        }
        public void SetPin(XmlPin param, XmlAccess value)
        {
            var wire = new XmlWire(this);
            wire.AddIdentCon(value);
            wire.AddNameCon(param);
            _Wires.Add(wire.Xml);
        }
        internal void ConfigureSfcHss(XmlCall call)
        {
            ConfigureBooleanInput(call, "HSS", new SignalReference("F_Safe|HSS", true));
        }

        internal void ConfigureBooleanInput(XmlCall call, string pinName, SignalReference reference, bool removeUnusedOldReference = false)
        {
            // Legacy v4 construction has unqualified children; exports are qualified.
            Func<XElement, string, bool> named = (e, name) => e.Name == _Namespace + name || e.Name == XName.Get(name);
            Func<string, InvalidOperationException> invalid = detail => new InvalidOperationException(
                "Cannot configure " + pinName + " in block '" + Block.Name + "', network '" + Name + "', instance '" + call.InstName +
                "': " + detail + ". Reconcile this input connection explicitly; unrelated wiring is preserved.");
            if (Parts == null || _Wires == null) throw invalid("no editable FBD Parts/Wires containers");
            var parameters = call.CallInfo.Elements().Where(e => named(e, "Parameter") && (string)e.Attribute("Name") == pinName).ToArray();
            var endpoints = _Wires.Descendants().Where(e => named(e, "NameCon") &&
                (string)e.Attribute("UId") == call.UId.ToString() && (string)e.Attribute("Name") == pinName).ToArray();
            if (parameters.Length > 1 || endpoints.Length > 1) throw invalid("multiple HSS parameters or drivers");

            XmlWire wire = null;
            XmlAccess oldAccess = null;
            if (endpoints.Length == 1)
            {
                wire = Wires.Values.SingleOrDefault(w => w.Xml == endpoints[0].Parent);
                if (wire == null) throw invalid("HSS wire is absent from the wire cache");
                var identifiers = wire.Xml.Elements().Where(e => named(e, "IdentCon")).ToArray();
                var names = wire.Xml.Elements().Where(e => named(e, "NameCon")).ToArray();
                var opens = wire.Xml.Elements().Where(e => named(e, "OpenCon")).ToArray();
                bool simple = identifiers.Length == 1 && opens.Length == 0 && wire.Xml.Elements().Count() == names.Length + 1;
                bool open = identifiers.Length == 0 && opens.Length == 1 && names.Length == 1 && wire.Xml.Elements().Count() == 2;
                if (!simple && !open) throw invalid("unsupported HSS topology (expected a simple operand or open connection)");
                if (simple && (!Access.TryGetValue((int)identifiers[0].Attribute("UId"), out oldAccess) ||
                    !(oldAccess is XmlGlobal || oldAccess is XmlConstant))) throw invalid("unsupported HSS operand");
                foreach (var other in names.Where(n => n != endpoints[0]))
                {
                    var consumer = Calls.Values.FirstOrDefault(c => c.UId.ToString() == (string)other.Attribute("UId"));
                    XmlParameter consumerPin;
                    if (consumer == null || !consumer.Parameters.TryGetValue((string)other.Attribute("Name"), out consumerPin) || consumerPin.Section != "Input")
                        throw invalid("HSS shares a wire with an unsupported source or consumer");
                }
            }

            var source = oldAccess as XmlGlobal;
            bool correct = source != null && (string)source.Xml.Attribute("Scope") == (reference.IsGlobal ? "GlobalVariable" : "LocalVariable") &&
                source.Components.SequenceEqual(reference.Value.Split('|'));
            var negations = call.Xml.Elements().Where(e => named(e, "Negated") && (string)e.Attribute("Name") == pinName).ToArray();
            bool parameterCorrect = parameters.Length == 1 && (string)parameters[0].Attribute("Section") == "Input" &&
                (string)parameters[0].Attribute("Type") == "Bool";
            if (correct && parameterCorrect && negations.Length == 0 && !call.Negations.Contains(pinName))
            {
                // Exported fan-out wires are not indexed by the legacy single-pin parser.
                var existing = call.GetParameter(pinName, "Input", "Bool");
                if (!wire.Contains(existing)) wire.Pins.Add(existing);
                wire.Value = source;
                UIdIdentCon[call.UId + "|" + pinName] = source.UId.ToString();
                OpenConWires.Remove(call.UId + "|" + pinName);
                return;
            }

            var parameter = call.GetParameter(pinName, "Input", "Bool");
            if (parameters.Length == 0) parameter.Xml.Name = _Namespace + "Parameter";
            parameter.Section = "Input";
            parameter.Type = "Bool";
            parameter.Xml.SetAttributeValue("Section", "Input");
            parameter.Xml.SetAttributeValue("Type", "Bool");
            foreach (var negation in negations) negation.Remove();
            call.Negations.RemoveAll(n => n == pinName);
            parameter.Neg = null;

            if (!correct)
            {
                source = Access.Values.OfType<XmlGlobal>().FirstOrDefault(a =>
                    (string)a.Xml.Attribute("Scope") == (reference.IsGlobal ? "GlobalVariable" : "LocalVariable") && a.Components.SequenceEqual(reference.Value.Split('|')));
                if (source == null)
                {
                    // Reference the required existing PLC member, without creating or editing
                    // F_Safe via GetValueFromGlobalDB. Import/compile validates the dependency.
                    var xml = new XElement(_Namespace + "Access", new XAttribute("UId", ++MaxUId),
                        new XAttribute("Scope", reference.IsGlobal ? "GlobalVariable" : "LocalVariable"),
                        new XElement(_Namespace + "Symbol", reference.Value.Split('|').Select(component =>
                            new XElement(_Namespace + "Component", new XAttribute("Name", component)))));
                    source = new XmlGlobal(this, xml);
                    Parts.AddFirst(xml);
                }
                if (wire != null && wire.Xml.Elements().Count(e => named(e, "NameCon")) == 1)
                {
                    wire.ReplaceIdentCon(source);
                }
                else
                {
                    if (wire != null)
                    {
                        endpoints[0].Remove();
                        wire.Pins.RemoveAll(p => p.UId == call.UId && p.Name == pinName);
                    }
                    wire = new XmlWire(this);
                    wire.AddIdentCon(source);
                    wire.AddNameCon(parameter);
                    foreach (var element in wire.Xml.DescendantsAndSelf()) element.Name = _Namespace + element.Name.LocalName;
                    _Wires.Add(wire.Xml);
                }
            }
            if (!wire.Contains(parameter)) wire.Pins.Add(parameter);
            wire.Value = source;
            UIdIdentCon[call.UId + "|" + pinName] = source.UId.ToString();
            OpenConWires.Remove(call.UId + "|" + pinName);
            if (removeUnusedOldReference && oldAccess != null && oldAccess != source &&
                !_Wires.Descendants().Any(e => named(e, "IdentCon") && (string)e.Attribute("UId") == oldAccess.UId.ToString()))
            {
                oldAccess.Xml.Remove();
                Access.Remove(oldAccess.UId);
            }
            Block.HasChanged = true;
        }

        private bool TrySetPin(XmlPin pin, string value)
        {
            var xvalue = GetPinValue(pin);
            if (xvalue != null)
            {
                xvalue.Set(value);
                return true;
            }
            return false;
        }

        public Dictionary<string, XmlCall> Calls = new Dictionary<string, XmlCall>();
        public Dictionary<int, XmlAccess> Access = new Dictionary<int, XmlAccess>();
        public Dictionary<int, XmlLogic> Logics = new Dictionary<int, XmlLogic>();
        public Dictionary<int, XmlWire> Wires = new Dictionary<int, XmlWire>();
        public Dictionary<string, string> UIdIdentCon = new Dictionary<string, string>();
        public Dictionary<string, XmlWire> OpenConWires = new Dictionary<string, XmlWire>();

        public XElement Xml;
        private XElement _NetworkSource;
        private XElement _Wires;
        public XElement Parts;
        internal void ReplaceInputReferenceWithConstant(SignalReference reference, bool value)
        {
            // Repair only exact generated operands disabled by the device configuration.
            // Keep UIds/wires/negations intact; unrelated/manual operands are not rewritten.
            string scope = reference.IsGlobal ? "GlobalVariable" : "LocalVariable";
            foreach (var access in Access.Values.OfType<XmlGlobal>().Where(a =>
                (string)a.Xml.Attribute("Scope") == scope && a.Components.SequenceEqual(reference.Value.Split('|'))).ToList())
            {
                var xml = new XElement(SourceNamespace + "Access", new XAttribute("UId", access.UId),
                    new XAttribute("Scope", "LiteralConstant"),
                    new XElement(SourceNamespace + "Constant", new XElement(SourceNamespace + "ConstantType", "Bool"),
                        new XElement(SourceNamespace + "ConstantValue", value ? "True" : "False")));
                access.Xml.ReplaceWith(xml);
                var constant = new XmlConstant(this, xml);
                Access[access.UId] = constant;
                foreach (var wire in Wires.Values.Where(w => w.Value == access)) wire.Value = constant;
                Block.HasChanged = true;
            }
        }

        public XNamespace SourceNamespace { get; private set; } = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v4";
        private XNamespace _Namespace { get { return SourceNamespace; } }

        private void ReadSourceNamespace()
        {
            if (_NetworkSource == null)
                throw new InvalidOperationException("Missing NetworkSource in block " + Block.FileName + ", compile unit " + (string)Xml.Attribute("ID") + ".");
            var source = _NetworkSource.Elements().FirstOrDefault(e => e.Name.LocalName == "FlgNet");
            if (source != null) SourceNamespace = source.Name.Namespace;
        }

        internal XName ElementName(string name)
        {
            // Preserve legacy v4 generation; qualify additions to newer exported networks
            // in their existing namespace rather than relabeling the user's network.
            return SourceNamespace.NamespaceName.EndsWith("/FlgNet/v4", StringComparison.Ordinal)
                ? XName.Get(name) : SourceNamespace + name;
        }


    }
}
