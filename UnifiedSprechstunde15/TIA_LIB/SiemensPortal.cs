using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Tags;
using Siemens.Engineering.SW.ExternalSources;
using System.Xml.Linq;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.HmiUnified.UI.Screens;
using Siemens.Engineering.HmiUnified.HmiTags;
using Siemens.Engineering.HmiUnified.UI.Widgets;
using Siemens.Engineering.HmiUnified.UI.Dynamization;
using Siemens.Engineering.HmiUnified.UI.Dynamization.Script;
using Siemens.Engineering.HmiUnified.HmiConnections;
using Siemens.Engineering.HmiUnified.UI.Controls;
using Siemens.Engineering.HmiUnified.UI.Parts;
using Siemens.Engineering.HmiUnified.HmiLogging;
using Siemens.Engineering.HmiUnified.LoggingTags;
using Siemens.Engineering.HW.Extensions;
using Siemens.Engineering.HmiUnified.HmiAlarm;
using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.HmiUnified.UI.Enum;
using Siemens.Engineering.HmiUnified.UI.Base;
using Siemens.Engineering.HmiUnified.UI.Events;
using Siemens.Engineering.HmiUnified.Common;
using Siemens.Engineering.SiVArc;
using TIA_LIB.Xml;
using TIA_LIB.Devices;
using Org.BouncyCastle.Crypto.Prng;

namespace TIA_LIB
{
    public class SiemensPortal
    {
        public SiemensPortal()
        {
            Current = this;
            Console.WriteLine("Suche offenes TIA Portal");

            //connect to open TIA Portal
            foreach (TiaPortalProcess tiaProcess in TiaPortal.GetProcesses())
            {
                Portal = tiaProcess.Attach();
                Console.WriteLine("Instanz gefunden");
            }

            //Check if tiaInstance is connected
            if (Portal == null)
            {
                Console.WriteLine("Keine TIA Portal Instanz gefunden");
                Console.WriteLine("Neue Instanz wird gestartet");
                Portal = new TiaPortal(TiaPortalMode.WithUserInterface);
            }

            //Check if Projekt is opened
            while (OpenProject == null)
            {
                if (Portal.Projects.Count == 0)
                {
                    Console.WriteLine("Kein Projekt geöffnet. Bitte öffnen.");
                    Console.WriteLine("Press any key to continue.");
                    Console.ReadKey(true);
                }
                List<Project> projectList;
                projectList = Portal.Projects.ToList<Project>();
                foreach (Project pro in projectList)
                {
                    OpenProject = pro;
                    Console.WriteLine(OpenProject.Name);
                    Sivarc = pro?.GetService<Sivarc>();
                }
            }

            Console.WriteLine("Geräte werden durchsucht");

            if (OpenProject.Devices != null)
            {
                foreach (Device device in OpenProject.Devices)
                {
                    Console.WriteLine(device.TypeIdentifier);

                    if (HmiSoftware == null)
                    {
                        HmiSoftware foundHmiSoftware = FindSoftware<HmiSoftware>(device.DeviceItems);
                        if (foundHmiSoftware != null)
                        {
                            PcStation = device;
                            HmiSoftware = foundHmiSoftware;
                            Console.WriteLine(device.Name + " wurde als HMI Gerät erkannt");
                            Console.WriteLine(foundHmiSoftware.Name + " wurde als Unified Runtime gefunden");
                        }
                    }

                    if (PlcSoftware == null)
                    {
                        PlcSoftware foundPlcSoftware = FindSoftware<PlcSoftware>(device.DeviceItems);
                        if (foundPlcSoftware != null)
                        {
                            PlcSoftware = foundPlcSoftware;
                            Console.WriteLine(device.Name + " wurde als PLC erkannt");
                            Console.WriteLine(foundPlcSoftware.Name + " wurde als PLC Software gefunden");
                        }
                    }
                }

                if (HmiSoftware == null)
                {
                    throw new InvalidOperationException("No HMI software was found in the open TIA project. Device detection now searches SoftwareContainer recursively and does not require a PC/IPC device.");
                }

                if (PlcSoftware == null)
                {
                    throw new InvalidOperationException("No PLC software was found in the open TIA project. Device detection searches SoftwareContainer recursively.");
                }

                Console.WriteLine("'" + PcStation.Name + "' mit '" + HmiSoftware.Name + "' wird nun beeinflusst");

                Connection = HmiSoftware.Connections.FirstOrDefault();
                if (Connection == null)
                {
                    throw new InvalidOperationException("No HMI connection was found for '" + HmiSoftware.Name + "'.");
                }
            }
        }

