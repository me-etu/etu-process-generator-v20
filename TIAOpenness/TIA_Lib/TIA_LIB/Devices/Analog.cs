using System;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.HmiUnified.UI.Screens;
using System.Collections.Generic;
using Siemens.Engineering.HmiUnified.UI.Base;
using TIA_LIB.Xml;
using TIA_LIB.Xml.Network;
using TIA_LIB.SignalStaging;
using System.Linq;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.Drawing;

namespace TIA_LIB.Devices
{
    public class Analog : GeneratedObject
    {
        public Analog(XmlUnit unit, string tagName, int iconType = 0, string unity = "", int numberDecPoints = 2, int countInstance = 0, float limValueMin = 0.0f, float limValueMax = 500.0f, string networkComment = "") : base(unit, tagName)
        {
            Portal = SiemensPortal.Current;
            _CountInstance = countInstance;
            XmlPin andOut = null;
            XmlCall call = null;
            XmlCall instance = null;
            XmlLogic orLockAH = null;
            XmlLogic orLockWH = null;
            XmlLogic orLockWL = null;
            XmlLogic orLockAL = null;
            XmlCall hasInstances = null;

            XNamespace _Namespace = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v4";
            XNamespace _emtpyNamespace = "";

            bool isNewNetwork = false;

            int indexInstance = 0;
            var calledBlockName = "fbMonAn";
            var inputReference = SignalStagingInventory.InputReference("IN_" + tagName);

            var network = unit.FindNetwork(tagName);

            if (network == null || XmlPlant.Current.SetErrorToUnitBlock)
            {
                isNewNetwork = true;
                _isNew = true;
            }

            if (network != null) hasInstances = network.FindCall(calledBlockName, "FB", "LocalVariable", tagName + "_Instance1");

            if (network == null || network != null && countInstance != 0 && hasInstances == null)
            {
                if (network != null && countInstance != 0 && hasInstances == null)
                {
                   var networkSource = network.Xml.Descendants("NetworkSource").FirstOrDefault();
                    XNamespace blockNamespace = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v4";

                    var deleteWire = networkSource.Descendants(blockNamespace + "NameCon").Where(el => el.Attribute("Name").Value != null && el.Attribute("Name").Value == "en").FirstOrDefault();
                    if (deleteWire != null) deleteWire.Parent.Remove();

                    if (countInstance != 0)
                    {
                        deleteWire = networkSource.Descendants(blockNamespace + "NameCon").Where(el => el.Attribute("Name").Value != null && el.Attribute("Name").Value == "AH").FirstOrDefault();
                        if (deleteWire != null) deleteWire.Parent.Remove();

                        deleteWire = networkSource.Descendants(blockNamespace + "NameCon").Where(el => el.Attribute("Name").Value != null && el.Attribute("Name").Value == "WH").FirstOrDefault();
                        if (deleteWire != null) deleteWire.Parent.Remove();

                        deleteWire = networkSource.Descendants(blockNamespace + "NameCon").Where(el => el.Attribute("Name").Value != null && el.Attribute("Name").Value == "WL").FirstOrDefault();
                        if (deleteWire != null) deleteWire.Parent.Remove();

                        deleteWire = networkSource.Descendants(blockNamespace + "NameCon").Where(el => el.Attribute("Name").Value != null && el.Attribute("Name").Value == "AL").FirstOrDefault();
                        if (deleteWire != null) deleteWire.Parent.Remove();
                    } 

                }

                network = unit.GetNetwork(tagName, networkComment);
                call = network.GetCall(calledBlockName, "FB", "LocalVariable", tagName);

                if (countInstance != 0)
                {
                    var and = new XmlLogic(network, XmlLogicType.And);
                    int actIndexInstance = 1;
                    network.SetPin(and, "in1", "Bool", "True");

                    andOut = and.GetPin("out");

                    for (indexInstance = countInstance; indexInstance > 0; indexInstance--)
                    {
                        instance = network.GetCall(calledBlockName, "FB", "LocalVariable", tagName + "_Instance" + (indexInstance));
                        network.SetParameter(instance, "en", "Input", andOut);

                        network.SetParameter(instance, "CFG", "Input", "typeAnalogCfg", "cfgData|" + unit.Name + "_" + tagName + "_Instance" + (indexInstance), true);
                        network.SetParameter(instance, "UNIT", "Input", "String", "'" + unity + "'");
                        network.SetParameter(instance, "NUM_POINTS", "Input", "Usint", numberDecPoints.ToString());
                        //network.SetParameter(instance, "RESET", "Input", "Bool", "HMI|" + "RESET", true);
                        network.SetParameter(instance, "IN_INT", "Input", "Int", inputReference.Value, inputReference.IsGlobal);

                        network.SetParameter(instance, "RESET", "Input", "Bool", "RESET_CYCL", true);

                        network.SetParameter(instance, "HI_LIM", "Input", "Real", limValueMax.ToString().Contains(".") ? limValueMax.ToString() : limValueMax.ToString() + ".0");
                        network.SetParameter(instance, "LO_LIM", "Input", "Real", limValueMin.ToString().Contains(".") ? limValueMin.ToString() : limValueMin.ToString() + ".0");

                        

                        if (countInstance > 1)
                        {
                            if (orLockAH == null) orLockAH = new XmlLogic(network, XmlLogicType.Or);
                            network.SetPin(orLockAH, "in" + (countInstance - indexInstance + 1), "Bool", tagName + "_Instance" + (actIndexInstance) + "|" + "QAH_M");

                            if (orLockWH == null) orLockWH = new XmlLogic(network, XmlLogicType.Or);
                            network.SetPin(orLockWH, "in" + (countInstance - indexInstance + 1), "Bool", tagName + "_Instance" + (actIndexInstance) + "|" + "QWH_M");

                            if (orLockWL == null) orLockWL = new XmlLogic(network, XmlLogicType.Or);
                            network.SetPin(orLockWL, "in" + (countInstance - indexInstance + 1), "Bool", tagName + "_Instance" + (actIndexInstance) + "|" + "QWL_M");

                            if (orLockAL == null) orLockAL = new XmlLogic(network, XmlLogicType.Or);
                            network.SetPin(orLockAL, "in" + (countInstance - indexInstance + 1), "Bool", tagName + "_Instance" + (actIndexInstance++) + "|" + "QAL_M");
                        }
                    }

                    if (countInstance > 1)
                    {
                        network.SetParameter(call, "AH", "Input", orLockAH.GetPin("out"));
                        network.SetParameter(call, "WH", "Input", orLockWH.GetPin("out"));
                        network.SetParameter(call, "WL", "Input", orLockWL.GetPin("out"));
                        network.SetParameter(call, "AL", "Input", orLockAL.GetPin("out"));

                    }
                    else if (countInstance == 1)
                    {
                        network.SetParameter(call, "AH", "Input", "Bool", tagName + "_Instance1" + "|" + "QAH_M");
                        network.SetParameter(call, "WH", "Input", "Bool", tagName + "_Instance1" + "|" + "QWH_M");
                        network.SetParameter(call, "WL", "Input", "Bool", tagName + "_Instance1" + "|" + "QWL_M");
                        network.SetParameter(call, "AL", "Input", "Bool", tagName + "_Instance1" + "|" + "QAL_M");
                    }

                    instance = network.FindCall(calledBlockName, "FB", "LocalVariable", tagName + "_Instance" + (countInstance));
                    network.SetParameter(instance, "LAST_CALL", "Input", "Bool", "True");


                    call = network.FindCall(calledBlockName, "FB", "LocalVariable", tagName);
                    network.SetParameter(call, "en", "Input", andOut);
                    network.SetParameter(call, "HAS_CALL", "Input", "Bool", "True");

                    if (countInstance != 0 && hasInstances == null)
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
                if (countInstance == 0)
                {
                    call = network.GetCall(calledBlockName, "FB", "LocalVariable", tagName);
                    network.SetParameter(call, "en", "Input", "Bool");

                }
                network.SetParameter(call, "RESET", "Input", "Bool", "HMI|" + "RESET", true);
            }
            else
            {
                if (call == null)
                {
                    call = network.GetCall(calledBlockName, "FB", "LocalVariable", tagName);
                }
            }

            network.SetParameter(call, "TYP_ICON", "Input", "Usint", iconType.ToString());          
            network.SetParameter(call, "UNIT", "Input", "String", "'" + unity + "'");
            network.SetParameter(call, "NUM_POINTS", "Input", "Usint", numberDecPoints.ToString());
            network.SetParameter(call, "HI_LIM", "Input", "Real", limValueMax.ToString().Contains(".") ? limValueMax.ToString() : limValueMax.ToString() + ".0");
            network.SetParameter(call, "LO_LIM", "Input", "Real", limValueMin.ToString().Contains(".") ? limValueMin.ToString() : limValueMin.ToString() + ".0");
            network.SetParameter(call, "MELDSYS_ON", "Input", "Bool", "True");

            if (isNewNetwork)
            {
                network.SetParameter(call, "IN_INT", "Input", "Int", inputReference.Value, inputReference.IsGlobal);
                network.SetParameter(call, "CFG", "Input", "typeAnalogCfg", "cfgData|" + unit.Name + "_" + tagName, true);
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



                    orStatus = null;

                    foreach (var wire in network.Wires)
                    {
                        var checkWire = wire.Value.Xml.Descendants(_Namespace + "NameCon").Where(el => el.Attribute("Name").Value != null && el.Attribute("Name").Value == "WARN").FirstOrDefault();
                        if (checkWire == null) checkWire = wire.Value.Xml.Descendants(_emtpyNamespace + "NameCon").Where(el => el.Attribute("Name").Value != null && el.Attribute("Name").Value == "WARN").FirstOrDefault();

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

                    value = 0;

                    if (orStatus != null)
                    {
                        value = orStatus.InputCount;
                    }
                    else
                    {
                       var networkSource = network.Xml.Descendants("NetworkSource").FirstOrDefault();

                        var deleteWire = networkSource.Descendants(_Namespace + "NameCon").Where(el => el.Attribute("Name").Value != null && el.Attribute("Name").Value == "WARN").FirstOrDefault();
                        if (deleteWire != null) deleteWire.Parent.Remove();

                        orStatus = new XmlLogic(network, XmlLogicType.Or);
                        network.SetPin(orStatus, "in1", "Bool", "SIM_WARN", true);
                        network.SetPin(orStatus, "in2", "Bool", tagName + "|" + "QWARN");
                        isNew = true;
                    }

                    if(!isNew)network.SetPin(orStatus, "in" + ++value, "Bool", tagName + "|" + "QWARN");
                    if (isNew) network.SetParameter(call, "WARN", "Input", orStatus.GetPin("out"));
                }
            }
            else if (hasInstances != null) 
            {
                for (indexInstance = countInstance; indexInstance > 0; indexInstance--)
                {
                    instance = network.FindCall(calledBlockName, "FB", "LocalVariable", tagName + "_Instance" + (indexInstance));

                    if (instance == null) continue;

                    try
                    {
                        network.SetParameter(instance, "UNIT", "Input", "String", "'" + unity + "'");
                        network.SetParameter(instance, "HI_LIM", "Input", "Real", limValueMax.ToString().Contains(".") ? limValueMax.ToString() : limValueMax.ToString() + ".0");
                        network.SetParameter(instance, "LO_LIM", "Input", "Real", limValueMin.ToString().Contains(".") ? limValueMin.ToString() : limValueMin.ToString() + ".0");
                        network.SetParameter(instance, "MELDSYS_ON", "Input", "Bool", "False");                 
                        network.SetParameter(instance, "RESET", "Input", "Bool", "RESET_CYCL", true);
                        network.SetParameter(instance, "NUM_POINTS", "Input", "Usint", numberDecPoints.ToString());
                        //var networkSource = network.Xml.Descendants("NetworkSource").FirstOrDefault();
                        //
                        //var checkWire = networkSource.Descendants(_Namespace + "Wire").Where(el => el.Elements().Count() == 2 &&
                        //                                                                       el.Elements().ElementAtOrDefault(0).Name.ToString().Contains("IdentCon") &&
                        //                                                                       el.Elements().ElementAtOrDefault(0).Attribute("UId") != null &&
                        //                                                                       el.Elements().ElementAtOrDefault(1).Attribute("Name") != null &&
                        //                                                                       el.Elements().ElementAtOrDefault(1).Attribute("Name").Value == "RESET").Elements();
                        //
                        //List<XElement> list = new List<XElement>();
                        //
                        //foreach (var wire in checkWire.Where(el => el.Attribute("Name") != null && el.Attribute("Name").Value == "RESET"))
                        //{
                        //    var uid_call = wire.Parent.Elements().ElementAt(1).Attribute("UId").Value;
                        //
                        //
                        //    if (uid_call == instance.UId.ToString())
                        //    {
                        //        var access_call = wire.Parent.Elements().ElementAt(0).Attribute("UId").Value;
                        //
                        //        var deleteAccess = networkSource.Descendants(_Namespace + "Access").Where(el => el.Attribute("UId").Value != null && el.Attribute("UId").Value == access_call.ToString()).Elements();
                        //        if (deleteAccess != null) deleteAccess.Remove();
                        //
                        //        network.Access.Remove(Int32.Parse(access_call));
                        //
                        //        list.Add(wire.Parent);
                        //    }
                        //
                        //}
                        //
                        //foreach(var el in list) 
                        //{
                        //    el.Remove();
                        //}
                        //







                        //var deleteWire = networkSource.Descendants(_Namespace + "NameCon").Where(el => el.Attribute("Name").Value != null && el.Attribute("Name").Value == "RESET").FirstOrDefault();
                        //if (deleteWire != null) deleteWire.Parent.Remove();


                    }
                    catch { }

                    //try
                    //{
                    //    network.SetParameter(instance, "IN_INT", "Input", "Int", inputReference.Value, inputReference.IsGlobal);
                    //}
                    //catch { }


                }
            }

            Console.WriteLine("Block was found " + unit.Name + "." + tagName);

            LimValMin = limValueMin;
            LimValMax = limValueMax;
        }
        public override void SetFaceplateProperties()
        {


            //SET PROPERTY FOR INSTANCES
            if (_isNew)
            {
                int i = 1;

                while (i <= _CountInstance)
                {
                    //Set GMP
                    bool isTagGMP = false;

                    if (isTagGMP)
                    {
                        Portal.SetTagGMP(Name + "_Instance" + i + "_LIM_HH", false, false);
                        Portal.SetTagGMP(Name + "_Instance" + i + "_LIM_H", false, false);
                        Portal.SetTagGMP(Name + "_Instance" + i + "_LIM_L", false, false);
                        Portal.SetTagGMP(Name + "_Instance" + i + "_LIM_LL", false, false);
                    }

                    Console.WriteLine("Property analog for set tag " + Name + " instance" + i + " was completed");

                    i++;
                }
            }
        }

        public string ScreenName;
        public SiemensPortal Portal;
        private float LimValMin;
        private float LimValMax;
        private bool _isNew;
        private int _CountInstance;
    }
}
