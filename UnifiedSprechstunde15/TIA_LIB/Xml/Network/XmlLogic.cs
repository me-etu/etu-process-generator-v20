using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Collections;

namespace TIA_LIB.Xml.Network
{
    public enum XmlLogicType
    {
        And, Or
    }
    public class XmlLogic: XmlPart
    {
        public XmlLogic(XmlNetwork network, XmlLogicType type): base(network)
        {
            switch (type)
            {
                case XmlLogicType.And:
                {
                    Name = "A";
                    break;
                }
                case XmlLogicType.Or:
                {
                    Name = "O";
                    break;
                }
            }

            UId = ++Network.MaxUId;

            Xml = new XElement("Part");
            Xml.SetAttributeValue("Name", Name);
            Xml.SetAttributeValue("UId", UId);

            TemplateValue = new XElement("TemplateValue");
            TemplateValue.SetAttributeValue("Name", "Card");
            TemplateValue.SetAttributeValue("Type", "Cardinality");
            TemplateValue.Value = "0";

            Xml.Add(TemplateValue);

            Network.Parts.Add(Xml);

            Network.Logics.Add(UId, this);

            Network.Block.HasChanged = true;
        }

        public XmlLogic(XmlNetwork network, XElement xml): base(network)
        {
            Network = network;

            Name = xml.Attribute("Name").Value;
            UId = Int32.Parse(xml.Attribute("UId").Value);
            Xml = xml;
            TemplateValue = Xml.Descendants(_Namespace + "TemplateValue").FirstOrDefault();
            Network.Logics.Add(UId, this);
        }

        public XmlPin AddPin(string name)
        {
            var lower = name.ToLower();

            XmlPin pin = new XmlPin(this, lower);

            if(lower.Contains("in"))
            {
                InputCount++;
            }
            else if(lower.Contains("out"))
            {
                OutputCount++;
            }

            return pin;
        }

        public XmlPin GetPin(string name, bool neg = false)
        {
            XmlPin pin;
            if (Pins.TryGetValue(name, out pin))
            {
                return pin;
            }

            if(name.Contains("in"))
            {
                InputCount++;
                TemplateValue.Value = InputCount.ToString();

                if (neg)
                {
                    ElNegated = new XElement("Negated");
                    ElNegated.SetAttributeValue("Name", "in" + InputCount);
                    Xml.Add(ElNegated);
                }
            }
            else if (name.Contains("out"))
            {
                OutputCount++;
            }

            pin = new XmlPin(this, name);
            return pin;
        }


        public int InputCount=0;
        public int OutputCount=0;
        public string Name;
        public Dictionary<string, XmlPin> Pins = new Dictionary<string, XmlPin>();
        public XElement Xml;
        public XElement TemplateValue;
        public XElement ElNegated;
        private XNamespace _Namespace = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v4";
    }
}
