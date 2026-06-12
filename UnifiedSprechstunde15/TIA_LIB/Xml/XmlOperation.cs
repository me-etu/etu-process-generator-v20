using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Documents;
using System.Xml.Linq;
using Siemens.Engineering.SW.Blocks;
using TIA_LIB.Devices;
using TIA_LIB.Xml.Network;


namespace TIA_LIB.Xml
{
    public class XmlOperation : XmlBlock
    {
        public XmlOperation(XmlUnit unit, string name): base(unit.UserGroup, name, unit.Name + "_" + name + ".xml", "Xml/EmptyOperation.xml", "", false)
        {
            XmlCall call = null;
            Unit  = unit;
            string calledBlockName;
            XmlNetwork network;

           //pHs selector
            calledBlockName = "fbControlSfc";
            network = FindNetwork(Name);
            
            if (network == null)
            {
                network = GetNetwork(Name);
                call = network.GetCall(calledBlockName, "FB", "LocalVariable", "sfc_" + Name);
                network.SetParameter(call, "en", "Input", "Bool");
                network.SetParameter(call, "isUnit", "Input", "Bool", "False");
                network.SetParameter(call, "isOp", "Input", "Bool", "True");
                network.SetParameter(call, "isPh", "Input", "Bool", "False");
                
                network.SetParameter(call, "IDLE",      "Output", "Bool",  null + "|" + "IDLE");
                network.SetParameter(call, "HELD",      "Output", "Bool",  null + "|" + "HELD");
                network.SetParameter(call, "START",      "Output", "Bool",  null + "|" + "START");
                network.SetParameter(call, "STOP",      "Output", "Bool",  null + "|" + "STOP");
                network.SetParameter(call, "ABORT",     "Output", "Bool",  null + "|" + "ABORT");
                network.SetParameter(call, "PAUSE",     "Output", "Bool",  null + "|" + "PAUSE");
                network.SetParameter(call, "COMPLETED", "Output", "Bool",  null + "|" + "COMPLETED");

                network.SetParameter(call, "InterfaceInOut", "InOut", "DWord", null + "|" + "Interface");

                //new XmlPrecondition(this, Name + "_Preconditions");


                //calledBlockName = Name + "_Preconditions";

                //network = FindNetwork(Name + "_Preconditions");

                //if (network == null)
                //{
                //    network = GetNetwork(Name + "_Preconditions");
                //    call = network.GetCall(calledBlockName, "FB", "LocalVariable", Name + "_Preconditions");
                //    network.SetParameter(call, "en", "Input", "Bool");

                //}

                    //network = GetNetwork(Name + "_Interlocks", "", true);
                    //network.Block.GetLocalInstance("intelocks_" + Name, "Array[1..50] of Bool");
                    //call = network.GetCall(calledBlockName, "FB", "LocalVariable", "intelocks_" + Name);
                }

            Console.WriteLine("PHs selector for operation " + Name + " was found ");
        }

        public XmlPhase GetPH(string name, 
            int countP,
            bool p01_type, double p01_sp_max, double p01_sp_min, int p01_numb_points, string p01_unit, string p01_txt_list,
            bool p02_type, double p02_sp_max, double p02_sp_min, int p02_numb_points, string p02_unit, string p02_txt_list,
            bool p03_type, double p03_sp_max, double p03_sp_min, int p03_numb_points, string p03_unit, string p03_txt_list,
            bool p04_type, double p04_sp_max, double p04_sp_min, int p04_numb_points, string p04_unit, string p04_txt_list,
            bool p05_type, double p05_sp_max, double p05_sp_min, int p05_numb_points, string p05_unit, string p05_txt_list)
        {
            XmlPhase ph;

            if (!Phases.TryGetValue(name, out ph))
            { 
                ph = new XmlPhase(this, name,
                    countP,
                p01_type, p01_sp_max, p01_sp_min, p01_numb_points, p01_unit, p01_txt_list,
                p02_type, p02_sp_max, p02_sp_min, p02_numb_points, p02_unit, p02_txt_list,
                p03_type, p03_sp_max, p03_sp_min, p03_numb_points, p03_unit, p03_txt_list,
                p04_type, p04_sp_max, p04_sp_min, p04_numb_points, p04_unit, p04_txt_list,
                p05_type, p05_sp_max, p05_sp_min, p05_numb_points, p05_unit, p05_txt_list);

                Phases.Add(name, ph);

                var network = FindNetwork(name);
                XmlCall call;

                if (network == null)
                {
                    network = GetNetwork(name);
                    call = network.GetCall(Name + "_" + name, "FB", "LocalVariable", name);
                    network.SetParameter(call, "en", "Input", "Bool");
                    network.SetParameter(call, "InterfaceIn", "Input", "DWord", "sfc_" + name + "|" + "InterfaceOut");
                    network.SetParameter(call, "InterfaceOut", "Input", "DWord", "sfc_" + name + "|" + "InterfaceIn");
                    network.SetParameter(call, "InterfaceInOut", "InOut", "DWord", null + "|" + "Interface");
                }
                else
                {
                    call = network.FindCall(Name + "_" + name, "FB", "LocalVariable", name);

                }

                if (countP >= 1) network.SetParameter(call, "SP01", "Input", "Real", "sfc_" + name + "|" + "SP01_OUT");
                if (countP >= 2) network.SetParameter(call, "SP02", "Input", "Real", "sfc_" + name + "|" + "SP02_OUT");
                if (countP >= 3) network.SetParameter(call, "SP03", "Input", "Real", "sfc_" + name + "|" + "SP03_OUT");
                if (countP >= 4) network.SetParameter(call, "SP04", "Input", "Real", "sfc_" + name + "|" + "SP04_OUT");
                if (countP == 5) network.SetParameter(call, "SP05", "Input", "Real", "sfc_" + name + "|" + "SP05_OUT");

            }

            return ph;
        
        }
        public void SetFaceplateProperties()
        {
        }

        
        public Dictionary<string, XmlPhase> Phases = new Dictionary<string, XmlPhase>();
        public XmlUnit Unit;
        public Dictionary<string, XmlInterlock> Interlocks = new Dictionary<string, XmlInterlock>();
        private XNamespace _Namespace = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v4";
    }
}
