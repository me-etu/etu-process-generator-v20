using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;

namespace TIA_LIB.Xml.Network
{
    public class XmlParameter: XmlPin
    {
        public XmlParameter(XmlCall call, string name, string section, string type, bool neg = false): base(call, name)
        {
            Call = call;

            Section = section;
            Type = type;

            Call.Parameters.Add(name, this);

            if(name.ToLower() != "en")
            {
                Xml = new XElement("Parameter");

                Xml.SetAttributeValue("Name", name);
                Xml.SetAttributeValue("Section", section);
                Xml.SetAttributeValue("Type", type);

                Call.CallInfo.Add(Xml);

                Call.Network.Block.HasChanged = true;
            }    
        }
        public XmlParameter(XmlCall call, XElement xml): base(call, null)
        {
            Call = call;

            Xml = xml;

            Name = Xml.Attribute("Name").Value;
            Section = Xml.Attribute("Section").Value;
            Type = Xml.Attribute("Type").Value;


            Call.Parameters.Add(Name, this);
        }

        public void SetNegations()
        {
            Neg = new XElement("Negated");
            Neg.SetAttributeValue("Name", Name);
            Call.CallInfo.AddAfterSelf(Neg);
            Call.Network.Block.HasChanged = true;
        }

        public XElement Xml;
        public XElement Neg;
        public string Section;
        public string Type;
        public XmlCall Call;
    }

}
