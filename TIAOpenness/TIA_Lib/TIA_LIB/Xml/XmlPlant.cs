using System;
using System.Collections.Generic;
using Siemens.Engineering.SW.Blocks;

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

                if(network == null)
                {
                    network = GetNetwork(name);
                    var call = network.GetCall("fb" + name, "FB", "GlobalVariable", name);
                    network.SetParameter(call, "en", "Input", "Bool");
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