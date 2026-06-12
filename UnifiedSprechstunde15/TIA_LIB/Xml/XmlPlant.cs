using System;
using System.Collections.Generic;
using Siemens.Engineering.SW.Blocks;

namespace TIA_LIB.Xml
{
    public class XmlPlant: XmlBlock
    {
        public XmlPlant(): base(null, "Plant", "Plant.xml", "Xml/EmptyPlant.xml", "", false, "", true)
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

                var network = FindNetwork(name);

                if(network == null)
                {
                    network = GetNetwork(name);
                    var call = network.GetCall("fb" + name, "FB", "GlobalVariable", name);
                    network.SetParameter(call, "en", "Input", "Bool");
                }      
            }
            return unit;
        }


        public static XmlPlant Current;
        public Dictionary<string, XmlUnit> Units;
        public bool SetErrorToUnitBlock = false;
    }

}
