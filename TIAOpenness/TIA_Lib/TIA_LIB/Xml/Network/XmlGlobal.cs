using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace TIA_LIB.Xml.Network
{
    public class XmlGlobal: XmlAccess
    {
        public XmlGlobal(XmlNetwork network, string value1, string value2, string datatype, bool isGlobal = false, bool isInstanceDB = false): base(network)
        {
            Value1 = value1;
            Value2 = value2;
            Datatype = datatype;

            if(isGlobal) Xml.SetAttributeValue("Scope", "GlobalVariable");
            if(!isGlobal) Xml.SetAttributeValue("Scope", "LocalVariable");

            var symbol = new XElement("Symbol");
            Xml.Add(symbol);

            if(value1 != "" && value1 != null)
            {
                var param1 = new XElement("Component");
                param1.SetAttributeValue("Name", value1);
                symbol.Add(param1);
            }

            if(value2 != "" && value2 != null)
            {
                var param2 = new XElement("Component");
                param2.SetAttributeValue("Name", value2);
                symbol.Add(param2);
            }

            if(isGlobal && value1 != null && value2 != null && (isInstanceDB || !value1.StartsWith("TP")))
            {
                SiemensPortal.Current.GetValueFromGlobalDB(value1, value2, datatype);
            }

            Network.Block.HasChanged = true;
        }
        public XmlGlobal(XmlNetwork network, XElement xml): base(network, xml)
        {
            var el1 = xml.Descendants(_Namespace + "Component").FirstOrDefault();

            if (el1 != null)
            {
                Value1 = el1.Attribute("Name").Value;
            }

            var el2 = xml.Descendants(_Namespace + "Component").LastOrDefault();
            if(el2 != null)
            {
                Value2 = el2.Attribute("Name").Value;
            }


            if(el2 != null) Value = Value1 + "|" + Value2;
            if(el2 == null) Value = Value1;
        }

        public override void Set(string value)
        {
            var values = value.Split('|');

            if(values.Count() > 1)
            {
                if (values[0] == Value1 && values[1] == Value2)
                {
                    return;
                }

                Value1 = values[0];
                Value2 = values[1];

                Xml.Descendants(_Namespace + "Component").FirstOrDefault().Value = Value1;
                Xml.Descendants(_Namespace + "Component").LastOrDefault().Value = Value2;
            }
            else
            {
                if (values[0] == Value1)
                {
                    return;
                }

                Value1 = values[0];

                Xml.Descendants(_Namespace + "Component").FirstOrDefault().Value = Value1;

            }


            Network.Block.HasChanged = true;
        }
        public string Value1;
        public string Value2;
        public string Value;
        public string Datatype;
        private XNamespace _Namespace = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v4";
    }
}