        private static TSoftware FindSoftware<TSoftware>(IEnumerable<DeviceItem> deviceItems) where TSoftware : class
        {
            if (deviceItems == null)
            {
                return null;
            }

            foreach (DeviceItem deviceItem in deviceItems)
            {
                SoftwareContainer swContainer = deviceItem.GetService<SoftwareContainer>();
                TSoftware software = swContainer?.Software as TSoftware;
                if (software != null)
                {
                    return software;
                }

                TSoftware nestedSoftware = FindSoftware<TSoftware>(deviceItem.DeviceItems);
                if (nestedSoftware != null)
                {
                    return nestedSoftware;
                }
            }

            return null;
        }

        public PlcBlockUserGroup CreatePlcFolder(string name = "")
        {
            PlcBlockUserGroup ret = PlcSoftware.BlockGroup.Groups.Find(name);

            if (ret != null) return ret;

            ret = PlcSoftware.BlockGroup.Groups.Create(name);
            if (ret != null) Console.WriteLine("Folder " + ret.Name + " was created");

            return ret;
        }
        public PlcBlockUserGroup CreatePlcFolder(PlcBlockUserGroup folder, string name = "")
        {
            PlcBlockUserGroup ret = folder.Groups.Find(name);

            if (ret != null) return ret;

            ret = folder.Groups.Create(name);

            if (ret != null) Console.WriteLine("Folder " + ret.Name + " was created");

            return ret;
        }
        public void CreateInstanceDB(PlcBlockUserGroup user_group, string blockname, string instancename)
        {
            PlcBlock plcBlock = user_group.Blocks.Find(blockname);
            PlcBlock plcBlockInstance = user_group.Blocks.Find(instancename);
 
            if (plcBlockInstance == null)
            {
                //plcBlockInstance.Delete();
                user_group.Blocks.CreateInstanceDB(instancename, true, 1, blockname);
            }

            
        }   
        public void ImportPlcBlock(PlcBlockUserGroup user_group, string blockName, string fileName)
        {
            PlcBlock plcblock = user_group.Blocks.Find(blockName);
            
            //if (plcblock != null)
            //{
               //plcblock.Delete();
            //}

            if(File.Exists(fileName))
            {
               user_group.Blocks.Import(new FileInfo(fileName), ImportOptions.Override);
               File.Delete(fileName);
            }
        }
        public XElement ExportPlcBlock(PlcBlockUserGroup user_group, string blockName, string file_name, bool overwrite = true)
        {
            //CompilePlcBlock(_plcBlockUserGroup, blockName);

            PlcBlock block = user_group.Blocks.Find(blockName);

            if (block == null)
                return null;

            if (overwrite && File.Exists(file_name))
            {
                File.Delete(file_name);
            }

            if (!File.Exists(file_name))
            {
                block.Export(new FileInfo(file_name), ExportOptions.WithDefaults);
            }

            XElement xml = XElement.Load(file_name);

            File.Delete(file_name);
            return xml;

        }
        public void DeletePlcBlock(PlcBlockUserGroup _plcBlockUserGroup, string blockName)
        {
            PlcBlock PlcBlock = _plcBlockUserGroup.Blocks.Find(blockName);

            if (PlcBlock != null)
            {
                PlcBlock.Delete();
                PlcBlock = _plcBlockUserGroup.Blocks.Find(blockName);
                if (PlcBlock == null) Console.WriteLine("Plcblock " + blockName + " was deleted");
            }
        }
        public bool ExistPlcBlock(PlcBlockUserGroup _plcBlockUserGroup, string blockName)
        {
            if (_plcBlockUserGroup == null || blockName == "") return false;

            PlcBlock PlcBlock = _plcBlockUserGroup.Blocks.Find(blockName);

            if (PlcBlock != null) return true;

            return false;

        }
        public void CompilePlcBlock(PlcBlockUserGroup _plcBlockUserGroup, string blockName)
        {
            PlcBlock PlcBlock = _plcBlockUserGroup.Blocks.Find(blockName);

            ICompilable compileService = PlcBlock.GetService<ICompilable>();
            compileService.Compile();

        }
        public HmiScreen CreateBaseScreen(string name)
        {
            HmiScreen ret = HmiSoftware.Screens.Find(name);

            if (ret != null) return ret;

            ret = HmiSoftware.Screens.Create(name);

            if (ret != null) Console.WriteLine("Screen " + ret.Name + " was created");

            return ret;
        }
        public void ChangeScreenWindow(string screen, string screenWindow)
        {
            HmiScreen Screen = HmiSoftware.Screens.Find(screen);

            if ((Screen == null) | (screenWindow == "")) return;

            foreach (HmiScreenItemBase item in Screen.ScreenItems)
            {
                if (item.GetType().ToString().IndexOf("HmiScreenWindow") >= 0) item.SetAttribute("Screen", screenWindow);
            }

        }
        public void SetPropertyTag(string value, string propertyName, HmiScreenItemBase item)
        {
            if (item == null) return;

            DynamizationBase dyn = item.Dynamizations.Find(propertyName);
            if (dyn == null) return;         
            dyn.SetAttribute("Tag", value);
        }

