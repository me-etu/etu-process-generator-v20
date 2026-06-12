using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TIA_LIB.Xml.Network
{
    public class XmlPart
    {
        public XmlPart(XmlNetwork network)
        {
            Network = network;
        }
        public XmlNetwork Network;
        public int UId;
    }
}
