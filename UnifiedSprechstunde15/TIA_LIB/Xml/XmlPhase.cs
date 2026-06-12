using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.HW.Features;
using System.Drawing;
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
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.ExternalSources;
using System.Xml.Linq;
using System.Xml;
using TIA_LIB;
using Siemens.Engineering.Compiler;
using TIA_LIB.Devices;
using TIA_LIB.Xml.Network;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Threading;


namespace TIA_LIB.Xml
{
    public class XmlPhase : XmlBlock
    {
        public XmlPhase(
            XmlOperation op, 
            string name,
            int countP,
            bool p01_type, double p01_sp_max, double p01_sp_min, int p01_numb_points, string p01_unit, string p01_txt_list,
            bool p02_type, double p02_sp_max, double p02_sp_min, int p02_numb_points, string p02_unit, string p02_txt_list,
            bool p03_type, double p03_sp_max, double p03_sp_min, int p03_numb_points, string p03_unit, string p03_txt_list,
            bool p04_type, double p04_sp_max, double p04_sp_min, int p04_numb_points, string p04_unit, string p04_txt_list,
            bool p05_type, double p05_sp_max, double p05_sp_min, int p05_numb_points, string p05_unit, string p05_txt_list
            ) : base(op.UserGroup, op.Name + "_" + name, op.Unit.Name + "_" + op.Name + "_" + name + ".xml", "Xml/EmptyPhase.xml", name, false, name)
        {
            Op = op;
            _CountP = countP;

            _p01_sp_min = p01_sp_min;
            _p02_sp_min = p02_sp_min;
            _p03_sp_min = p03_sp_min;
            _p04_sp_min = p04_sp_min;
            _p05_sp_min = p05_sp_min;

            _p01_sp_max = p01_sp_max;
            _p02_sp_max = p02_sp_max;
            _p03_sp_max = p03_sp_max;
            _p04_sp_max = p04_sp_max;
            _p05_sp_max = p05_sp_max;

            _p01_type = p01_type;
            _p02_type = p02_type;
            _p03_type = p03_type;
            _p04_type = p04_type;
            _p05_type = p05_type;

            XmlCall call = null;
            string calledBlockName;
            XmlNetwork network;
            _Name = name;
            phName = name;

         

            calledBlockName = Op.Name + "_Interlocks";
            network = Op.FindNetwork("Interlocks");

            if (network == null)
            {
                network = Op.GetNetwork("Interlocks");
                call = network.GetCall(calledBlockName, "FB", "LocalVariable", "Interlocks");
                network.SetParameter(call, "en", "Input", "Bool");
            }


            call = null;

            //Preconditions
            calledBlockName = "fbIntlck50";
            network = Op.FindNetwork("Preconditions " + name);

            if (network == null)
            {
                network = Op.GetNetwork("Preconditions " + name);
                call = network.GetCall(calledBlockName, "FB", "LocalVariable", "precond_" + name);
                network.SetParameter(call, "en", "Input", "Bool");
                network.SetParameter(call, "START", "Input", "Bool", name + "|START");

                //for (var i = 1; i <= 50; i++)
                //{
                    //var networkSource = network.Xml.Descendants("NetworkSource").FirstOrDefault();
                    //var deleteWire = networkSource.Descendants(_Namespace + "NameCon").Where(el => el.Attribute("Name").Value != null && el.Attribute("Name").Value == "IN" + (i < 10 ? "0" + i.ToString() : i.ToString())).FirstOrDefault();
                    //if (deleteWire != null) deleteWire.Parent.Remove();

                //    network.SetParameter(call, "IN" + (i < 10 ? "0" + i.ToString() : i.ToString()), "Input", "Bool", Op.Name + "_Preconditions" + "|" + phName + "_PRECOND_" + (i < 10 ? "0" + i.ToString() : i.ToString()));
                //}
            }
            else
            {
                if (call == null)
                {
                    call = network.GetCall(calledBlockName, "FB", "LocalVariable", "precond_" + name);
                }
            }

            call = null;


            //Sfc control
            calledBlockName = "fbControlSfc";
            network = Op.FindNetwork("Sfc control " + name);

            if (network == null)
            {
                _isNew = true;

                network = Op.GetNetwork("Sfc control " + name);
                call = network.GetCall(calledBlockName, "FB", "LocalVariable", "sfc_" + name);

                network.SetParameter(call, "en", "Input", "Bool");
                network.SetParameter(call, "isUnit", "Input", "Bool", "False");
                network.SetParameter(call, "isOp", "Input", "Bool", "False");
                network.SetParameter(call, "isPh", "Input", "Bool", "True");
                network.SetParameter(call, "InterfaceInOut", "InOut", "DWord", null + "|" + "InterfaceInOut");
                network.SetParameter(call, "InterfaceIn", "Input", "DWord", name + "|" + "InterfaceOut");
                network.SetParameter(call, "InterfaceOut", "Output", "DWord", name + "|" + "InterfaceIn");
                network.SetParameter(call, "PRECOND_SSS", "Input", "Bool", "precond_" + name + "|" + "QLOCK");
                network.SetParameter(call, "PRECOND_IDLE", "Input", "Bool", null + "|" + "IDLE");
                network.SetParameter(call, "Reset", "Input", "Bool", "HMI|" + "RESET_DELAYED", true);
            }

            else
            {
                if (call == null)
                {
                    call = network.GetCall(calledBlockName, "FB", "LocalVariable", "sfc_" + name);
                }
            }

            var interlock = GetInterlock(Op.Name);

            for (var i = 1; i <= 50; i++)
            {
                interlock.GetLocalInstance("PRECOND_" + (i < 10 ? "0" + i.ToString() : i.ToString()), "Bool");
            }

            for (var i = 1; i <= 50; i++)
            {
                interlock.GetLocalInstance(phName + "_PRECOND_" + (i < 10 ? "0" + i.ToString() : i.ToString()), "Bool");
            }

            //network.SetParameter(call, "CountP", "Input", "USint", countP.ToString());

            network.SetParameter(call, "P01_TYPE", "Input", "Bool", p01_type.ToString());
            network.SetParameter(call, "P02_TYPE", "Input", "Bool", p02_type.ToString());
            network.SetParameter(call, "P03_TYPE", "Input", "Bool", p03_type.ToString());
            network.SetParameter(call, "P04_TYPE", "Input", "Bool", p04_type.ToString());
            network.SetParameter(call, "P05_TYPE", "Input", "Bool", p05_type.ToString());

            network.SetParameter(call, "P01_NUM_POINTS", "Input", "USInt", p01_numb_points.ToString());
            network.SetParameter(call, "P02_NUM_POINTS", "Input", "USInt", p02_numb_points.ToString());
            network.SetParameter(call, "P03_NUM_POINTS", "Input", "USInt", p03_numb_points.ToString());
            network.SetParameter(call, "P04_NUM_POINTS", "Input", "USInt", p04_numb_points.ToString());
            network.SetParameter(call, "P05_NUM_POINTS", "Input", "USInt", p05_numb_points.ToString());

            network.SetParameter(call, "P01_SPMAX", "Input", "Real", p01_sp_max.ToString().Contains(".") ? p01_sp_max.ToString() : p01_sp_max.ToString() + ".0");
            network.SetParameter(call, "P02_SPMAX", "Input", "Real", p02_sp_max.ToString().Contains(".") ? p02_sp_max.ToString() : p02_sp_max.ToString() + ".0");
            network.SetParameter(call, "P03_SPMAX", "Input", "Real", p03_sp_max.ToString().Contains(".") ? p03_sp_max.ToString() : p03_sp_max.ToString() + ".0");
            network.SetParameter(call, "P04_SPMAX", "Input", "Real", p04_sp_max.ToString().Contains(".") ? p04_sp_max.ToString() : p04_sp_max.ToString() + ".0");
            network.SetParameter(call, "P05_SPMAX", "Input", "Real", p05_sp_max.ToString().Contains(".") ? p05_sp_max.ToString() : p05_sp_max.ToString() + ".0");

            network.SetParameter(call, "P01_SPMIN", "Input", "Real", p01_sp_min.ToString().Contains(".") ? p01_sp_min.ToString() : p01_sp_min.ToString() + ".0");
            network.SetParameter(call, "P02_SPMIN", "Input", "Real", p02_sp_min.ToString().Contains(".") ? p02_sp_min.ToString() : p02_sp_min.ToString() + ".0");
            network.SetParameter(call, "P03_SPMIN", "Input", "Real", p03_sp_min.ToString().Contains(".") ? p03_sp_min.ToString() : p03_sp_min.ToString() + ".0");
            network.SetParameter(call, "P04_SPMIN", "Input", "Real", p04_sp_min.ToString().Contains(".") ? p04_sp_min.ToString() : p04_sp_min.ToString() + ".0");
            network.SetParameter(call, "P05_SPMIN", "Input", "Real", p05_sp_min.ToString().Contains(".") ? p05_sp_min.ToString() : p05_sp_min.ToString() + ".0");

            network.SetParameter(call, "P01_UNIT", "Input", "String", "'" + p01_unit + "'");
            network.SetParameter(call, "P02_UNIT", "Input", "String", "'" + p02_unit + "'");
            network.SetParameter(call, "P03_UNIT", "Input", "String", "'" + p03_unit + "'");
            network.SetParameter(call, "P04_UNIT", "Input", "String", "'" + p04_unit + "'");
            network.SetParameter(call, "P05_UNIT", "Input", "String", "'" + p05_unit + "'");

            network.SetParameter(call, "P01_TXT_LIST", "Input", "String", "'" + p01_txt_list + "'");
            network.SetParameter(call, "P02_TXT_LIST", "Input", "String", "'" + p02_txt_list + "'");
            network.SetParameter(call, "P03_TXT_LIST", "Input", "String", "'" + p03_txt_list + "'");
            network.SetParameter(call, "P04_TXT_LIST", "Input", "String", "'" + p04_txt_list + "'");
            network.SetParameter(call, "P05_TXT_LIST", "Input", "String", "'" + p05_txt_list + "'");

            Console.WriteLine("Block phase was found " + name);
        }