        public void SetPropertyTag(string value, string propertyName, HmiScreen screen)
        {
            if (screen == null) return;

            DynamizationBase dyn = screen.Dynamizations.Find(propertyName);
            if (dyn == null) return;
            dyn.SetAttribute("Tag", value);
        }
        public void SetProperty(object value, string propertyName, HmiScreenItemBase item)
        {
            if (item == null) return;
            item.SetAttribute(propertyName, value);
        }
        public void EnumerateAllBlockGroupsAndSubgroups()
        {
            foreach (PlcBlockUserGroup blockUserGroup in SiemensPortal.Current.PlcSoftware.BlockGroup.Groups)
            {
                EnumerateAllBlocks(blockUserGroup);
                EnumerateBlockUserGroups(blockUserGroup);
            }
            foreach (PlcBlock plcBlock in SiemensPortal.Current.PlcSoftware.BlockGroup.Blocks)
            {
                EnumerateBlock(plcBlock);
            }
        }
        public void EnumerateBlockUserGroups(PlcBlockUserGroup blockUserGroup)
        {
            foreach (PlcBlockUserGroup subBlockUserGroup in blockUserGroup.Groups)
            {
                EnumerateAllBlocks(subBlockUserGroup);
                EnumerateBlockUserGroups(subBlockUserGroup);
            }
        }
        public void EnumerateAllBlocks(PlcBlockUserGroup blockUserGroup)
        {
            foreach (PlcBlock block in blockUserGroup.Blocks)
            {
                //if (!blockList.ContainsKey(block)) blockList.Add(block, blockUserGroup);
                //Console.WriteLine(block.Name);

               //plcbBlockList.Add(block.Name, new Block(block, blockUserGroup));
            }
        }
        public void EnumerateBlock(PlcBlock block)
        {
            //if (!blockList.ContainsKey(block)) blockList.Add(block, null);
        }
        public void Sort(string type)
        {
            //IEnumerable<PlcBlock> elList = from block in blockList
            //                               where block.GetType().ToString().Contains("Siemens.Engineering.SW.Blocks." + type) == true
            //                               orderby block.Number ascending
            //                               select block;

            //foreach (PlcBlock block in elList)
            //{

            //    string path = @"c:\temp\MyTest.txt";

            //    if (!File.Exists(path))
            //    {
            //        using (StreamWriter sw = File.CreateText(path))
            //        {
            //            sw.WriteLine("Type" + "\t" + "Number" + "\t" + "Name:" + "\t" + "HeaderVersion" + "\t" + "ModifiedDate" + "\t" + "Author");
            //        }
            //    }

            //    using (StreamWriter sw = File.AppendText(path))
            //    {
            //        if (type != "InstanceDB" && type != "GlobalDB") sw.WriteLine(block.GetType().ToString().Replace("Siemens.Engineering.SW.Blocks.", "") + "\t" + block.Number + "\t" + block.Name + "\t" + block.HeaderVersion + "\t" + block.ModifiedDate + "\t" + block.HeaderAuthor);
            //        if (type == "InstanceDB") sw.WriteLine(block.GetType().ToString().Replace("Siemens.Engineering.SW.Blocks.Instance", "") + "\t" + block.Number + "\t" + block.Name + "\t" + block.HeaderVersion + "\t" + block.ModifiedDate + "\t" + block.HeaderAuthor);
            //        if (type == "GlobalDB") sw.WriteLine(block.GetType().ToString().Replace("Siemens.Engineering.SW.Blocks.Global", "") + "\t" + block.Number + "\t" + block.Name + "\t" + block.HeaderVersion + "\t" + block.ModifiedDate + "\t" + block.HeaderAuthor);
            //    }
            //}
        }
        public HmiScreenItemBase CreateScreenWindow(HmiScreen screen, string screenWindowName)
        {
            HmiScreenItemBase newScreenWindow = screen.ScreenItems.Find(screenWindowName);

            if (newScreenWindow == null && screen != null)
            {
                newScreenWindow = screen.ScreenItems.Create<HmiScreenWindow>(screenWindowName);
                Console.WriteLine("Screenwindow " + screenWindowName + " was created");
            }
            return newScreenWindow;
        }
        public HmiScreenItemBase SetAttributeScreenWindow(HmiScreenItemBase screenWindow, object top = null, object left = null)
        {
            if (screenWindow != null)
            {
                screenWindow.SetAttribute("Screen", screenWindow.Name);
                screenWindow.SetAttribute("WindowFlags", HmiWindowFlag.None);
                string name = screenWindow.Name;
                screenWindow.SetAttribute("Width", GetHmiScreen(screenWindow.Name).Width);
                screenWindow.SetAttribute("Height", GetHmiScreen(screenWindow.Name).Height);
                if (top != null) screenWindow.SetAttribute("Top", top);
                if (left != null) screenWindow.SetAttribute("Left", left);
            }

            return screenWindow;
        }
        public void EnumerateScreens()
        {
            foreach (HmiScreen screen in HmiSoftware.Screens)
            {
                hmiScreens.Add(screen);
                Console.WriteLine("founded screen is " + screen.Name);
            }

        }
        public void SiVarcGenerate(string HmiApplicationName, string PlcName)
        {
            Sivarc.Generate(HmiApplicationName, new List<string> { PlcName }, GenerationOptions.UsedHmiTags | GenerationOptions.FullGeneration);
            //Console.WriteLine("Sivarc generated objects on " + HmiApplicationName);     
        }
        public void CompilePlc()
        {
            ICompilable compileService = PlcSoftware.GetService<ICompilable>();
            compileService.Compile();

        }
        public void SetTagRange(string tagName, object tagValueMax, object tagValueMin, bool fixRange = false)
        {
            HmiTag hmiTag = HmiSoftware.Tags.Find(tagName);

            if (hmiTag == null)
            {
                Console.WriteLine("Tag " + tagName + " was not founded for set ranges");
                return;
            }

            //set upper limit
            hmiTag.InitialMaxValue.ValueType = HmiLimitValueType.Tag;
            hmiTag.InitialMaxValue.Value = tagValueMax;

            //set lower limit
            hmiTag.InitialMinValue.ValueType = HmiLimitValueType.Tag;
            hmiTag.InitialMinValue.Value = tagValueMin;
        }

