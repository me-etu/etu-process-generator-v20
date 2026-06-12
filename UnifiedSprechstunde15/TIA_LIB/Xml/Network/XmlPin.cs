using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace TIA_LIB.Xml.Network
{
    public enum XmlPinType
    {
        Input, Output, InOut
    }
    public class XmlPin
    {
        public XmlPin (XmlPart part, string name)
        {
            Name = name;
            Part = part;
            UId = part.UId;
        }


        public string Name;
        public XmlPart Part;
        public int UId;
    }
}
