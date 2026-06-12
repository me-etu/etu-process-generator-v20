using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Tags;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.IO;
using Siemens.Engineering;

namespace TIA_LIB.Xml
{
    public class XmlTable
    {
        public XmlTable(PlcTagTable plcTagTable, PlcTagTableUserGroup userGroup)
        {
            string folder = System.IO.Directory.GetCurrentDirectory() + '\\';
            string file_name = folder + plcTagTable.Name + ".xml";
            Name = plcTagTable.Name;

            if (File.Exists(file_name))
            {
                File.Delete(file_name);
            }

            if (!File.Exists(file_name))
            {
                plcTagTable.Export(new FileInfo(file_name), ExportOptions.WithDefaults);
            }

            XElement xml = XElement.Load(file_name);

            File.Delete(file_name);

            Xml = xml;
            UserGroup = userGroup;
            PlcTagTable = plcTagTable;

            if (Xml == null)
            {
                return;
            }

            foreach (var tag in Xml.Descendants("SW.Tags.PlcTag"))
            {
                new XmlTag(this, tag);
            }

            SiemensPortal.Current.Tables.Add(this);
        }
        public XmlTable(PlcTagTable plcTagTable, PlcTagTableSystemGroup systemGroup)
        {
            string folder = System.IO.Directory.GetCurrentDirectory() + '\\';
            string file_name = folder + plcTagTable.Name + ".xml";
            Name = plcTagTable.Name;

            if (File.Exists(file_name))
            {
                File.Delete(file_name);
            }

            if (!File.Exists(file_name))
            {
                plcTagTable.Export(new FileInfo(file_name), ExportOptions.WithDefaults);
            }

            XElement xml = XElement.Load(file_name);

            File.Delete(file_name);

            Xml = xml;
            SystemGroup = systemGroup;
            PlcTagTable = plcTagTable;

            if (Xml == null)
            {
                return;
            }

            foreach (var tag in Xml.Descendants("SW.Tags.PlcTag"))
            {
                new XmlTag(this, tag);
            }

            SiemensPortal.Current.Tables.Add(this);
        }

        public PlcTagTable PlcTagTable;
        public PlcTagTableUserGroup UserGroup;
        public PlcTagTableSystemGroup SystemGroup;
        public XElement Xml;
        public string Name;
        public Dictionary<string, XmlTag> Tags = new Dictionary<string, XmlTag>();

    }
}
