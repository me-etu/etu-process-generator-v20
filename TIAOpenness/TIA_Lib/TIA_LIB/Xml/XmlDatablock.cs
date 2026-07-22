using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Siemens.Engineering.SW.Blocks;

namespace TIA_LIB.Xml
{
    public class XmlDatablock
    {
        public XmlDatablock(string name)
        {
            _Name = name;
           
            string folder = System.IO.Directory.GetCurrentDirectory() + "\\";

            FileName = folder + _Name + ".xml";

            if (Datablocks == null)
            {
                Datablocks = new List<XmlDatablock>();
            }
            Datablocks.Add(this);

            SiemensPortal.Current.GlobalDatablocks.Add(name, this);

            UserGroup = SiemensPortal.Current.CreatePlcFolder("Datablocks");

            Xml = SiemensPortal.Current.ExportPlcBlock(UserGroup, Name, FileName);
            if (Xml != null)
            {
                _AllowedCultures = GetCultures(Xml);
                AddProjectCultures(_AllowedCultures);
            }

            if (Xml == null)
            {
                Xml = XElement.Load("Xml/EmptyDatablock.xml");
                Name = name;
            }

            SectionList = Xml.Descendants("SW.Blocks.GlobalDB").Descendants(_Namespace + "Section").FirstOrDefault();

            foreach (var member in SectionList.Elements())
            {
                new Xml.XmlGlobalInstance(this, member);
            }
        }

        public void GetValue(string name, string datatype)
        {

            if(!GlobalsInstance.ContainsKey(name))
            {
               new Xml.XmlGlobalInstance(this, name, datatype);
            }           
        }


        public void SetMember(XmlDataMember member)
        {
            var existing = SectionList.Elements()
                .FirstOrDefault(element => element.Attribute("Name") != null && element.Attribute("Name").Value == member.Name);

            if (existing != null)
            {
                existing.ReplaceWith(member.ToXElement());
            }
            else
            {
                SectionList.Add(member.ToXElement());
            }

            HasChanged = true;
        }

        public void Upload()
        {
            if (!HasChanged) return;

            PruneUnsupportedMultilingualTextItems();

            Xml.Save(FileName);

            SiemensPortal.Current.ImportPlcBlock(UserGroup, Name, FileName);

            HasChanged = false;
        }

        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                if (Xml.Descendants("SW.Blocks.GlobalDB").Descendants("AttributeList").Descendants("Name").FirstOrDefault().Value != _Name)
                {
                    Xml.Descendants("SW.Blocks.GlobalDB").Descendants("AttributeList").Descendants("Name").FirstOrDefault().Value = _Name;
                    HasChanged = true;
                }
            }
        }
        public bool HasChanged = false;
        private string _Name;
        public XElement Xml;
        public XElement SectionList;
        public string FileName;
        public PlcBlockUserGroup UserGroup;
        public Dictionary<string, Xml.XmlGlobalInstance> GlobalsInstance = new Dictionary<string, XmlGlobalInstance>();
        private static HashSet<string> _ProjectCultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _AllowedCultures;
        private XNamespace _Namespace = "http://www.siemens.com/automation/Openness/SW/Interface/v5";
        public static List<XmlDatablock> Datablocks;

        private HashSet<string> GetCultures(XElement xml)
        {
            return new HashSet<string>(
                xml.Descendants("Culture")
                   .Select(el => el.Value)
                   .Where(value => !string.IsNullOrWhiteSpace(value)),
                StringComparer.OrdinalIgnoreCase);
        }

        private void AddProjectCultures(HashSet<string> cultures)
        {
            foreach (var culture in cultures)
            {
                _ProjectCultures.Add(culture);
            }
        }

        private void PruneUnsupportedMultilingualTextItems()
        {
            if (_AllowedCultures == null || _AllowedCultures.Count == 0)
            {
                _AllowedCultures = _ProjectCultures.Count > 0
                    ? new HashSet<string>(_ProjectCultures, StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "de-DE", "en-US" };
            }

            var unsupportedItems = Xml
                .Descendants("MultilingualTextItem")
                .Where(item =>
                {
                    var culture = item.Descendants("Culture").FirstOrDefault()?.Value;
                    return !string.IsNullOrWhiteSpace(culture) && !_AllowedCultures.Contains(culture);
                })
                .ToList();

            foreach (var item in unsupportedItems)
            {
                item.Remove();
            }
        }

    }
}
