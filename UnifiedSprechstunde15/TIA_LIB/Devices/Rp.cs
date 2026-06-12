using Siemens.Engineering.HmiUnified.UI.Screens;
using System;
using TIA_LIB.Xml;
using TIA_LIB.Xml.Network;
using System.Linq;

namespace TIA_LIB
{
    public class Rp
    {
        public Rp(XmlBlock block, string name, bool type, double sp_max, double sp_min, string unity, int numbDecPoints, string listname) 
        {
            _Name = name;
            _Type = type;

            XmlCall call = null;
            var calledBlockName = "fbRP";

            PlcProject.RPs.Add(this);
            var network = block.FindNetwork(name);

            if (network == null)
            {
                block.Instances.Add("true" + "|" + name + "|" + calledBlockName);
                network = block.GetNetwork(name);
                call = network.GetCall("fbRP", "FB", "GlobalVariable", name);
                network.SetParameter(call, "en", "Input", "Bool");
                network.SetParameter(call, "RESET", "Input", "Bool", "HMI|" + "RESET", true);
            }
            else
            {
                if (call == null)
                {
                    call = network.GetCall(calledBlockName, "FB", "GlobalVariable", name);
                }
            }

            network.SetParameter(call, "TYPE",          "Input", "Bool", type.ToString());
            network.SetParameter(call, "TEXTLIST_NAME", "Input", "String", "'" + listname.ToString() + "'");
            network.SetParameter(call, "MTR_MAX",       "Input", "Real", sp_max.ToString().Contains(".") ? sp_max.ToString() : sp_max.ToString() + ".0");
            network.SetParameter(call, "MTR_MIN",       "Input", "Real", sp_min.ToString().Contains(".") ? sp_min.ToString() : sp_min.ToString() + ".0");
            network.SetParameter(call, "UNIT",          "Input", "String", "'" + unity.ToString() + "'");
            network.SetParameter(call, "NUM_POINTS",    "Input", "USint", numbDecPoints.ToString());
            
            Console.WriteLine("Recipe parameter " + name + " was found");
        }
        public void SetFaceplateProperties()
        {
            var Portal = SiemensPortal.Current;
            string max_value = "";
            string min_value = "";
            int top = 0;
            int left = 0;

            HmiScreen screen;

            //Portal.SetTagGMP(_Name + "_QOUT", false, false);

            screen = Portal.GetHmiScreen(_Name);

            if (screen != null)
            {
                //set property
                ////change ranges to input setpoint
                max_value = _Name + "_MTR_MAX";
                min_value = _Name + "_MTR_MIN";
          
                if(!_Type)
                {
                    Portal.SetTagRange(_Name + "_SP",  max_value, min_value);
                    Portal.SetTagRange(_Name + "_TR_MAX", max_value, min_value);
                    Portal.SetTagRange(_Name + "_TR_MIN", max_value, min_value);
                    Portal.SetTagRange(_Name + "_AR_MAX", max_value, min_value);
                    Portal.SetTagRange(_Name + "_AR_MIN", max_value, min_value);

                    //Portal.SetTagGMP(_Name + "_SP", false, false);                  
                }
                else
                {
                    //Portal.SetTagGMP(_Name + "_SP_INT", false, false);
                }

                //CREATE ICON ON SCREEN
                screen = Portal.GetHmiScreen("swRecipeContent");

                var isWindowCreated = Portal.GetHmiScreenItem(screen, _Name);

                if(isWindowCreated == null)
                {
                    if (Portal.GetHmiScreen("swRecipeContent").ScreenItems.Count() > 0)
                    {
                        if (!_Type)
                        {
                            top = Int32.Parse(Portal.GetHmiScreen("swRecipeContent").ScreenItems.Where(attleft => attleft.GetAttribute("Left").ToString() == "0")
                                                                                                .Select(att => att.GetAttribute("Top")).ToList().Max().ToString());
                            top = top + 41;
                        }
                        else
                        {
                            left = 1126;
                            if (Portal.GetHmiScreen("swRecipeContent").ScreenItems.Where(attleft => attleft.GetAttribute("Left").ToString() != "0").Count() > 0)
                            {
                                top = Int32.Parse(Portal.GetHmiScreen("swRecipeContent").ScreenItems.Where(attleft => attleft.GetAttribute("Left").ToString() != "0")
                                                                                                    .Select(att => att.GetAttribute("Top")).ToList().Max().ToString());
                                top = top + 41;
                            }
                        }
                    }

                    if (screen != null)
                    {
                        Portal.SetAttributeScreenWindow(Portal.CreateScreenWindow(screen, _Name), top, left);
                    }
                }

                //SET PROPERTY FOR ACT SCREEN
                screen = Portal.GetHmiScreen(_Name + "_Act");

                if (screen != null)
                {
                    //set property
                    ////change ranges to input setpoint
                    max_value = _Name + "_QOUT_TR_MAX";
                    min_value = _Name + "_QOUT_TR_MIN";

                    if (!_Type)
                    {
                        Portal.SetTagRange(_Name + "_QOUT", max_value, min_value);
                        //Portal.SetTagRange(_Name + "_QOUT_AR_MAX", max_value, min_value);
                        //Portal.SetTagRange(_Name + "_QOUT_AR_MIN", max_value, min_value);
                    }
                    else
                    {
                        //Portal.SetTagGMP(_Name + "_SP_INT", false, false);
                    }

                    //GMP
                    Portal.SetTagGMP(_Name + "_QOUT", false, false);
                }

                //CREATE ICON ON SCREEN ACT
                screen = Portal.GetHmiScreen("swRecipeContent_Act");

                top = 0;
                left = 0;

                isWindowCreated = Portal.GetHmiScreenItem(screen, _Name + "_Act");

                if (isWindowCreated == null)
                {
                    if (Portal.GetHmiScreen("swRecipeContent_Act").ScreenItems.Count() > 0)
                    {
                        if (!_Type)
                        {
                            top = Int32.Parse(Portal.GetHmiScreen("swRecipeContent_Act").ScreenItems.Where(attleft => attleft.GetAttribute("Left").ToString() == "0")
                                                                                                .Select(att => att.GetAttribute("Top")).ToList().Max().ToString());
                            top = top + 41;
                        }
                        else
                        {
                            left = 1126;
                            if (Portal.GetHmiScreen("swRecipeContent_Act").ScreenItems.Where(attleft => attleft.GetAttribute("Left").ToString() != "0").Count() > 0)
                            {
                                top = Int32.Parse(Portal.GetHmiScreen("swRecipeContent_Act").ScreenItems.Where(attleft => attleft.GetAttribute("Left").ToString() != "0")
                                                                                                    .Select(att => att.GetAttribute("Top")).ToList().Max().ToString());
                                top = top + 41;
                            }
                        }
                    }

                    if (screen != null)
                    {
                        Portal.SetAttributeScreenWindow(Portal.CreateScreenWindow(screen, _Name + "_Act"), top, left);
                    }
                }

                Console.WriteLine("Property for recipe parameter " + _Name + " was completed");
            }
        }

        private string _Name;
        private bool _Type;
        public string ScreenName;
        public SiemensPortal Portal;
       
    }
}
