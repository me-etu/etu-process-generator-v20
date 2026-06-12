using System;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.HmiUnified;
using TIA_LIB.Xml;

namespace TIA_LIB.Devices
{
    public class GeneratedObject
        {
        public GeneratedObject(XmlUnit unit, string tagName)
        {
            Unit = unit;

            if (!Unit.Devices.ContainsKey(tagName))
            {
                TagName = tagName;
                Name = unit.Name + "_" + tagName;                
                Unit.Devices.Add(tagName, this);
            }
        }

        public GeneratedObject (string taName, string tagName) 
        {
            TagName = tagName;
            Name = taName + "_" + tagName;

            var plant = XmlPlant.Current;

            Unit = plant.GetUnit(taName);

            Unit.Devices.Add(tagName, this);
        }

        public virtual void SetFaceplateProperties() { }
        public XmlUnit Unit;
        public XmlOperation Op;
        public string Name;
        public string TagName;
        }
}
