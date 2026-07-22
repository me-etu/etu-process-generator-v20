using Siemens.Engineering.SW.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace TIA_LIB.Xml
{
    public class XmlUdt
    {
        public XmlUdt(string name, IEnumerable<XmlDataMember> members)
        {
            Name = name;
            FileName = Path.Combine(Directory.GetCurrentDirectory(), name + ".xml");

            if (Udts == null)
            {
                Udts = new List<XmlUdt>();
            }

            Udts.Add(this);
            Xml = BuildXml(name, members);
        }

        public void Upload()
        {
            Xml.Save(FileName);
            SiemensPortal.Current.ImportPlcType(Name, FileName);
        }

        private static XElement BuildXml(string name, IEnumerable<XmlDataMember> members)
        {
            XNamespace intf = "http://www.siemens.com/automation/Openness/SW/Interface/v5";
            string created = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffffff");

            var section = new XElement(intf + "Section", new XAttribute("Name", "None"));
            foreach (var member in members.OrderBy(item => item.Name))
            {
                section.Add(member.ToXElement());
            }

            return new XElement("Document",
                new XElement("Engineering", new XAttribute("version", "V17")),
                new XElement("DocumentInfo",
                    new XElement("Created", created),
                    new XElement("ExportSetting", "None")),
                new XElement("SW.Types.PlcStruct",
                    new XAttribute("ID", "0"),
                    new XElement("AttributeList",
                        new XElement("Interface",
                            new XElement(intf + "Sections", section)),
                        new XElement("Name", name)),
                    new XElement("ObjectList",
                        BuildMultilingualText("1", "2", "Comment"),
                        BuildMultilingualText("3", "4", "Title"))));
        }

        private static XElement BuildMultilingualText(string id, string itemId, string compositionName)
        {
            return new XElement("MultilingualText",
                new XAttribute("ID", id),
                new XAttribute("CompositionName", compositionName),
                new XElement("ObjectList",
                    new XElement("MultilingualTextItem",
                        new XAttribute("ID", itemId),
                        new XAttribute("CompositionName", "Items"),
                        new XElement("AttributeList",
                            new XElement("Culture", "en-US"),
                            new XElement("Text")))));
        }

        public string Name { get; private set; }
        public string FileName { get; private set; }
        public XElement Xml { get; private set; }
        public static List<XmlUdt> Udts;
    }
}
