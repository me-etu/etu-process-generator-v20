using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace TIA_LIB.Xml.Network
{
    public class XmlWire
    {
        public XmlWire(XmlNetwork network)
        {
            Network = network;

            UId = ++Network.MaxUId;

            Xml = new XElement("Wire");

            Xml.SetAttributeValue("UId", UId);

            Network.Wires.Add(UId, this);

            Network.Block.HasChanged = true;
        }
        public XmlWire(XmlNetwork network, XElement xml)
        {
            Network = network;
            Xml = xml;

            UId = Int32.Parse(Xml.Attribute("UId").Value);

            if (UId > Network.MaxUId)
            {
                Network.MaxUId = UId;
            }

            _OpenCon = Xml.Descendants(_Namespace + "OpenCon").FirstOrDefault();
            if(_OpenCon != null)
            {
                var openConUId = Int32.Parse(_OpenCon.Attribute("UId").Value);
                if (openConUId > Network.MaxUId)
                {
                    Network.MaxUId = openConUId;
                }
            }

            _IdentCon = Xml.Descendants(_Namespace + "IdentCon").FirstOrDefault();
            if(_IdentCon != null)
            {
                var identConUId = Int32.Parse(_IdentCon.Attribute("UId").Value);
                Network.Access.TryGetValue(identConUId, out Value);
            }

            
            foreach(var nameCon in Xml.Descendants(_Namespace + "NameCon"))
            {
                var nameConUId = Int32.Parse(nameCon.Attribute("UId").Value);
                var pinName = nameCon.Attribute("Name").Value;

                XmlLogic logic;
                if(Network.Logics.TryGetValue(nameConUId, out logic))
                {
                    logic.AddPin(pinName);
                }
            }

            Network.Wires.Add(UId, this);

            //SUCHEN AKTUELLE UID ZUGEORDNET ZU PARAMETER
            var nameConList = Xml.Descendants(_Namespace + "NameCon");
            var identConList = Xml.Descendants(_Namespace + "IdentCon");
            var opentConList = Xml.Descendants(_Namespace + "OpenCon");

            if (nameConList != null && identConList != null)
            {
                if(nameConList.Count() == 1 && identConList.Count() == 1)
                {
                    var parName = nameConList.ElementAt(0).Attribute("Name").Value;
                    var parUid = identConList.ElementAt(0).Attribute("UId").Value;
                    var callUId = nameConList.ElementAt(0).Attribute("UId").Value;

                    Network.UIdIdentCon.Add(callUId + "|" + parName, parUid);
                }               
            }

            if (nameConList != null && opentConList != null)
            {
                if (nameConList.Count() == 1 && opentConList.Count() == 1)
                {
                    var callUId = nameConList.ElementAt(0).Attribute("UId").Value;
                    var parName = nameConList.ElementAt(0).Attribute("Name").Value;

                    Network.OpenConWires.Add(callUId + "|" + parName, this);
                }
            }
        }

        public void AddIdentCon(XmlAccess value)
        {
            Value = value;

            if(_OpenCon != null)
            {
                _OpenCon.Remove();
                _OpenCon = null;
            }

            var identCon = new XElement("IdentCon");
            identCon.SetAttributeValue("UId", value.UId);
            Xml.Add(identCon);
        }

        public void AddNameCon(XmlPin pin)
        {
            if (!Contains(pin))
            {
                Pins.Add(pin);
                var nameCon = new XElement("NameCon");
                nameCon.SetAttributeValue("UId", pin.Part.UId);
                nameCon.SetAttributeValue("Name", pin.Name);
                Xml.Add(nameCon);
            }
        }

        public void AddOpenCon(XmlPin pin)
        {
            if(_OpenCon == null)
            {
                _OpenCon = new XElement("OpenCon");
                _OpenCon.SetAttributeValue("UId", ++Network.MaxUId);
                Xml.Add(_OpenCon);
            }
            AddNameCon(pin);
        }

        public bool Contains(XmlPin pin)
        { 
            foreach (var tmpPin in Pins)
            {
                if(tmpPin.Name != pin.Name)
                {
                    continue;
                }
                if(tmpPin.Part.UId == pin.Part.UId)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetPinValue(XmlPin pin, out XmlAccess value)
        {
            value = null;
            if (Value == null)
            {
                return false;
            }
            if(Contains(pin))
            {
                value = Value;
                return true;
            }
           
            return false;

 
        }
        public XElement Xml;
        public XmlNetwork Network;
        public int UId = 0;
        private XElement _OpenCon;
        private XElement _IdentCon;
        public List<XmlPin> Pins = new List<XmlPin>();      
        public XmlAccess Value;
        private XNamespace _Namespace = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v4";
    }
  
}
