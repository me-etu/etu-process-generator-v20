using Siemens.Engineering.HmiUnified.UI.Screens;
using System;
using TIA_LIB.Xml;
using TIA_LIB.Xml.Network;
using TIA_LIB.SignalStaging;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;

namespace TIA_LIB.Devices
{
    //public enum ValveType
    //{
    //    Left = 1, 
    //    Right
    //}
    public class Digital : GeneratedObject
    {
        public Digital(XmlUnit unit, string tagName, int iconType = 0, int colorState = 0, int countInstance = 0, bool qualityBit = true, bool neg = true) : base(unit, tagName)
        {
            Portal = SiemensPortal.Current;
            _CountInstance = countInstance;
            XmlPin andOut = null;
            XmlCall call = null;
            XmlCall instance = null;
            XmlLogic orLockAlarm = null;
            XmlLogic orLockWarn = null;

            XmlCall hasInstances = null;

            XNamespace _Namespace = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v4";
            XNamespace _emtpyNamespace = "";

            bool isNewNetwork = false;

            int indexInstance = 0;
            var calledBlockName = "fbMonDi";
            var inputReference = SignalStagingInventory.InputReference("IN_" + tagName);
            var qualityReference = SignalStagingInventory.InputReference("IN_" + tagName + "_QB");

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
                        deleteWire = networkSource.Descendants(blockNamespace + "NameCon").Where(el => el.Attribute("Name").Value != null && el.Attribute("Name").Value == "ALARM").FirstOrDefault();
                        if (deleteWire != null) deleteWire.Parent.Remove();

                        deleteWire = networkSource.Descendants(blockNamespace + "NameCon").Where(el => el.Attribute("Name").Value != null && el.Attribute("Name").Value == "WARN").FirstOrDefault();
                        if (deleteWire != null) deleteWire.Parent.Remove();
                    }

                }

                network = unit.GetNetwork(tagName);
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

                        network.SetParameter(instance, "CFG", "Input", "typeDigitalCfg", "cfgData|" + unit.Name + "_" + tagName + "_Instance" + (indexInstance), true);;
                        network.SetParameter(instance, "RESET", "Input", "Bool", "RESET_CYCL", true);
                        network.SetParameter(instance, "IN", "Input", "Bool", inputReference.Value, inputReference.IsGlobal, false, neg);
                        network.SetParameter(instance, "ERR_EXT", "Input", "Bool", qualityReference.Value, qualityReference.IsGlobal, false, true);

                        if (countInstance > 1)
                        {
                            if (orLockAlarm == null) orLockAlarm = new XmlLogic(network, XmlLogicType.Or);
                            network.SetPin(orLockAlarm, "in" + (countInstance - indexInstance + 1), "Bool", tagName + "_Instance" + (actIndexInstance) + "|" + "QALARM");

                            if (orLockWarn == null) orLockWarn = new XmlLogic(network, XmlLogicType.Or);
                            network.SetPin(orLockWarn, "in" + (countInstance - indexInstance + 1), "Bool", tagName + "_Instance" + (actIndexInstance) + "|" + "QWARN");
                        }
                    }

                    if (countInstance > 1)
                    {
                        network.SetParameter(call, "ALARM", "Input", orLockAlarm.GetPin("out"));
                        network.SetParameter(call, "WARN", "Input", orLockWarn.GetPin("out"));

                    }
                    else if (countInstance == 1)
                    {
                        network.SetParameter(call, "ALARM", "Input", "Bool", tagName + "_Instance1" + "|" + "QALARM");
                        network.SetParameter(call, "WARN", "Input", "Bool", tagName + "_Instance1" + "|" + "QWARN");
                    }

                    instance = network.FindCall(calledBlockName, "FB", "LocalVariable", tagName + "_Instance" + (countInstance));
                    //network.SetParameter(instance, "LAST_CALL", "Input", "Bool", "True");


                    call = network.FindCall(calledBlockName, "FB", "LocalVariable", tagName);
                    network.SetParameter(call, "en", "Input", andOut);
                    //network.SetParameter(call, "HAS_CALL", "Input", "Bool", "True");

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


            if (isNewNetwork)
            {
                if (qualityBit) network.SetParameter(call, "ERR_EXT", "Input", "Bool", qualityReference.Value, qualityReference.IsGlobal, false, true);
                network.SetParameter(call, "IN", "Input", "Bool", inputReference.Value, inputReference.IsGlobal, false, neg);
                network.SetParameter(call, "CFG", "Input", "typeDigitalCfg", "cfgData|" + unit.Name + "_" + tagName, true);
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

                    if (!isNew) network.SetPin(orStatus, "in" + ++value, "Bool", tagName + "|" + "QWARN");
                    if (isNew) network.SetParameter(call, "WARN", "Input", orStatus.GetPin("out"));
                }
            }
            else if (hasInstances != null)
            {
                for (indexInstance = countInstance; indexInstance > 0; indexInstance--)
                {
                    instance = network.FindCall(calledBlockName, "FB", "LocalVariable", tagName + "_Instance" + (indexInstance));
                    //network.SetParameter(instance, "IN", "Input", "Bool", "IN_" + tagName, true, false, true);
                    //network.SetParameter(instance, "ERR_EXT", "Input", "Bool", qualityReference.Value, qualityReference.IsGlobal, false, true);
                    //network.SetParameter(instance, "RESET", "Input", "Bool", "RESET_CYCL", true);
                }
            }


            Console.WriteLine("Block was found " + unit.Name + "." + tagName);
        }
        
        public override void SetFaceplateProperties()
        {

        }

        public string ScreenName;
        public SiemensPortal Portal;
        private int _CountInstance;
        private bool _isNew;
    }
}
