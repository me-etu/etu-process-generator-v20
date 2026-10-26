using Siemens.Engineering.SW.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using TIA_LIB.Xml.Network;

namespace TIA_LIB.Xml
{
    public class XmlBlock
    {
        internal bool ManagedBodyVerified;
        public XmlBlock(PlcBlockUserGroup parent_user_group, string name, string file_name, string template, string instName, bool instGlobal = false, string user_gorup_name = "", bool overwrite = false, bool preserveOnly = false, string expectedLanguage = null, XmlBlock parentBlock = null)
        {          
            _Name = name;

            string  inst = "";
            if (instName.StartsWith("fb")) inst = instName.Substring(2);
            if (!instName.StartsWith("fb")) inst = instName;

            if(instGlobal) Instances.Add(instGlobal + "|" + inst + "|" + name);

            string folder = System.IO.Directory.GetCurrentDirectory() + "\\";
            FileName = folder + file_name;

            Networks = new Dictionary<string, XmlNetwork>();

            if (Blocks == null)
            {
                Blocks = new List<XmlBlock>();
            }
            Blocks.Add(this);

            if (Generation.ModularGenerationRuntime.Current != null)
            {
                TargetGroupPath = (parentBlock?.TargetGroupPath ?? Generation.ModularGenerationRuntime.ParentPath(parent_user_group)).Concat(new[] { user_gorup_name == "" ? Name : user_gorup_name }).ToArray();
                UserGroup = Generation.ModularGenerationRuntime.Current.FindGroup(TargetGroupPath);
            }
            else if(parent_user_group != null)
            {
                if (user_gorup_name == "") UserGroup = SiemensPortal.Current.CreatePlcFolder(parent_user_group, Name);
                if (user_gorup_name != "") UserGroup = SiemensPortal.Current.CreatePlcFolder(parent_user_group, user_gorup_name);
            }
            else
            {
                if(user_gorup_name == "") UserGroup = SiemensPortal.Current.CreatePlcFolder(Name);
                if(user_gorup_name != "") UserGroup = SiemensPortal.Current.CreatePlcFolder(user_gorup_name);
            }

            Xml = UserGroup == null && Generation.ModularGenerationRuntime.Current != null ? null : SiemensPortal.Current.ExportPlcBlock(UserGroup, _Name, FileName);
            OriginalDocument = Xml == null ? null : new XElement(Xml);
            if (Generation.ModularGenerationRuntime.Current != null)
                Xml = Generation.ModularGenerationRuntime.Current.Editable(this, name, Xml);
            if (Xml != null && expectedLanguage != null &&
                (string)Xml.Element("SW.Blocks.FB")?.Element("AttributeList")?.Element("ProgrammingLanguage") != expectedLanguage)
                throw new InvalidOperationException("Operation interlock " + UserGroup.Name + "/" + name + " requires an " + expectedLanguage + " FB. Existing block language/type differs. Reconcile explicitly; no automatic conversion or replacement is performed.");
            // SCL operation interlocks are inspected/reused as complete blocks at preflight.
            // Do not parse their SCL networks as FBD, load a fallback or add interface members.
            if (preserveOnly) return;
            if (Xml != null)
            {
                _AllowedCultures = GetCultures(Xml);
                AddProjectCultures(_AllowedCultures);
            }

            if (Xml == null || overwrite)
            {
                Xml = LoadEmptyTemplate(template);
                Name = name;
            }

            _ObjectList = Xml.Descendants("SW.Blocks.FB").Descendants("ObjectList").FirstOrDefault();

            foreach (var network in _ObjectList.Descendants("SW.Blocks.CompileUnit"))
            {
                new XmlNetwork(this, network);
            }


            LocalInstance = new Dictionary<string, XmlLocalInstance>();

            _Memberlist = Xml.Descendants(_Namespace + "Section").Where(el => el.Attribute("Name").Value == "Static").FirstOrDefault();

            if (_Memberlist != null)
            {
                foreach (var member in _Memberlist.Elements())
                {
                    new XmlLocalInstance(this, member);
                }
            }

        }

