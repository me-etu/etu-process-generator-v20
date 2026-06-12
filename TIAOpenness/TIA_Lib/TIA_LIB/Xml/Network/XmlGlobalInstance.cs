using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TIA_LIB.Xml.Network;

namespace TIA_LIB.Xml
{
    public class XmlGlobalInstance
    {
        public XmlGlobalInstance(XmlDatablock datablock, string name, string datatype)
        {
            Name = name;
            Datatype = datatype;

            Xml = new XElement("Member");

            Xml.SetAttributeValue("Name", name);
            Xml.SetAttributeValue("Datatype", datatype);
            Xml.SetAttributeValue("Remanence", "Retain");
            Xml.SetAttributeValue("Accessibility", "Public");

            datablock.SectionList.Add(Xml);

            datablock.GlobalsInstance.Add(Name, this);

            datablock.HasChanged = true;
        }

        public XmlGlobalInstance(XmlDatablock datablock ,XElement xml)
        {
            Xml = xml;

            Name = Xml.Attribute("Name").Value;
            Datatype = Xml.Attribute("Datatype").Value;

            datablock.GlobalsInstance.Add(Name, this);
        }

        public string Name;
        public string Datatype;
        public XElement Xml;
        private XNamespace _Namespace = "http://www.siemens.com/automation/Openness/SW/Interface/v5";
    }
}
