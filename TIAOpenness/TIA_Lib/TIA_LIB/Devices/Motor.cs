using System;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.HmiUnified.UI.Screens;
using System.Collections.Generic;
using Siemens.Engineering.HmiUnified.UI.Base;
using TIA_LIB.Xml;
using TIA_LIB.Xml.Network;
using TIA_LIB.SignalStaging;
using System.Xml.Linq;
using System.Linq;
using System.Drawing;

namespace TIA_LIB.Devices
{
    public class Motor : GeneratedObject
    {
        public Motor(XmlUnit unit, string tagName, int iconType = 0, int interlockCount = 0, int SafeInterlockCount = 0, bool mon_on = false, bool mon_const = false, int tp_number = -1, int mon_t = -1, string networkComment = "") : base(unit, tagName)
        {
            Portal = SiemensPortal.Current;

            XmlPin andOut = null;
            XmlCall call = null;
            XmlCall hasLock = null;
            XmlCall interlock = null;

            int indexInterlock = 0;
            int indexInterlockSafe = 0;
            var calledBlockName = "fbMotS";
            var fbOnReference = SignalStagingInventory.InputReference("FB_ON_" + tagName);
            var ctrlReference = SignalStagingInventory.OutputReference("CTRL_" + tagName);
            XNamespace _Namespace = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v4";
            XNamespace _emtpyNamespace = "";
            bool isNewNetwork = false;

            var network = unit.FindNetwork(tagName);

            if (network == null || XmlPlant.Current.SetErrorToUnitBlock)
            {
                isNewNetwork = true;
            }

            if (network != null) hasLock = network.FindCall("fbIntlck7", "FB", "LocalVariable", tagName + "_" + "Interlock1");

            if (network == null || network != null && (interlockCount != 0 || SafeInterlockCount != 0) && hasLock == null)
            {
                if (network != null && (interlockCount != 0 || SafeInterlockCount != 0) && hasLock == null)
                {
                    var networkSource = network.Xml.Descendants("NetworkSource").FirstOrDefault();
                    XNamespace blockNamespace = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v4";

                    var deleteWire = networkSource.Descendants(blockNamespace + "NameCon").Where(el => el.Attribute("Name").Value != null && el.Attribute("Name").Value == "en").FirstOrDefault();
                    if (deleteWire != null) deleteWire.Parent.Remove();

                    if (interlockCount != 0)
                    {
                        deleteWire = networkSource.Descendants(blockNamespace + "NameCon").Where(el => el.Attribute("Name").Value != null && el.Attribute("Name").Value == "LOCK").FirstOrDefault();
                        if (deleteWire != null) deleteWire.Parent.Remove();
                    }

                    if (SafeInterlockCount != 0)
                    {
                        deleteWire = networkSource.Descendants(blockNamespace + "NameCon").Where(el => el.Attribute("Name").Value != null && el.Attribute("Name").Value == "S_LOCK").FirstOrDefault();
                        if (deleteWire != null) deleteWire.Parent.Remove();
                    }
                }

                network = unit.GetNetwork(tagName, networkComment);

                call = network.GetCall(calledBlockName, "FB", "LocalVariable", tagName);
                //network.SetParameter(call, "en", "Input", andOut);

                if (interlockCount != 0 || SafeInterlockCount != 0)
                {
                    var and = new XmlLogic(network, XmlLogicType.And);
                    XmlLogic orLock = null;
                    XmlPin orLockOut = null;
                    XmlLogic orLockSafe = null;
                    XmlPin orLockSafeOut = null;
                    int actIndexInterlock = 1;
                    network.SetPin(and, "in1", "Bool", "True");

                    andOut = and.GetPin("out");

                    if (interlockCount != 0)
                    {
                        for (indexInterlock = interlockCount + SafeInterlockCount; indexInterlock > SafeInterlockCount; indexInterlock--)
                        {
                            interlock = network.GetCall("fbIntlck7", "FB", "LocalVariable", tagName + "_" + "Interlock" + (indexInterlock));
                            network.SetParameter(interlock, "en", "Input", andOut);

                            if (interlockCount > 1)
                            {
                                if (orLock == null)
                                {
                                    orLock = new XmlLogic(network, XmlLogicType.Or);
                                }

                                network.SetPin(orLock, "in" + (interlockCount + SafeInterlockCount - indexInterlock + 1), "Bool", tagName + "_" + "Interlock" + (actIndexInterlock++) + "|" + "QLOCK");
                            }

                            if (interlockCount + SafeInterlockCount > 1)
                            {
                                network.SetParameter(interlock, "L_LOCK", "Input", "Bool", tagName + "|" + "QLOCK");
                            }
                        }

                        if (interlockCount > 1)
                        {
                            orLockOut = orLock.GetPin("out");
                            network.SetParameter(call, "LOCK", "Input", orLockOut);

                        }
                        else if (interlockCount == 1)
                        {
                            network.SetParameter(call, "LOCK", "Input", "Bool", tagName + "_" + "Interlock" + actIndexInterlock++ + "|" + "QLOCK");
                        }
                    }

                    if (SafeInterlockCount != 0)
                    {
                        for (indexInterlockSafe = SafeInterlockCount; indexInterlockSafe > 0; indexInterlockSafe--)
                        {
                            interlock = network.GetCall("fbIntlck7", "FB", "LocalVariable", tagName + "_" + "Interlock" + (indexInterlockSafe));
                            network.SetParameter(interlock, "en", "Input", andOut);

                            if (SafeInterlockCount > 1)
                            {
                                if (orLockSafe == null)
                                {
                                    orLockSafe = new XmlLogic(network, XmlLogicType.Or);
                                }

                                network.SetPin(orLockSafe, "in" + (SafeInterlockCount - indexInterlockSafe + 1), "Bool", tagName + "_" + "Interlock" + (actIndexInterlock++) + "|" + "QLOCK");
                            }

                            if (interlockCount + SafeInterlockCount > 1)
                            {
                                network.SetParameter(interlock, "L_LOCK", "Input", "Bool", tagName + "|" + "QLOCK");
                            }
                        }

                        if (SafeInterlockCount > 1)
                        {
                            orLockSafeOut = orLockSafe.GetPin("out");
                            network.SetParameter(call, "S_LOCK", "Input", orLockSafeOut);
                        }
                        else if (SafeInterlockCount == 1)
                        {
                            network.SetParameter(call, "S_LOCK", "Input", "Bool", tagName + "_" + "Interlock" + actIndexInterlock + "|" + "QLOCK");
                        }
                    }


                    interlock = network.FindCall("fbIntlck7", "FB", "LocalVariable", tagName + "_" + "Interlock" + (interlockCount + SafeInterlockCount));
                    network.SetParameter(interlock, "LAST_CALL", "Input", "Bool", "True");


                    call = network.FindCall(calledBlockName, "FB", "LocalVariable", tagName);
                    network.SetParameter(call, "en", "Input", andOut);

                    network.SetParameter(call, "HAS_LOCK", "Input", "Bool", "True");

                    network.SetParameter(call, "FB_ON", "Input", "Bool", fbOnReference.Value, fbOnReference.IsGlobal); //Input verknüpfen mit FB_ON Variable
                    network.SetParameter(call, "QCMD_ON", "Output", "Bool", ctrlReference.Value, ctrlReference.IsGlobal); //Output verknüpfen mit CTRL Variable

                    if ((interlockCount != 0 || SafeInterlockCount != 0) && hasLock == null)
                    {
                        var networkSource = network.Xml.Descendants("NetworkSource").FirstOrDefault();
  
                        var deleteWire = networkSource.Descendants(_emtpyNamespace + "Wire").Where(el => el.Elements().Count() > 1 &&
                                                                                                  el.Elements().ElementAtOrDefault(0).Attribute("Name") != null &&
                                                                                                  el.Elements().ElementAtOrDefault(1).Attribute("Name") != null
                                                                                                  ).FirstOrDefault();

                        var elements = deleteWire.Descendants("NameCon");
                        int i;

                        List<string> tmpList = new List<string>();

                        for (i = 1; i < elements.Count() - 1; i++)
                        {
                            tmpList.Add(elements.ElementAt(i).Attribute("UId").Value);
                        }

                        tmpList.Reverse();

                        for (i = 1; i < elements.Count() - 1; i++)
                        {
                            elements.ElementAt(i).SetAttributeValue("UId", tmpList.ElementAt(i - 1));
                        }

                    }
                }
                if (interlockCount == 0 && SafeInterlockCount == 0)
                {
                    call = network.GetCall(calledBlockName, "FB", "LocalVariable", tagName);
                    network.SetParameter(call, "en", "Input", "Bool");
                }
            }
            else
            {
                if (call == null)
                {
                    call = network.GetCall(calledBlockName, "FB", "LocalVariable", tagName);
                }
            }

            network.SetParameter(call, "TYP_ICON", "Input", "Usint", iconType.ToString());
            network.SetParameter(call, "CFG", "Input", "typeMotorCfg", "cfgData|" + unit.Name + "_" + tagName, true);
            network.SetParameter(call, "RESET", "Input", "Bool", "HMI|" + "RESET", true);
            if(isNewNetwork) network.SetParameter(call, "PROT", "Input", "Bool", "F_Safe|" + "HSS", true);

            network.SetParameter(call, "MON_ON", "Input", "Bool", mon_on.ToString());
            if (PlcProject.StagingMode == SignalStagingMode.GeneratedDbUdt)
            {
                network.SetParameter(call, "FB_ON", "Input", "Bool", fbOnReference.Value, fbOnReference.IsGlobal);
                network.SetParameter(call, "QCMD_ON", "Output", "Bool", ctrlReference.Value, ctrlReference.IsGlobal);
            }
     
            if (mon_const)
            {
                if (mon_t != -1)
                {
                    network.SetParameter(call, "MON_T_ST", "Input", "Real", mon_t.ToString());
                    network.SetParameter(call, "MON_T_DN", "Input", "Real", mon_t.ToString());
                }

            }
            else
            {
                if (tp_number != -1)
                {
                    network.SetParameter(call, "MON_T_ST", "Input", "Real", "TP" + tp_number + "|OUT", true, true);
                    network.SetParameter(call, "MON_T_DN", "Input", "Real", "TP" + tp_number + "|OUT", true, true);

                }
            }

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
        }

        public string ScreenName;
        public SiemensPortal Portal;

    }
}