        public void SetTagRange(string udtName, string tagName, object tagValueMax, object tagValueMin, bool fixRange = false)
        {
            HmiTag hmiTag = HmiSoftware.Tags.Find(udtName);

            HmiTag tag = hmiTag.Members.Find(tagName);

            if (hmiTag == null || tag == null)
            {
                if(hmiTag == null) Console.WriteLine("Tag " + hmiTag + " was not founded for set ranges");
                if (tag == null) Console.WriteLine("Tag " + tagName + " was not founded for set ranges");
                return;
            }

            //set upper limit
            tag.InitialMaxValue.ValueType = HmiLimitValueType.Tag;
            tag.InitialMaxValue.Value = tagValueMax;

            //set lower limit
            tag.InitialMinValue.ValueType = HmiLimitValueType.Tag;
            tag.InitialMinValue.Value = tagValueMin;
        }
        public void SetTagRangeFix(string tagName, string tagValueMax, string tagValueMin)
        {
            HmiTag hmiTag = HmiSoftware.Tags.Find(tagName);

            if (hmiTag == null)
            {
                Console.WriteLine("Tag " + tagName + " was not founded for set ranges");
                return;
            }

            //set upper limit
            hmiTag.InitialMaxValue.ValueType = HmiLimitValueType.Constant;
            hmiTag.InitialMaxValue.Value = tagValueMax;

            //set lower limit
            hmiTag.InitialMinValue.ValueType = HmiLimitValueType.Constant;
            hmiTag.InitialMinValue.Value = tagValueMin;
        }
        public void SetTagGMP(string tagName, bool withConfirm, bool withComment, bool value = true)
        {
            HmiTag hmiTag = HmiSoftware.Tags.Find(tagName);

            if (hmiTag == null)
            {
                Console.WriteLine("Tag " + tagName + " was not founded for set GMP");
                return;
            }
            
            //set gmp
            try
            {
                hmiTag.GmpRelevant = value;
            }
            catch { }
            //set options with confirmations

            if (withConfirm)
            {
                if (hmiTag.ConfirmationType != HmiConfirmationType.Confirmation)
                {
                    hmiTag.ConfirmationType = HmiConfirmationType.Confirmation;
                }
                if (withComment)
                {
                    if (!hmiTag.MandatoryCommenting)
                    {
                        hmiTag.MandatoryCommenting = true;
                    }
                }

            }

        }

