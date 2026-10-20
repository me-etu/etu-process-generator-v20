using System.Collections.Generic;
using System.Xml.Linq;

namespace TIA_LIB.Xml
{
    public class XmlDataMember
    {
        public XmlDataMember(string name, string datatype, string comment = "")
        {
            Name = name;
            Datatype = datatype;
            Comment = comment;
        }

        public string Name { get; private set; }
        public string Datatype { get; private set; }
        public string Comment { get; private set; }
        public List<XmlDataMember> Children { get; private set; } = new List<XmlDataMember>();

        public XElement ToXElement()
        {
            XNamespace memberNamespace = XNamespace.None;
#if TIA_V21
            memberNamespace = "http://www.siemens.com/automation/Openness/SW/Interface/v5";
#endif
            var member = new XElement(memberNamespace + "Member");
            member.SetAttributeValue("Name", Name);
            member.SetAttributeValue("Datatype", Datatype);

            if (!string.IsNullOrWhiteSpace(Comment))
            {
                member.Add(new XElement(memberNamespace + "Comment",
                    new XElement(memberNamespace + "MultiLanguageText",
                        new XAttribute("Lang", "en-US"),
                        Comment)));
            }

            foreach (var child in Children)
            {
                member.Add(child.ToXElement());
            }

            return member;
        }
    }
}