        public XmlInterlock GetInterlock(string name)
        {
            XmlInterlock interlock;

            if (!this.Op.Interlocks.TryGetValue(name, out interlock))
            {
                interlock = new XmlInterlock(this);
                this.Op.Interlocks.Add(name, interlock);

            }
            return interlock;
        }

        public void SetFaceplateProperties()
        {
            var Portal = SiemensPortal.Current;

            //if (_isNew)
            //{

                //change ranges to input setpoint
                //int i = 1;
                //while (i <= _CountP)
                //{
                    //var tagName   = Op.Unit.Name  + "_" + Op.Name + "_sfc_" + _Name + "_P0" + i + "_SP";
                    //var max_value = Op.Unit.Name + "_" + Op.Name + "_sfc_" + _Name + "_P0" + i + "_SPMAX";
                    //var min_value = Op.Unit.Name + "_" + Op.Name + "_sfc_" + _Name + "_P0" + i + "_SPMIN";

            if (_CountP >= 1 && !_p01_type) Portal.SetTagRangeFix(Op.Unit.Name + "_" + Op.Name + "_sfc_" + _Name + "_P01_SP", _p01_sp_max.ToString(), _p01_sp_min.ToString());
            if (_CountP >= 1 && !_p01_type) Portal.SetTagGMP(Op.Unit.Name + "_" + Op.Name + "_sfc_" + _Name + "_P01_SP", false, false, false);

            if (_CountP >= 2 && !_p02_type) Portal.SetTagRangeFix(Op.Unit.Name + "_" + Op.Name + "_sfc_" + _Name + "_P02_SP", _p02_sp_max.ToString(), _p02_sp_min.ToString());
            if (_CountP >= 2 && !_p02_type) Portal.SetTagGMP(Op.Unit.Name + "_" + Op.Name + "_sfc_" + _Name + "_P02_SP", false, false, false);

            if (_CountP >= 3 && !_p03_type) Portal.SetTagRangeFix(Op.Unit.Name + "_" + Op.Name + "_sfc_" + _Name + "_P03_SP", _p03_sp_max.ToString(), _p03_sp_min.ToString());
            if (_CountP >= 3 && !_p03_type) Portal.SetTagGMP(Op.Unit.Name + "_" + Op.Name + "_sfc_" + _Name + "_P03_SP", false, false, false);

            if (_CountP >= 4 && !_p04_type) Portal.SetTagRangeFix(Op.Unit.Name + "_" + Op.Name + "_sfc_" + _Name + "_P04_SP", _p04_sp_max.ToString(), _p04_sp_min.ToString());
            if (_CountP >= 4 && !_p04_type) Portal.SetTagGMP(Op.Unit.Name + "_" + Op.Name + "_sfc_" + _Name + "_P04_SP", false, false, false);

            if (_CountP == 5 && !_p05_type ) Portal.SetTagRangeFix(Op.Unit.Name + "_" + Op.Name + "_sfc_" + _Name + "_P05_SP", _p05_sp_max.ToString(), _p05_sp_min.ToString());
            if (_CountP == 5 && !_p05_type) Portal.SetTagGMP(Op.Unit.Name + "_" + Op.Name + "_sfc_" + _Name + "_P05_SP", false, false, false);

            //INT
            if (_CountP >= 1 && _p01_type) Portal.SetTagRangeFix(Op.Unit.Name + "_" + Op.Name + "_sfc_" + _Name + "_P01_SP_INT", _p01_sp_max.ToString(), _p01_sp_min.ToString());
            if (_CountP >= 1 && _p01_type) Portal.SetTagGMP(Op.Unit.Name + "_" + Op.Name + "_sfc_" + _Name + "_P01_SP_INT", false, false, false);

            if (_CountP >= 2 && _p02_type) Portal.SetTagRangeFix(Op.Unit.Name + "_" + Op.Name + "_sfc_" + _Name + "_P02_SP_INT", _p02_sp_max.ToString(), _p02_sp_min.ToString());
            if (_CountP >= 2 && _p02_type) Portal.SetTagGMP(Op.Unit.Name + "_" + Op.Name + "_sfc_" + _Name + "_P02_SP_INT", false, false, false);

            if (_CountP >= 3 && _p03_type) Portal.SetTagRangeFix(Op.Unit.Name + "_" + Op.Name + "_sfc_" + _Name + "_P03_SP_INT", _p03_sp_max.ToString(), _p03_sp_min.ToString());
            if (_CountP >= 3 && _p03_type) Portal.SetTagGMP(Op.Unit.Name + "_" + Op.Name + "_sfc_" + _Name + "_P03_SP_INT", false, false, false);

            if (_CountP >= 4 && _p04_type) Portal.SetTagRangeFix(Op.Unit.Name + "_" + Op.Name + "_sfc_" + _Name + "_P04_SP_INT", _p04_sp_max.ToString(), _p04_sp_min.ToString());
            if (_CountP >= 4 && _p04_type) Portal.SetTagGMP(Op.Unit.Name + "_" + Op.Name + "_sfc_" + _Name + "_P04_SP_INT", false, false, false);

            if (_CountP == 5 && _p05_type) Portal.SetTagRangeFix(Op.Unit.Name + "_" + Op.Name + "_sfc_" + _Name + "_P05_SP_INT", _p05_sp_max.ToString(), _p05_sp_min.ToString());
            if (_CountP == 5 && _p05_type) Portal.SetTagGMP(Op.Unit.Name + "_" + Op.Name + "_sfc_" + _Name + "_P05_SP_INT", false, false, false);
            //    i++;
            //}
            //}

            Console.WriteLine("Property for phase " + Name + " was completed");
        }


        public XmlOperation Op;
        private int _CountP;
        private string _Name;
        private bool _isNew;
        public string phName;

        private double _p01_sp_min;
        private double _p02_sp_min;
        private double _p03_sp_min;
        private double _p04_sp_min;
        private double _p05_sp_min;

        private double _p01_sp_max;
        private double _p02_sp_max;
        private double _p03_sp_max;
        private double _p04_sp_max;
        private double _p05_sp_max;

        private bool _p01_type;
        private bool _p02_type;
        private bool _p03_type;
        private bool _p04_type;
        private bool _p05_type;
        private XNamespace _Namespace = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v4";
    }
}