        public void CreateHmiTag(string tagName, string taName = "", string dbName = "", string parName = "")
        {
            if (tagName == "")
            {
                Console.WriteLine("tagName is empty");
                return;
            }

            string plctag;
            string tag;

            if(taName != "")
            { 
                plctag = dbName + "." + $"\"{taName}_{tagName}\"" + "." + parName;
                tag = dbName + "_" + taName + "_" + tagName + "_" + parName;
            }
            else
            {
                plctag = dbName + "." + tagName + "." + parName;
                tag = dbName + "_" + tagName + "_" + parName;
            }

            HmiTag hmiTag = HmiSoftware.Tags.Find(tag);

            //create tag and table, in case not exist
            if (hmiTag == null)
            {
                HmiTagTable hmiTagTable = HmiSoftware.TagTables.Find(taName);

                if (hmiTagTable == null)
                {
                    hmiTagTable = HmiSoftware.TagTables.Create(taName);
                }

                hmiTag = hmiTagTable.Tags.Create(tag);
                hmiTag.Connection = Connection.Name;
                hmiTag.PlcTag = plctag;
            }
        }
        public HmiSoftware GetHmiSoftware()
        {
            return HmiSoftware;
        }
        public PlcSoftware GetPlcSoftware()
        {
            return PlcSoftware;
        }
        public HmiScreen GetHmiScreen(string name)
        {
           //try
           //{
                return HmiSoftware.Screens.Find(name);
           //}
           //catch
           //{
           //     Console.WriteLine("GetHmiScreen was failured. Screen name " + name);
           //     return null;
           //}
        }
        public HmiScreenItemBase GetHmiScreenItem(HmiScreen screen, string name)
        {
            //try
            //{
                return screen.ScreenItems.Find(name);
            //}
            //catch
            //{              
            //    Console.WriteLine("GetHmiScreenItem was failured. Item name " + name + " screen name " + screen.Name);
            //    return null;
            //}
        }
        public void GetValueFromGlobalDB(string name, string value, string datatype)
        {
            Xml.XmlDatablock DataBlock;

            if(!GlobalDatablocks.TryGetValue(name, out DataBlock))
            {
                DataBlock = new Xml.XmlDatablock(name);
            }

            DataBlock.GetValue(value, datatype);

        }
        public HmiConnection Connection = null;
        public static SiemensPortal Current = null;
        public HmiSoftware HmiSoftware = null;
        public PlcSoftware PlcSoftware = null;
        public Device PcStation = null;
        public TiaPortal Portal = null;
        public Project OpenProject = null;
        public static List<HmiScreen> hmiScreens = new List<HmiScreen>();

        public static List<string> createHmiTags = new List<string>();

        public Dictionary<string, XmlDatablock> GlobalDatablocks= new Dictionary<string, XmlDatablock>();

