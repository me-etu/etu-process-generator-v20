using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TIA_LIB.Xml.Network;

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

            Parts = _NetworkSource.Descendants(_Namespace + "Parts").FirstOrDefault();
            _Wires = _NetworkSource.Descendants(_Namespace + "Wires").FirstOrDefault();

            var title = Xml.Descendants("MultilingualText").Where(el => el.Attribute("CompositionName").Value == "Title").FirstOrDefault();

            foreach (var text in title.Descendants("Text"))
            {
                text.Value = Name;
            }

            if(description != "")
            {
                var comm = Xml.Descendants("MultilingualText").Where(el => el.Attribute("CompositionName").Value == "Comment").FirstOrDefault();

                foreach (var text in comm.Descendants("Text"))
                {
                    text.Value = description;
                }
            }

            Block.HasChanged = true;
        }

        public XmlNetwork(XmlBlock block, XElement xml)
        {
            Block = block;
            Xml = xml;

            _NetworkSource = Xml.Descendants("NetworkSource").FirstOrDefault();

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

        public XmlBlock Block;
        public int MaxUId = 20;
        public string Name;
        public XmlCall GetCall(string blockName, string blockType, string instType = null, string instName = null)
        {
            XmlCall call;

            Calls.TryGetValue(instName, out call);

            if(call == null)
            {
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
        private XNamespace _Namespace = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v4";


    }
}
