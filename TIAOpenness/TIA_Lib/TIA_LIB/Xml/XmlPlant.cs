using System;
using System.Collections.Generic;
using Siemens.Engineering.SW.Blocks;
using TIA_LIB.SignalStaging;
using TIA_LIB.Xml.Network;

namespace TIA_LIB.Xml
{
    public class XmlPlant: XmlBlock
    {
        public XmlPlant(): base(null, "Plant", "Plant.xml", "Xml/EmptyPlant.xml", "", false)
        {
            Current = this;

            Units = new Dictionary<string, XmlUnit>();

        }

        public XmlUnit GetUnit(string name)
        {
            XmlUnit unit;

            if(!Units.TryGetValue("fb" + name, out unit))
            {
                unit = new XmlUnit("fb" + name);
                Units.Add("fb" + name, unit);

                var network = FindNetwork(name) ?? FindUnitCallNetwork(name);

                XmlCall call = null;

                if(network == null)
                {
                    network = GetNetwork(name);
                    call = network.GetCall("fb" + name, "FB", "GlobalVariable", name);
                    network.SetParameter(call, "en", "Input", "Bool");
                }
                else
                {
                    call = network.FindCall("fb" + name, "FB", "GlobalVariable", name);
                }

                if (PlcProject.StagingMode == SignalStagingMode.GeneratedDbUdt && call != null)
                {
                    string safeUnitName = SignalStagingInventory.SafeName(name);
                    network.SetParameter(call, "hwIN", "Input", "hwIN_" + safeUnitName, "dbIO|" + safeUnitName + "|IN", true);
                    network.SetParameter(call, "hwOUT", "Output", "hwOUT_" + safeUnitName, "dbIO|" + safeUnitName + "|OUT", true);
                }      
            }
            return unit;
        }

        private XmlNetwork FindUnitCallNetwork(string unitName)
        {
            string blockName = "fb" + unitName;

            foreach (var network in Networks.Values)
            {
                foreach (var call in network.Calls.Values)
                {
                    if (call.BlockName == blockName && call.InstName == unitName)
                    {
                        return network;
                    }
                }
            }

            return null;
        }


        public static XmlPlant Current;
        public Dictionary<string, XmlUnit> Units;
        public bool SetErrorToUnitBlock = false;
    }

}