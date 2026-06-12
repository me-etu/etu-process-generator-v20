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

        public void Upload()
        {
            if (!HasChanged) return;

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
        private XNamespace _Namespace = "http://www.siemens.com/automation/Openness/SW/Interface/v5";
        public static List<XmlDatablock> Datablocks;

    }
}