        public Sivarc Sivarc;     
        public void Tags()
        {
            var plc = GetPlcSoftware();
            
            foreach (var table in plc.TagTableGroup.TagTables)
            {
                Console.WriteLine("Table " + table.Name + " was found");

                foreach (var group in plc.TagTableGroup.Groups)
                {
                    Console.WriteLine("Table " + table.Name + " was found");
                }
            }
        }

        public void DeleteRenamedTags()
        {
            var hmiSoftware = GetHmiSoftware();

            List<HmiTag> hmiTagsToDelete = new List<HmiTag>();

            foreach (var tag in hmiSoftware.Tags)
            {
                if (tag.Name.ToString().EndsWith("_Renamed"))
                { 
                    hmiTagsToDelete.Add(tag);
                }
            }
            foreach (var tag in hmiTagsToDelete)
            {
                var tagName = tag.Name;
                tag.Delete();
                Console.WriteLine("HmiTag has been deleted " + tagName);
            }
        }

        public bool CheckTags()
        {
            var hmiSoftware = GetHmiSoftware();

            List<HmiTag> hmiTagsToDelete = new List<HmiTag>();

            foreach (var tag in hmiSoftware.Tags)
            {
                string hmiTagName = tag.Name.ToString().Replace(".", "").Replace("_", "").Replace("\"\\\"", "").Replace(" ", "");
                string plcTagName = tag.PlcTag.ToString().Replace(".", "").Replace("_", "").Replace("\"", "").Replace(" ", "");

                if (hmiTagName != plcTagName && hmiTagName != "" && plcTagName != "")
                {
                    hmiTagsToDelete.Add(tag);
                }
            }

            bool tagsDetected = hmiTagsToDelete.Count != 0;

            foreach (var tag in hmiTagsToDelete)
            {
                var tagName = tag.Name;
                tag.Delete();
                Console.WriteLine("HmiTag has been deleted " + tagName);
            }
            
            return tagsDetected;


        }
        public void EnumerateAllTags()
        {
            var hmiSoftware = GetHmiSoftware();
            
            List<HmiTag> hmiTagsToDelete = new List<HmiTag>();

            foreach (var tag in hmiSoftware.Tags)
            {
                if (tag.Name.ToString().EndsWith("_Renamed"))
                {
                    hmiTagsToDelete.Add(tag);
                }
                // EnumerateTagBlockUserGroups(group);
            }
            //foreach (var table in plcSoftware.TagTableGroup.TagTables)
            //{
            //    foreach (var tag in table.Tags)
            //    {
            //        var name = tag.Name;
            //        if (tag.Name.ToString().EndsWith("_Renamed"))
            //        {
            //            tag.Delete();
            //        }
            //    }
            //}          
        }
        public void EnumerateTagBlockUserGroups(PlcTagTableUserGroup blockUserGroup)
        {
            foreach (var table in blockUserGroup.TagTables)
            {
               foreach (var tag in table.Tags)
                {
                    var name = tag.Name;
                    if (tag.Name.ToString().EndsWith("_Renamed"))
                    {
                        tag.Delete();
                    }
                }
            }
            foreach (var group in blockUserGroup.Groups)
            {
                EnumerateTagBlockUserGroups(group);
            }
        }
        public void ExportPlcTable(PlcTagTable table, PlcTagTableUserGroup userGroup)
        {
            new XmlTable(table, userGroup);
        }
        public void ExportPlcTable(PlcTagTable table, PlcTagTableSystemGroup userGroup)
        {
            new XmlTable(table, userGroup);
        }
        public void ImportPlcTable(XmlTable table, PlcTagTableUserGroup userGroup)
        {
            string folder = System.IO.Directory.GetCurrentDirectory() + '\\';
            string file_name = folder + table.Name + ".xml";
            table.Xml.Save(file_name);

            if (File.Exists(file_name) && userGroup != null)
            {
                userGroup.TagTables.Import(new FileInfo(file_name), ImportOptions.Override);
                File.Delete(file_name);
            }
        }
        public void ImportPlcTable(PlcTagTable table, PlcTagTableSystemGroup systemGroup)
        {
            string folder = System.IO.Directory.GetCurrentDirectory() + '\\';
            string file_name = folder + table.Name + ".xml";



            if (File.Exists(file_name))
            {
                systemGroup.TagTables.Import(new FileInfo(file_name), ImportOptions.Override);
            }
        }
        public List<XmlTable> Tables = new List<XmlTable>();

 
    }
}
