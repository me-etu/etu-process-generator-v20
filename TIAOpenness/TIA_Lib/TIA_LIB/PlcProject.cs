using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TIA_LIB.Devices;
using TIA_LIB.Xml;
using System.IO;
using Siemens.Engineering.HmiUnified.UI.Screens;
using Siemens.Engineering.HmiUnified.UI.Events;
using Siemens.Engineering.HmiUnified.UI.Dynamization;
using System.Threading;
using Siemens.Engineering.Hmi.Communication;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using TIA_LIB.SignalStaging;

namespace TIA_LIB
{
    public class PlcProject
    {
        public PlcProject()
        {
 
            Portal = new SiemensPortal();
            Portal.CompilePlc();
            Plant = new XmlPlant();
        }

        public XmlUnit AddUnit(string unit_name)
        {
            var unit = Plant.GetUnit(unit_name);

            return unit;
        }

        public XmlOperation AddOP(string unit_name, string op_name)
        {
            var unit=Plant.GetUnit(unit_name);

            return unit.GetOP(op_name);
        }
        public XmlPhase AddPH(string unit_name, 
            string op_name, 
            string ph_name, 
            int countP = 0, 
            bool p01_type = false, double p01_sp_min = 0, double p01_sp_max = 0, int p01_numb_points = 2, string p01_unit = "", string p01_txt_list = "",
            bool p02_type = false, double p02_sp_min = 0, double p02_sp_max = 0, int p02_numb_points = 2, string p02_unit = "", string p02_txt_list = "",
            bool p03_type = false, double p03_sp_min = 0, double p03_sp_max = 0, int p03_numb_points = 2, string p03_unit = "", string p03_txt_list = "",
            bool p04_type = false, double p04_sp_min = 0, double p04_sp_max = 0, int p04_numb_points = 2, string p04_unit = "", string p04_txt_list = "",
            bool p05_type = false, double p05_sp_min = 0, double p05_sp_max = 0, int p05_numb_points = 2, string p05_unit = "", string p05_txt_list = "")
        {
            var unit = Plant.GetUnit(unit_name);

            var op = unit.GetOP(op_name);

            var ph = op.GetPH(ph_name, countP, 
                p01_type, p01_sp_max, p01_sp_min, p01_numb_points, p01_unit, p01_txt_list,
                p02_type, p02_sp_max, p02_sp_min, p02_numb_points, p02_unit, p02_txt_list,
                p03_type, p03_sp_max, p03_sp_min, p03_numb_points, p03_unit, p03_txt_list,
                p04_type, p04_sp_max, p04_sp_min, p04_numb_points, p04_unit, p04_txt_list,
                p05_type, p05_sp_max, p05_sp_min, p05_numb_points, p05_unit, p05_txt_list);

            return ph;
        }

        private string GetCallerLineComment(string callerFilePath, int callerLineNumber)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(callerFilePath) || callerLineNumber <= 0 || !File.Exists(callerFilePath)) return "";

                var line = File.ReadLines(callerFilePath).Skip(callerLineNumber - 1).FirstOrDefault();
                if (string.IsNullOrWhiteSpace(line)) return "";

