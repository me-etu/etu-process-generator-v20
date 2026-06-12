using Siemens.Engineering.HmiUnified.UI.Screens;
using System.Linq;
using System;
using TIA_LIB.Xml;
using TIA_LIB.Xml.Network;


namespace TIA_LIB
{
    public class Tp
    {
        public Tp(XmlBlock block, string name, bool isSafety, bool type, double sp_max, double sp_min, double def_value, string unity, int numbDecPoints, string listname) 
        {
            _IsSafety = isSafety;
            _Name = name;
            _Type = type;
            XmlCall call = null;
            var calledBlockName = "fbTP";
            PlcProject.TPs.Add(this);
            var network = block.FindNetwork(name);

            if (network == null)
            {
                block.Instances.Add("true" + "|" + name + "|" + calledBlockName);
                network = block.GetNetwork(name);
                call = network.GetCall("fbTP", "FB", "GlobalVariable", name);
                network.SetParameter(call, "en", "Input", "Bool");
            }
            else
            {
                if (call == null)
                {
                    call = network.GetCall(calledBlockName, "FB", "GlobalVariable", name);
                }
            }

            network.SetParameter(call, "IsSafety",      "Input", "Bool", isSafety.ToString());
            network.SetParameter(call, "TYPE",          "Input", "Bool", type.ToString());
            network.SetParameter(call, "TEXTLIST_NAME", "Input", "String", "'" + listname.ToString() + "'");
            network.SetParameter(call, "SP_MAX",        "Input", "Real", sp_max.ToString().Contains(".") ? sp_max.ToString() : sp_max.ToString() + ".0");
            network.SetParameter(call, "SP_MIN",        "Input", "Real", sp_min.ToString().Contains(".") ? sp_min.ToString() : sp_min.ToString() + ".0");
            network.SetParameter(call, "SP_DEF",        "Input", "Real", def_value.ToString().Contains(".") ? def_value.ToString() : def_value.ToString() + ".0");
            network.SetParameter(call, "UNIT",          "Input", "String", "'" + unity.ToString() + "'");
            network.SetParameter(call, "NUM_POINTS",    "Input", "USint", numbDecPoints.ToString());
            network.SetParameter(call, "RESET",         "Input", "Bool", "HMI|" + "RESET", true);

            Console.WriteLine("Technical parameter " + name + " was found");
        }
        public void SetFaceplateProperties()
        {

        }

        private string _Name;
        private bool _Type;
        private bool _IsSafety;
        public string ScreenName;
        public SiemensPortal Portal;
    }
}
