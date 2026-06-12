using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Tags;
using TIA_LIB.Xml;

namespace TIA_LIB
{
    public class XmlTag
    {
        public XmlTag(XmlTable table, XElement xml)
        {
            Xml = xml;
            _Name = Xml.Descendants("AttributeList").Descendants("Name").FirstOrDefault().Value;

            table.Tags.Add(_Name, this);
        }
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                if (Xml.Descendants("AttributeList").Descendants("Name").FirstOrDefault().Value != value)
                {
                    Xml.Descendants("AttributeList").Descendants("Name").FirstOrDefault().Value = value;
                  //  HasChanged = true;
                }
            }
        }
        public XElement Xml;
        private string _Name;
    }
}