                return ExtractTrailingLineComment(line);
            }
            catch
            {
                return "";
            }
        }

        private string ExtractTrailingLineComment(string line)
        {
            bool inString = false;
            bool inChar = false;
            bool escaped = false;

            for (int i = 0; i < line.Length - 1; i++)
            {
                char c = line[i];

                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if ((inString || inChar) && c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (!inChar && c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (!inString && c == '\'')
                {
                    inChar = !inChar;
                    continue;
                }

                if (!inString && !inChar && c == '/' && line[i + 1] == '/')
                {
                    var code = line.Substring(0, i).TrimEnd();
                    if (!code.EndsWith(";")) return "";

                    return line.Substring(i + 2).Trim();
                }
            }

            return "";
        }

        public Valve AddValve(string unit_name, string name, int iconType = 0, int interlockCount = 0, int interlockSafeCount = 0, bool mon_opn = false, bool mon_cls = false, bool mon_const = false, bool qualityBit = true, bool neg = true, int tp_number = -1, int mon_t = -1, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            var unit = Plant.GetUnit(unit_name);
            var networkComment = GetCallerLineComment(callerFilePath, callerLineNumber);
            StagingInventory.AddOutput(unit_name, "CTRL_" + name, "Bool", "Valve command " + name);
            if (mon_opn) StagingInventory.AddInput(unit_name, "FB_OPN_" + name, "Bool", "Valve open feedback " + name);
            if (mon_cls) StagingInventory.AddInput(unit_name, "FB_CLS_" + name, "Bool", "Valve closed feedback " + name);
            if (qualityBit && mon_opn) StagingInventory.AddInput(unit_name, "FB_OPN_" + name + "_QB", "Bool", "Valve open feedback quality bit " + name);
            if (qualityBit && mon_cls) StagingInventory.AddInput(unit_name, "FB_CLS_" + name + "_QB", "Bool", "Valve closed feedback quality bit " + name);
            var device = unit.GetDevice(name) as Valve;

            if(device == null)
            {
                device = new Valve(unit, name, iconType, interlockCount, interlockSafeCount, mon_opn, mon_cls, mon_const, qualityBit, neg, tp_number, mon_t, networkComment);
            }

            Valves.Add(new TagValve("FB_OPN_" + name, "FB_CLS_" + name, "CTRL_" + name));
            return device;
        }

        public Tp AddTp(int number, double def_value, double sp_min, double sp_max, string unity = "", bool isSafety = false, int numbDecPoints = 1, bool type = false, string listname = "")
        {
            if(fbTPs == null)
            {
                fbTPs = new XmlBlock(null, "fbTPs", "fbTPs.xml", "Xml/EmptyBlock.xml", "", false, "Technical parameters");
            }
            return  new Tp(fbTPs, "TP" + number.ToString(), isSafety, type, sp_max, sp_min, def_value, unity, numbDecPoints, listname); 

        }

        public Rp AddRp(int number, double sp_min, double sp_max, string unity = "", int numbDecPoints = 1, bool type = false, string listname = "")
        {
            if (fbRPs == null)
            {
                fbRPs = new XmlBlock(null, "fbRPs", "fbRPs.xml", "Xml/EmptyBlock.xml", "", false, "Recipe parameters");

            }
            return new Rp(fbRPs, "RP" + number.ToString(), type, sp_max, sp_min, unity, numbDecPoints, listname);

        }
        public ValveControl AddValveControl(string unit_name, string name, int iconType = 0, int interlockCount = 0, int interlockSafeCount = 0, string unity = "%", int numbDecPoints = 1, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            var unit = Plant.GetUnit(unit_name);
            var networkComment = GetCallerLineComment(callerFilePath, callerLineNumber);
            StagingInventory.AddOutput(unit_name, "CTRL_" + name, "Int", "ValveControl setpoint " + unity + ", " + numbDecPoints + " decimals");
            var device = unit.GetDevice(name) as ValveControl;

            if (device == null)
            {
                device = new ValveControl(unit, name, iconType, interlockCount, interlockSafeCount, numbDecPoints, unity, networkComment);
                ControlValves.Add(new TagControlValve("CTRL_" + name));
            }

            return device;
        }

        public Motor AddMotor(string unit_name, string name, int iconType = 0, int interlockCount = 0, int interlockSafeCount = 0, bool mon_on = false, bool mon_const = false, int tp_number = -1, int mon_t = -1, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            var unit = Plant.GetUnit(unit_name);
            var networkComment = GetCallerLineComment(callerFilePath, callerLineNumber);
            StagingInventory.AddInput(unit_name, "FB_ON_" + name, "Bool", "Motor running feedback " + name);
            StagingInventory.AddOutput(unit_name, "CTRL_" + name, "Bool", "Motor command " + name);
            var device = unit.GetDevice(name) as Motor;

            if (device == null)
            {
                device = new Motor(unit, name, iconType, interlockCount, interlockSafeCount, mon_on, mon_const, tp_number, mon_t, networkComment);
                Motors.Add(new TagMotor("FB_ON_" + name, "CTRL_" + name));
            }

            return device;
        }

        public MotorControl AddMotorControl(string unit_name, string name, int iconType = 0, int interlockCount = 0, int interlockSafeCount = 0, string unity = "%", int numbDecPoints = 1, bool mon_on = false, bool mon_const = false, int tp_number = -1, int mon_t = -1, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            var unit = Plant.GetUnit(unit_name);
            var networkComment = GetCallerLineComment(callerFilePath, callerLineNumber);
            if (mon_on) StagingInventory.AddInput(unit_name, "FB_ON_" + name, "Bool", "Controlled motor running feedback " + name);
            StagingInventory.AddOutput(unit_name, "CTRL_" + name, "Int", "MotorControl " + unity + ", " + numbDecPoints + " decimals");
            var device = unit.GetDevice(name) as MotorControl;

            if (device == null)
            {
                device = new MotorControl(unit, name, iconType, interlockCount, interlockSafeCount, numbDecPoints, unity, mon_on, mon_const, tp_number, mon_t, networkComment);
            }
             return device;
        }

        public Analog AddAnalog(string unit_name, string name, int iconType = 0, int instanceCount = 0, string unity = "", int numbDecPoints = 1, float limMin = 0.0f, float limMax = 500.0f, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            var unit = Plant.GetUnit(unit_name);
            var networkComment = GetCallerLineComment(callerFilePath, callerLineNumber);
            StagingInventory.AddInput(unit_name, "IN_" + name, "Int", "Analog " + name + ", " + unity + ", " + numbDecPoints + " decimals, " + limMin + ".." + limMax);
            var device = unit.GetDevice(name) as Analog;

            if (device == null)
            {
                device = new Analog(unit, name, iconType, unity, numbDecPoints, instanceCount, limMin, limMax, networkComment);
                Analogs.Add(new TagAnalog("IN_" + name));
            }

            return device;
        }

        public Digital AddDigital(string unit_name, string name, int iconType = 0, int colorType = 0, int instanceCount = 0, bool qualityBit = false, bool neg = false, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            var unit = Plant.GetUnit(unit_name);
            var networkComment = GetCallerLineComment(callerFilePath, callerLineNumber);
            StagingInventory.AddInput(unit_name, "IN_" + name, "Bool", "Digital input " + name);
            if (qualityBit) StagingInventory.AddInput(unit_name, "IN_" + name + "_QB", "Bool", "Digital input quality bit " + name);
            var device = unit.GetDevice(name) as Digital;

            if (device == null)
            {
                device = new Digital(unit, name, iconType, colorType, instanceCount, qualityBit, neg, networkComment);
                Digitals.Add(new TagDigital("IN_" + name));
            }

            return device;
        }


        public PidControl AddPidControl(string unit_name, string name, int iconType = 0,  string unity = "", int numbDecPoints = 1, string unityOut = "%", int numbDecPointsOut = 2, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            var unit = Plant.GetUnit(unit_name);
            var networkComment = GetCallerLineComment(callerFilePath, callerLineNumber);
            StagingInventory.AddOutput(unit_name, name + "_SP", "Int", "PID setpoint " + unity + ", " + numbDecPoints + " decimals");
            StagingInventory.AddOutput(unit_name, name + "_OUT", "Int", "PID output " + unityOut + ", " + numbDecPointsOut + " decimals");
            var device = unit.GetDevice(name) as PidControl;

            if (device == null)
            {
                device = new PidControl(unit, name, iconType, numbDecPoints, unity, numbDecPointsOut, unityOut, networkComment);
            }

            return device;
        }
        public void CreateFaceplates()
        {
            foreach (KeyValuePair<string, XmlUnit> ta_item in Plant.Units)
            {
                XmlUnit ta = ta_item.Value;

                foreach (KeyValuePair<string, GeneratedObject> dev_item in ta.Devices)
                {
                    GeneratedObject device = dev_item.Value;
                    device.SetFaceplateProperties();
                }
            }

        }

        public class TagValve 
        {
            public TagValve(string fb_opn = "", string fb_cls = "", string ctrl = "", bool qualityBit = true) 
            {
                FB_OPN = fb_opn;
                FB_CLS = fb_cls;
                CTRL = ctrl;
                QualityBit = qualityBit;
            }
            public string FB_OPN;
            public string FB_CLS;
            public string CTRL;
            public bool QualityBit;

        }

        public class TagAnalog
        {
            public TagAnalog(string name = "")
            {
                Name = name;
            }
            public string Name;
        }

        public class TagDigital
        {
            public TagDigital(string name = "", bool qualityBit = true)
            {
                Name = name;
                QualityBit = qualityBit;
            }
            public string Name;
            public bool QualityBit;
        }

        public class TagControlValve
        {
            public TagControlValve(string ctrl = "")
            {
                Ctrl = ctrl;
            }
            public string Ctrl;
        }

        public class TagMotor
        {
            public TagMotor(string fb_on = "", string ctrl = "", bool qualityBit = true)
            {
                FB_ON = fb_on;
                CTRL = ctrl;
                QualityBit = qualityBit;
            }
            public string FB_ON;
            public string CTRL;
            public bool QualityBit;

        }

        public List<TagValve> Valves = new List<TagValve>();
        public List<TagAnalog> Analogs = new List<TagAnalog>();
        public List<TagDigital> Digitals = new List<TagDigital>();
        public List<TagControlValve> ControlValves = new List<TagControlValve>();
        public List<TagMotor> Motors = new List<TagMotor>();


        public XmlPlant Plant;
        public SiemensPortal Portal;
        public static bool DeleteXml = true;
        public static XmlBlock fbTPs;
        public static XmlBlock fbRPs;
        public static List<Tp> TPs = new List<Tp>();
        public static List<Rp> RPs = new List<Rp>();
        public List<string> Messages = new List<string>();
        public SignalStagingInventory StagingInventory = new SignalStagingInventory();
        public static SignalStagingMode StagingMode = SignalStagingMode.MarkerMemory;
        public static void Upload()
        {
            var delayUnit = Task.Delay(TimeSpan.FromSeconds(5));
            while (!delayUnit.IsCompleted)
            {
                Thread.Sleep(TimeSpan.FromSeconds(5));
            }          

            if (StagingMode == SignalStagingMode.GeneratedDbUdt && XmlUdt.Udts != null)
            {
                foreach (var udt in XmlUdt.Udts)
                {
                    udt.Upload();
                }
            }

            var datablocksBeforePlant = XmlDatablock.Datablocks;
            if (StagingMode == SignalStagingMode.GeneratedDbUdt && datablocksBeforePlant != null)
            {
                foreach (var datablock in datablocksBeforePlant)
                {
                    datablock.Upload();
                }
            }

            XmlPlant.Current.Upload();

            var datablocks = XmlDatablock.Datablocks;
            
            if (datablocks != null)
            {
                foreach (var datablock in datablocks)
                {
                    datablock.Upload();
                }
            }

            if(fbTPs != null)
            {
                fbTPs.Upload();
            }

            if (fbRPs != null)
            {
                fbRPs.Upload();
            }

            var units = XmlBlock.Blocks.OfType<XmlUnit>();
            var ops = XmlBlock.Blocks.OfType<XmlOperation>();
            var phs = XmlBlock.Blocks.OfType<XmlPhase>();
            var preconds = XmlBlock.Blocks.OfType<XmlInterlock>();
            var interlocks = XmlBlock.Blocks.OfType<XmlInterlock>();

            foreach (var ph in phs)
            {
                ph.Upload();
            }

            foreach (var precond in preconds)
            {
                precond.Upload();
            }

            foreach (var op in ops)
            {
                op.Upload();
            }

            foreach (var interlock in interlocks)
            {
                interlock.Upload();
            }

            foreach (var unit in units)
            {
                unit.Upload();
            }

            SiemensPortal.Current.CompilePlc();

        }
        public static void Wait(int s = 1)
        {
            var delayUnit = Task.Delay(TimeSpan.FromSeconds(s));
            while (!delayUnit.IsCompleted)
            {
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
        }
        public void GenerateSiVarc()
        {
           
            Portal.SiVarcGenerate(SiemensPortal.Current.GetHmiSoftware().Name, Portal.PlcSoftware.Name);
            SiemensPortal.Current.CompilePlc();
        }

        public void UserInterface()
        {
            foreach(var unit in Plant.Units.Values)
            {
                foreach(var device in unit.Devices)
                {
                    device.Value.SetFaceplateProperties();
                }

                unit.SetFaceplateProperties();

                foreach (var op in unit.Operations.Values)
                {
                    op.SetFaceplateProperties();

                    foreach(var ph in op.Phases.Values)
                    {
                        ph.SetFaceplateProperties();

                        foreach (var instance in ph.LocalInstance)
                        {
                            break;
                            if (instance.Value.Datatype.Contains("fbOpMsg"))
                            {
                                var Portal = SiemensPortal.Current;

                                HmiScreen screen;

                                //SET PROPERTY FOR STANDARDVIEW SCREEN
                                screen = Portal.GetHmiScreen("opMsg_" + unit.Name + "_"  + ph.Name + "_" + instance.Key);

                                if (screen != null && false)
                                {
                                    Messages.Add(screen.Name);

                                    //set property
                                    //change ranges to input setpoint

                                    if (Portal.GetHmiScreenItem(screen, "ioSP") == null) continue;

                                    var max_value = unit.Name + "_" +  ph.Name + "_" + instance.Key + "_SP_MAX";
                                    var min_value = unit.Name + "_" + ph.Name + "_" + instance.Key + "_SP_MIN";

                                    Portal.SetTagRange(unit.Name + "_" + ph.Name + "_" + instance.Key + "_SP", max_value, min_value);
                                }
                            }
                        }
                    } 
                }

                foreach(var instance in unit.LocalInstance)
                {
                    break;
                    if(instance.Value.Datatype.Contains("fbOpMsg"))
                    {
                        var Portal = SiemensPortal.Current;

                        HmiScreen screen;

                        //SET PROPERTY FOR STANDARDVIEW SCREEN
                        screen = Portal.GetHmiScreen("opMsg_" + unit.Name + "_" + instance.Key);

                        if (screen != null)
                        {
                            Messages.Add(screen.Name);

                            //set property
                            //change ranges to input setpoint

                            if (Portal.GetHmiScreenItem(screen, "ioSP") == null) continue;

                            var max_value = unit.Name + "_" + instance.Key + "_SP_MAX";
                            var min_value = unit.Name + "_" + instance.Key + "_SP_MIN";

                            Portal.SetTagRange(unit.Name + "_" + instance.Key + "_SP", max_value, min_value);
                        }
                    }
                }
            }

            foreach (var tp in TPs)
            {
                tp.SetFaceplateProperties();
            }

            foreach (var rp in RPs)
            {
                rp.SetFaceplateProperties();
            }

            foreach (var message in Messages)
            {
                var Portal = SiemensPortal.Current;

                HmiScreen screen = Portal.GetHmiScreen("012_PopUps");

                int top = 0;
                int left = 0;

                var screenWindowMessage = Portal.GetHmiScreenItem(screen, message);
                screenWindowMessage.SetAttribute("Visible", false);

                if (screenWindowMessage == null)
                {
                    if (Portal.GetHmiScreen("012_PopUps").ScreenItems.Count() > 0)
                    {
                        top = Int32.Parse(Portal.GetHmiScreen("012_PopUps").ScreenItems.Where(attleft => attleft.GetAttribute("Left").ToString() == "0")
                                                                                       .Select(att => att.GetAttribute("Top")).ToList().Max().ToString());
                        top = top + 85;
                  
                    }

                    if (screen != null)
                    {
                        var screenWindow = Portal.SetAttributeScreenWindow(Portal.CreateScreenWindow(screen, message), top, left);

                        PropertyEventHandler propertyEvent = screenWindow.PropertyEventHandlers.Create("Visible", Siemens.Engineering.HmiUnified.UI.Events.PropertyEventType.Change);

                        propertyEvent.Script.ScriptCode = "\n" + "showMessage(value, item.Name)";


                    }
                }
            }
        }


        private void CreateGeneratedDbUdtStaging()
        {
            XmlUdt.Udts = new List<XmlUdt>();

            foreach (var unitPlan in StagingInventory.Units)
            {
                XmlUnit unit;
                if (Plant.Units.TryGetValue("fb" + unitPlan.OriginalName, out unit))
                {
                    unit.GetInterfaceMember("Input", "hwIN", "hwIN_" + unitPlan.SafeName);
                    unit.GetInterfaceMember("Output", "hwOUT", "hwOUT_" + unitPlan.SafeName);
                }
            }

            StagingInventory.LogPlan();

            foreach (var udt in StagingInventory.CreateUdts())
            {
                Console.WriteLine("Prepared UDT XML: " + udt.Name);
            }

            StagingInventory.CreateDbIo();
            Console.WriteLine("Prepared DB XML: dbIO");
        }
        public void CreateTags()
        {
            if (StagingMode == SignalStagingMode.GeneratedDbUdt)
            {
                CreateGeneratedDbUdtStaging();
                return;
            }

            Console.WriteLine("Signal staging mode: Marker memory");
            var plc = SiemensPortal.Current.GetPlcSoftware();   //Aktuelle Instance aufrufen
            int countByte = 500;    //Merkeradresse für Valves
            int countBit = 0;

            var table = plc.TagTableGroup.TagTables.Find("TEMP");   //Suche PLC-Variablentabelle "TEMP"
            if (table == null)                                      //Erstelle wenn nicht gefunden
            {
                table = plc.TagTableGroup.TagTables.Create("TEMP");
            }


            foreach (var valve in Valves)
            {
                if (valve.FB_OPN != "") //Erstelle FB_OPN und Quality Bit Variable
                {

                    if (table.Tags.Find(valve.FB_OPN) == null)      
                    {
                        table.Tags.Create(valve.FB_OPN, "Bool", "M" + countByte + "." + countBit++);
                        if (countBit == 7)
                        {
                            countByte++;
                            countBit = 0;
                        }
                    }
                    
                    if (valve.QualityBit)
                    {
                        if (table.Tags.Find(valve.FB_OPN + "_QB") == null)  
                        {
                            table.Tags.Create(valve.FB_OPN + "_QB", "Bool", "M" + countByte + "." + countBit++);
                            if (countBit == 7)
                            {
                                countByte++;
                                countBit = 0;
                            }
                        } 
                    }
                }
                                
                if (valve.FB_CLS != "") //Erstelle FB_CLS und Quality Bit Variable
                {
                    if (table.Tags.Find(valve.FB_CLS) == null)  
                    {
                        table.Tags.Create(valve.FB_CLS, "Bool", "M" + countByte + "." + countBit++);
                        if (countBit == 7)
                        {
                            countByte++;
                            countBit = 0;
                        }
                    }

                    if (valve.QualityBit)
                    {
                        if (table.Tags.Find(valve.FB_CLS + "_QB") == null)
                        {
                            table.Tags.Create(valve.FB_CLS + "_QB", "Bool", "M" + countByte + "." + countBit++);
                            if (countBit == 7)
                            {
                                countByte++;
                                countBit = 0;
                            }
                        }
                    }
                }
                
                if (table.Tags.Find(valve.CTRL) == null)    //Erstelle Valve Control Variable
                {
                    table.Tags.Create(valve.CTRL, "Bool", "M" + countByte + "." + countBit++);
                    if (countBit == 7)
                    {
                        countByte++;
                        countBit = 0;
                    }
                }
       
            }

            countByte = 1000;   //Adressbereich für Digitale Eingänge
            countBit = 0;

            foreach (var digital in Digitals)
            {
                if (digital.Name != "")     //Erstelle Digitale Eingänge und Quality Bit
                {
                    if (table.Tags.Find(digital.Name) == null)
                    {
                        table.Tags.Create(digital.Name, "Bool", "M" + countByte + "." + countBit++);
                        if (countBit == 7)
                        {
                            countByte++;
                            countBit = 0;
                        }
                    }
                    if (table.Tags.Find(digital.Name + "_QB") == null)
                    {
                        if (digital.QualityBit)
                        {
                            table.Tags.Create(digital.Name + "_QB", "Bool", "M" + countByte + "." + countBit++);
                            if (countBit == 7)
                            {
                                countByte++;
                                countBit = 0;
                            }
                        }
                    }
                }
            }


            int countWord = 1500;   //Merkeradressbereich für Control valves

            foreach (var valve in ControlValves)
            {
                if (valve.Ctrl != "")   //Erstelle Integer für Control Valves
                {
                    if (table.Tags.Find(valve.Ctrl) == null)
                    {
                        table.Tags.Create(valve.Ctrl, "Int", "MW" + countWord);
                        countWord += 2;
                    }
                                           
                }
            }

            countWord = 2000;   //Merkeradressbereich für Analoge Eingänge

            foreach (var analog in Analogs)
            {
                if (analog.Name != "")      //Erstelle Integer für Analoge Eingänge
                {
                    if (table.Tags.Find(analog.Name) == null)
                    {
                        table.Tags.Create(analog.Name, "Int", "MW" + countWord);
                        countWord += 2;
                    }
  
                }
            }

            countByte = 2500;   //Merkeradressbereich für Motor Eingänge und Ausgänge
            countBit = 0;

            foreach (var motor in Motors)
            {
                if (motor.CTRL != "")      //Erstelle Ausgang für Motor
                {
                    if (table.Tags.Find(motor.CTRL) == null)
                    {
                        table.Tags.Create(motor.CTRL, "Bool", "M" + countByte + "." + countBit++);
                        if (countBit == 7)
                        {
                            countByte++;
                            countBit = 0;
                        }

                        table.Tags.Create(motor.FB_ON, "Bool", "M" + countByte + "." + countBit++);
                        if (countBit == 7)
                        {
                            countByte++;
                            countBit = 0;
                        }
                    }

                }
            }
        }

    }
}
