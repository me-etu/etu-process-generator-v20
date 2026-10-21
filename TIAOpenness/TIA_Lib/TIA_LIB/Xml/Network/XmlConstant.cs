using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace TIA_LIB.Xml.Network
{
    public class XmlConstant: XmlAccess
    {
        public XmlConstant(XmlNetwork network, string type, string value): base(network)
        {
            Type = type;
            Value = value;

            Xml.SetAttributeValue("Scope", "LiteralConstant");
            var constant = new XElement(Network.ElementName("Constant"));
            Xml.Add(constant);

            var constantType = new XElement(Network.ElementName("ConstantType"));
            constantType.SetValue(type);
            constant.Add(constantType);

            var constantValue = new XElement(Network.ElementName("ConstantValue"));
            constantValue.SetValue(value);
            constant.Add(constantValue);

            Network.Block.HasChanged = true;

        }
        public XmlConstant(XmlNetwork network, XElement xml): base(network, xml)
        {
            Type = xml.Descendants(_Namespace + "ConstantType").FirstOrDefault().Value;
            Value = xml.Descendants(_Namespace + "ConstantValue").FirstOrDefault().Value;
        }

        public override void Set(string value)
        {
            if (Value == value)
            {
                return;
            }

            Value = value;

            Xml.Descendants(_Namespace + "ConstantValue").FirstOrDefault().Value = Value;

            Network.Block.HasChanged = true;
        }

        public string Type;
        public string Value;
        private XNamespace _Namespace { get { return Network.SourceNamespace; } }
    }
}
