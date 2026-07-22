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

        public void SetMemberComment(string sectionName, string memberName, string datatype, string description)
        {
            if (string.IsNullOrWhiteSpace(description)) return;

            var sections = Xml.Elements().FirstOrDefault(el => el.Name.LocalName == "Sections");
            if (sections == null)
            {
                sections = new XElement(_Namespace + "Sections");
                Xml.Add(sections);
            }

            var section = sections.Elements().FirstOrDefault(el => el.Name.LocalName == "Section" && el.Attribute("Name") != null && el.Attribute("Name").Value == sectionName);
            if (section == null)
            {
                section = new XElement(_Namespace + "Section");
                section.SetAttributeValue("Name", sectionName);
                sections.Add(section);
            }

            var member = section.Elements().FirstOrDefault(el => el.Name.LocalName == "Member" && el.Attribute("Name") != null && el.Attribute("Name").Value == memberName);
            if (member == null)
            {
                member = new XElement(_Namespace + "Member");
                member.SetAttributeValue("Name", memberName);
                member.SetAttributeValue("Datatype", datatype);
                section.Add(member);
            }
            else if (member.Attribute("Datatype") == null)
            {
                member.SetAttributeValue("Datatype", datatype);
            }

            var ns = member.Name.NamespaceName != "" ? member.Name.Namespace : _Namespace;
            var comment = member.Elements().FirstOrDefault(el => el.Name.LocalName == "Comment");
            if (comment == null)
            {
                comment = new XElement(ns + "Comment");
                member.AddFirst(comment);
            }

            var text = comment.Elements().FirstOrDefault(el => el.Name.LocalName == "MultiLanguageText" && el.Attribute("Lang") != null && el.Attribute("Lang").Value == "en-US");
            if (text == null)
            {
                text = new XElement(ns + "MultiLanguageText", new XAttribute("Lang", "en-US"));
                comment.Add(text);
            }

            if (text.Value != description)
            {
                text.Value = description;
                Block.HasChanged = true;
            }
        }
        public XmlBlock Block;
        public string Name;
        public string Datatype;
        public XElement Xml;
        private XNamespace _Namespace = "http://www.siemens.com/automation/Openness/SW/Interface/v5";
    }
}
