using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace TIA_LIB.Xml.Network
{
    public class XmlAccess: XmlPart
    {
        protected XmlAccess(XmlNetwork network): base(network)
        {
            UId = ++Network.MaxUId;

            Xml = new XElement("Access");
            Xml.SetAttributeValue("UId", UId);

            Network.Access.Add(UId, this);

            Network.Parts.AddFirst(Xml);

            Network.Block.HasChanged = true;
        }
        protected XmlAccess(XmlNetwork network, XElement xml) : base(network)
        {
            Xml = xml;
            if (Xml.Attribute("UId") != null) UId = Int32.Parse(Xml.Attribute("UId").Value);

            if (UId > Network.MaxUId)
            {
                Network.MaxUId = UId;
            }
            if(!Network.Access.ContainsKey(UId)) Network.Access.Add(UId, this);
        }
        public void Clear()
        {
            foreach (var el in Xml.Elements())
            {
                el.Remove();
            }
        }

        public virtual void Set(string value) { }
        public XElement Xml;
     
    }
}
