using System;
using System.Collections.Generic;
using System.Windows.Documents;
using Siemens.Engineering.SW.Blocks;
using TIA_LIB.Devices;
using TIA_LIB.Xml.Network;


namespace TIA_LIB.Xml
{
    public class XmlInterlock : XmlBlock
    {
        public XmlInterlock(XmlPhase ph): base(ph.Op.Unit.UserGroup, ph.Op.Name + "_Interlocks",  ph.Op.Name + "_" + ph.phName +  "_Interlocks" + ".xml", "Xml/EmptyBlock.xml", "Interlocks", false, ph.Op.Name)
        {
        }
    }
}
