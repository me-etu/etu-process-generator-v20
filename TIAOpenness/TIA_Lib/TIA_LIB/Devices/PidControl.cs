using System;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.HmiUnified.UI.Screens;
using System.Collections.Generic;
using Siemens.Engineering.HmiUnified.UI.Base;
using TIA_LIB.Xml;
using TIA_LIB.Xml.Network;
using System.Xml.Linq;
using System.Linq;

namespace TIA_LIB.Devices
{
    public class PidControl : GeneratedObject
    {

        public PidControl(XmlUnit unit, string tagName, int iconType = 0, int numbDecPoints = 2, string unity = "", int numbDecPointsOut = 1, string unityOut = "%", string networkComment = "") : base(unit, tagName)
        {
            Portal = SiemensPortal.Current;
            XmlCall call = null;
            var calledBlockName = "fbPIDCtrl";
            XNamespace _Namespace = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v4";
            XNamespace _emtpyNamespace = "";
            bool isNewNetwork = false;

            var network = unit.FindNetwork(tagName);

            if (network == null || XmlPlant.Current.SetErrorToUnitBlock)
            {
                isNewNetwork = true;
                _isNew = network == null;
            }

            if (network == null)
            {
                network = unit.GetNetwork(tagName, networkComment);
                call = network.GetCall(calledBlockName, "FB", "LocalVariable", tagName);
                network.SetParameter(call, "en", "Input", "Bool");;
            }
            else
            {
                if (call == null)
                {
                    call = network.GetCall(calledBlockName, "FB", "LocalVariable", tagName);
                }
            }

            network.SetParameter(call, "TYP_ICON", "Input", "Usint", iconType.ToString());
            network.SetParameter(call, "CFG", "Input", "typePIDCfg", "cfgData|" + unit.Name + "_" + tagName, true);
            network.SetParameter(call, "UNIT_SP_AV", "Input", "String", "'" + unity + "'");
            network.SetParameter(call, "NUM_POINTS_SP_AV", "Input", "Usint", numbDecPoints.ToString());
            network.SetParameter(call, "UNIT_OUT", "Input", "String", "'" + unityOut + "'");
            network.SetParameter(call, "NUM_POINTS_OUT", "Input", "Usint", numbDecPointsOut.ToString());
            network.SetParameter(call, "RESET", "Input", "Bool", "HMI|" + "RESET", true);

            if (isNewNetwork)
            {
                network = unit.FindNetwork(unit.Name);
                call = network.FindCall("fbUnit", "FB", "LocalVariable", unit.Name);

                bool isNew = false;

                if (network != null && call != null)
                {

                    XmlLogic orStatus = null;

                    foreach (var wire in network.Wires)
                    {
                        var checkWire = wire.Value.Xml.Descendants(_Namespace + "NameCon").Where(el => el.Attribute("Name").Value != null && el.Attribute("Name").Value == "ALARM").FirstOrDefault();
                        if (checkWire == null) checkWire = wire.Value.Xml.Descendants(_emtpyNamespace + "NameCon").Where(el => el.Attribute("Name").Value != null && el.Attribute("Name").Value == "ALARM").FirstOrDefault();

                        if (checkWire != null)
                        {
                            var outElement = checkWire.ElementsBeforeSelf().Where(el => el.Name == _Namespace + "NameCon" && el.Attribute("Name").Value != null && el.Attribute("Name").Value == "out").FirstOrDefault();
                            if (outElement == null) outElement = checkWire.ElementsBeforeSelf().Where(el => el.Name == _emtpyNamespace + "NameCon" && el.Attribute("Name").Value != null && el.Attribute("Name").Value == "out").FirstOrDefault();
                            if (outElement != null)
                            {
                                var outId = Int32.Parse(outElement.Attribute("UId").Value);
                                if (network.Logics.TryGetValue(outId, out orStatus))
                                {
                                    break;
                                }
                            }
                        }
                    }

                    int value = 0;

                    if (orStatus != null)
                    {
                        value = orStatus.InputCount;
                    }
                    else
                    {
                        var networkSource = network.Xml.Descendants("NetworkSource").FirstOrDefault();

                        var deleteWire = networkSource.Descendants(_Namespace + "NameCon").Where(el => el.Attribute("Name").Value != null && el.Attribute("Name").Value == "ALARM").FirstOrDefault();
                        if (deleteWire != null) deleteWire.Parent.Remove();

                        orStatus = new XmlLogic(network, XmlLogicType.Or);
                        network.SetPin(orStatus, "in1", "Bool", "SIM_ERR", true);
                        network.SetPin(orStatus, "in2", "Bool", tagName + "|" + "QERR");

                        isNew = true;
                    }

                    if (!isNew) network.SetPin(orStatus, "in" + ++value, "Bool", tagName + "|" + "QERR");
                    if (isNew) network.SetParameter(call, "ALARM", "Input", orStatus.GetPin("out"));

                }
            }

                Console.WriteLine("Block was found " + unit.Name + "." + tagName);
        }
        public override void SetFaceplateProperties()
        {
            if (_isNew)
            {
                //Set GMP
                bool isTagGMP = true;

                if (isTagGMP)
                {
                    Portal.SetTagGMP(Name + "_OperatorSetpoint", false, false);
                }

                isTagGMP = true;

                if (isTagGMP)
                {
                    Portal.SetTagGMP(Name + "_OperatorManualValue", false, false);
                    Portal.SetTagGMP(Name + "_OperatorManualValueOut1", false, false);
                    Portal.SetTagGMP(Name + "_OperatorManualValueOut2", false, false);

                    Portal.SetTagGMP(Name + "_GAIN", false, false);
                    Portal.SetTagGMP(Name + "_TI", false, false);
                    Portal.SetTagGMP(Name + "_TD", false, false);
                    Portal.SetTagGMP(Name + "_DWEIGHTING", false, false);
                    Portal.SetTagGMP(Name + "_PWEIGHTING", false, false);
                    Portal.SetTagGMP(Name + "_TD_FILTR_RATIO", false, false);
                }

                //change ranges
                Portal.SetTagRange(Name + "_OperatorSetpoint", Name + "_SP_MAX", Name + "_SP_MIN");
                Portal.SetTagRange(Name + "_OperatorManualValue", Name + "_OUT_MAX",Name + "_OUT_MIN");
                Portal.SetTagRange(Name + "_OperatorManualValueOut1", Name + "_OUT1_MAX", Name + "_OUT1_MIN");
                Portal.SetTagRange(Name + "_OperatorManualValueOut2", Name + "_OUT2_MAX", Name + "_OUT2_MIN");

                Portal.SetTagRangeFix(Name + "_GAIN", "1000000", "0");
                Portal.SetTagRangeFix(Name + "_TI", "1000000", "0");
                Portal.SetTagRangeFix(Name + "_TD", "100000", "0");
                Portal.SetTagRangeFix(Name + "_DWEIGHTING", "1", "0");
                Portal.SetTagRangeFix(Name + "_PWEIGHTING", "1", "0");
                Portal.SetTagRangeFix(Name + "_TD_FILTR_RATIO", "1000000", "0");

                Console.WriteLine("Property pid control for tag range " + Name + " was completed");
            }

        }

        public string ScreenName;
        public SiemensPortal Portal;
        private bool _isNew;
    }
}
