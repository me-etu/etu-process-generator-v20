using System;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.HmiUnified;
using TIA_LIB.Xml;
using System.Collections.Generic;
using TIA_LIB.Xml.Network;

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

        private string _DescriptionBlockType;
        private int _DescriptionMonitorCount;
        private int _DescriptionInterlockCount;

        internal IEnumerable<XmlCall> ConfiguredDeviceCalls()
        {
            var network = Unit.FindNetwork(TagName);
            if (network == null) throw new InvalidOperationException("Missing device network " + Unit.Name + "/" + TagName + ".");
            for (int index = 0; index <= _DescriptionMonitorCount; index++)
            {
                string name = index == 0 ? TagName : TagName + "_Instance" + index;
                XmlCall call;
                if (!network.Calls.TryGetValue(name, out call) || call.BlockName != _DescriptionBlockType || call.InstType != "LocalVariable")
                    throw new InvalidOperationException("Expected owned device call " + Unit.Name + "/" + name + " of " + _DescriptionBlockType + ".");
                yield return call;
            }
        }

        protected void ConfigureInstanceDescription(string blockType, int monitorCount, string description, int interlockCount = 0)
        {
            _DescriptionBlockType = blockType;
            _DescriptionMonitorCount = monitorCount;
            _DescriptionInterlockCount = interlockCount;
            SetDeviceDescription(description);
        }

        public void SetDeviceDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description)) return;
            for (int index = 0; index <= _DescriptionMonitorCount; index++)
            {
                var instanceName = index == 0 ? TagName : TagName + "_Instance" + index;
                SetInstanceDescription(instanceName, _DescriptionBlockType, description);
            }
            for (int index = 1; index <= _DescriptionInterlockCount; index++)
                SetInstanceDescription(TagName + "_Interlock" + index, "fbIntlck7", description);
        }

        private void SetInstanceDescription(string instanceName, string blockType, string description)
        {
            XmlLocalInstance instance;
            if (!Unit.LocalInstance.TryGetValue(instanceName, out instance) ||
                instance.Datatype.Trim('"') != blockType)
                throw new InvalidOperationException("Expected local instance '" + instanceName +
                    "' of " + blockType + " in unit '" + Unit.Name + "'.");
            instance.SetDeclarationComment(description);
        }

        public virtual void SetFaceplateProperties() { }
        public XmlUnit Unit;
        public XmlOperation Op;
        public string Name;
        public string TagName;
        }
}