        private static XElement LoadEmptyTemplate(string template)
        {
            var xml = XElement.Load(template);
#if TIA_V21
            // Migrate only fresh template documents; retain existing exported block content.
            xml.Element("Engineering").SetAttributeValue("version", "V21");
            var attributes = xml.Element("SW.Blocks.FB").Element("AttributeList");
            if (attributes.Element("Namespace") == null)
            {
                attributes.Element("Name").AddAfterSelf(new XElement("Namespace"));
            }
#endif
            return xml;
        }

        public XmlLocalInstance GetLocalInstance(string name, string datatype)
        {
            XmlLocalInstance member;
            
            if(!LocalInstance.TryGetValue(name, out member))
            {
                member = new XmlLocalInstance(this, name, datatype);
            }

            return member;
        }

        internal XmlCall ConfigureSfcControl(string networkName, string instanceName, out bool isNew)
        {
            var network = FindNetwork(networkName);
            isNew = network == null;
            XmlCall call;
            if (isNew)
            {
                network = GetNetwork(networkName);
                call = network.GetCall("fbControlSfc", "FB", "LocalVariable", instanceName);
            }
            else if (!network.Calls.TryGetValue(instanceName, out call) || call.BlockName != "fbControlSfc" ||
                call.BlockType != "FB" || call.InstType != "LocalVariable")
            {
                throw new InvalidOperationException("Cannot configure HSS in block '" + Name + "', network '" + networkName +
                    "', instance '" + instanceName + "': expected the existing local fbControlSfc FB call. Reconcile the call explicitly.");
            }

            XmlLocalInstance instance;
            if (!LocalInstance.TryGetValue(instanceName, out instance) || instance.Datatype.Trim('"') != "fbControlSfc")
                throw new InvalidOperationException("Cannot configure HSS in block '" + Name + "', network '" + networkName +
                    "', instance '" + instanceName + "': expected a matching Static fbControlSfc declaration. Reconcile explicitly.");

            network.ConfigureSfcHss(call);
            return call;
        }

        public void SetLocalInstanceMemberComment(string instanceName, string sectionName, string memberName, string datatype, string description)
        {
            XmlLocalInstance member;

            if (LocalInstance.TryGetValue(instanceName, out member))
            {
                member.SetMemberComment(sectionName, memberName, datatype, description);
            }
        }
        public XmlNetwork FindNetwork(string name)
        {
            XmlNetwork network;

            Networks.TryGetValue(name, out network);

            return network;
        }

        public XmlNetwork GetNetwork(string name, string description = "", bool empty = false)
        {
            XmlNetwork network;

            if(!Networks.TryGetValue(name, out network))
            {
                network = new XmlNetwork(this, name, description, empty);


                var target = _ObjectList.Descendants("SW.Blocks.CompileUnit").LastOrDefault();
                if (target == null) 
                    target = _ObjectList.Descendants("MultilingualText").FirstOrDefault();

                target.AddAfterSelf(network.Xml);
            }
            else if (!string.IsNullOrWhiteSpace(description))
            {
                network.SetComment(description);
            }
            return network;
        }
        public void GetInterfaceMember(string sectionName, string name, string datatype)
        {
            var section = Xml.Descendants(_Namespace + "Section")
                .FirstOrDefault(el => el.Attribute("Name") != null && el.Attribute("Name").Value == sectionName);

            if (section == null)
            {
                var sections = Xml.Descendants(_Namespace + "Sections").FirstOrDefault();
                if (sections == null) return;

                section = new XElement(_Namespace + "Section");
                section.SetAttributeValue("Name", sectionName);
                sections.Add(section);
            }

            var member = section.Elements()
                .FirstOrDefault(el => el.Attribute("Name") != null && el.Attribute("Name").Value == name);

            if (member == null)
            {
                XNamespace memberNamespace = XNamespace.None;
#if TIA_V21
                memberNamespace = _Namespace;
#endif
                member = new XElement(memberNamespace + "Member");
                member.SetAttributeValue("Name", name);
                member.SetAttributeValue("Datatype", datatype);
                member.SetAttributeValue("Accessibility", "Public");
                section.Add(member);
                HasChanged = true;
                return;
            }

            var currentDatatype = member.Attribute("Datatype") != null ? member.Attribute("Datatype").Value : "";
            if (currentDatatype != datatype)
            {
                member.SetAttributeValue("Datatype", datatype);
                HasChanged = true;
            }
        }
        public static List<XmlBlock> Blocks;
        public bool HasChanged = false;
        public PlcBlockUserGroup UserGroup;
        public string Name
        {
            get
            {
                if(_Name.StartsWith("fb"))
                {
                    return _Name.Substring(2);
                }
                else
                {
                    return _Name;
                }
            }
            set
            {
                if(Xml.Descendants("SW.Blocks.FB").Descendants("AttributeList").Descendants("Name").FirstOrDefault().Value != _Name)
                {
                    Xml.Descendants("SW.Blocks.FB").Descendants("AttributeList").Descendants("Name").FirstOrDefault().Value = _Name;
                    HasChanged = true;
                }
            }
        }
        private string _Name;
        internal XElement OriginalDocument;
        internal string[] TargetGroupPath;
        private static HashSet<string> _ProjectCultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _AllowedCultures;

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

