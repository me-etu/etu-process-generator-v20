using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace TIA_LIB.Xml.Network
{
    public class XmlCall : XmlPart
    {
        public XmlCall(XmlNetwork network, string blockName, string blockType, string instType = null, string instName = null): base(network)
        {
            UId = ++Network.MaxUId;

            BlockType = blockType;
            BlockName = blockName;
            InstType = instType;
            InstName = instName;

            Name = instName;
            
            Xml = new XElement("Call");          
            Xml.SetAttributeValue("UId", UId);

            CallInfo = new XElement("CallInfo");
            CallInfo.SetAttributeValue("BlockType", BlockType);
            CallInfo.SetAttributeValue("Name", BlockName);

            Xml.Add(CallInfo);

            if (BlockType == "FB")
            {
                var instance = new XElement("Instance");
                instance.SetAttributeValue("UId", ++Network.MaxUId);

                instance.SetAttributeValue("Scope", InstType);

                var component = new XElement("Component");
                component.SetAttributeValue("Name", InstName);

                instance.Add(component);

                CallInfo.Add(instance);
            }

            Network.Calls.Add(Name, this);

            Network.Parts.AddFirst(Xml);
         
            if(InstType == "LocalVariable")
            {
                Network.Block.GetLocalInstance(instName, blockName);
            }
            else if (InstType == "GlobalVariable")
            {
                Network.Block.InstName = instName;
                Network.Block.InstGlobal = true;
                Network.Block.BlocktName = BlockName;
            }

            Network.Block.HasChanged = true;

        }
        public XmlCall(XmlNetwork network, XElement xml): base(network)
        {
            Xml = xml;

            UId = Int32.Parse(Xml.Attribute("UId").Value);

            if (UId > Network.MaxUId)
            {
                Network.MaxUId = UId;
            }

            CallInfo = Xml.Descendants(_Namespace + "CallInfo").FirstOrDefault();

            BlockType = CallInfo.Attribute("BlockType").Value;
            BlockName = CallInfo.Attribute("Name").Value;

            if (BlockType == "FB")
            {
                var instance = CallInfo.Descendants(_Namespace + "Instance").FirstOrDefault();
                var uid = Int32.Parse(instance.Attribute("UId").Value);

                if (uid > Network.MaxUId)
                {
                    Network.MaxUId = uid;
                }

                InstType = instance.Attribute("Scope").Value;

                var component = instance.Descendants(_Namespace + "Component").FirstOrDefault();
                InstName = component.Attribute("Name").Value;

                Network.Calls.Add(InstName, this);
            }

           

            //SUCHEN PARAMETERS
            foreach (var par in CallInfo.Descendants(_Namespace + "Parameter"))
            {
                new XmlParameter(this, par);
            }

            //SUCHEN NEGATIONS
            foreach (var neg in Xml.Descendants(_Namespace + "Negated"))
            {
                Negations.Add(neg.Attribute("Name").Value);
            }


            GetParameter("EN", "Input", "Bool");      
        }

        public XmlParameter GetParameter(string name, string section, string type, bool neg = false)
        {
            XmlParameter param;

            if (!Parameters.TryGetValue(name, out param))
            {
                param = new XmlParameter(this, name, section, type, neg);
            }

            if (!Negations.Contains(name) && neg)
            {
                param.SetNegations();
            }

            return param;
        }

        public XElement Xml;
        public XElement CallInfo;
        public string BlockType;
        public string BlockName;
        public string Name;
        public string InstType;
        public string InstName;
        public Dictionary<string, XmlParameter> Parameters = new Dictionary<string, XmlParameter>();
        public List<string> Negations = new List<string>();
        private XNamespace _Namespace = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v4";
    }
  
}
