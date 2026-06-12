using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TIA_LIB.Xml.Network;

namespace TIA_LIB.Xml
{
    public class XmlLocalInstance
    {
        public XmlLocalInstance(XmlBlock block, string name, string datatype)
        {
            Name = name;
            Block = block;
            Datatype = datatype;

            Xml = new XElement("Member");

            Xml.SetAttributeValue("Name", name);
            Xml.SetAttributeValue("Datatype", datatype);
            Xml.SetAttributeValue("Accessibility", "Public");

            Block.LocalInstance.Add(Name, this);

            var target = block.Xml.Descendants(_Namespace + "Section").Where(el => el.Attribute("Name").Value == "Static").FirstOrDefault();
            target.Add(Xml);

            Block.HasChanged = true;
        }

        public XmlLocalInstance(XmlBlock block, XElement xml)
        {
            Block = block;
            Xml = xml;

            Name = Xml.Attribute("Name").Value;
            Datatype = Xml.Attribute("Datatype").Value;

            Block.LocalInstance.Add(Name, this);
        }

        public XmlBlock Block;
        public string Name;
        public string Datatype;
        public XElement Xml;
        private XNamespace _Namespace = "http://www.siemens.com/automation/Openness/SW/Interface/v5";
    }
}