        internal string GetCommentCulture()
        {
            var cultures = _AllowedCultures != null && _AllowedCultures.Count > 0
                ? _AllowedCultures : _ProjectCultures;
            if (cultures.Count == 0) return "en-US";
            return cultures.FirstOrDefault(c => string.Equals(c, "en-US", StringComparison.OrdinalIgnoreCase))
                ?? cultures.FirstOrDefault(c => string.Equals(c, "de-DE", StringComparison.OrdinalIgnoreCase))
                ?? cultures.OrderBy(c => c, StringComparer.Ordinal).First();
        }

        private void PruneUnsupportedMultilingualTextItems()
        {
            PruneUnsupportedMultilingualTextItems(Xml);
        }

        internal void PruneUnsupportedMultilingualTextItems(XElement document)
        {
            if (_AllowedCultures == null || _AllowedCultures.Count == 0)
            {
                _AllowedCultures = _ProjectCultures.Count > 0
                    ? new HashSet<string>(_ProjectCultures, StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "de-DE", "en-US" };
            }

            var unsupportedItems = document
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

        public virtual void Upload()
        {
            if (!HasChanged) return;

            int count = 1;

            foreach (XElement el in _ObjectList.Descendants().Where(el => el.Attribute("ID") != null))
            {
                el.SetAttributeValue("ID", count.ToString("X"));
                count++;
            }

            PruneUnsupportedMultilingualTextItems();

            Xml.Save(FileName);

            try
            {
                SiemensPortal.Current.ImportPlcBlock(UserGroup, _Name, FileName);
            }
            catch (Exception ex) when (Networks.Values.Any(n => n.Calls.Values.Any(c => c.BlockName == "fbControlSfc")))
            {
                throw new InvalidOperationException("Import failed for block '" + Name + "' (XML: " + FileName +
                    "). For generated SFC controllers, verify the target library has fbControlSfc.Input HSS : Bool and an existing F_Safe.HSS : Bool. " +
                    "The original import diagnostic is retained as the inner exception; the generator does not replace the library or create a fallback HSS signal.", ex);
            }

           

            foreach (var el in Instances)
            {
                var isGlobal = el.Split('|')[0].ToLower();
                var instName = el.Split('|')[1];
                var blockName = el.Split('|')[2];

                if (isGlobal == "true" && instName != "" && instName != null)
                {
                    SiemensPortal.Current.CreateInstanceDB(UserGroup, blockName, instName);
                }
            }

            HasChanged = false;
        }
        public XElement Xml;
        private XElement _ObjectList;
        private XElement _Memberlist;
        public string FileName;
        private XNamespace _Namespace = "http://www.siemens.com/automation/Openness/SW/Interface/v5";
        public Dictionary<string, XmlNetwork> Networks;
        public Dictionary<string, XmlLocalInstance> LocalInstance;
        public bool InstGlobal;
        public string InstName;

        public string BlocktName;
        public List<string> Instances = new List<string>();

    }
}
