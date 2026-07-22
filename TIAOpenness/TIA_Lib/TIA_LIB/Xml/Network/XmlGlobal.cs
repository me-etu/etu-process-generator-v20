using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace TIA_LIB.Xml.Network
{
    public class XmlGlobal: XmlAccess
    {
        public XmlGlobal(XmlNetwork network, string value1, string value2, string datatype, bool isGlobal = false, bool isInstanceDB = false)
            : this(network, new[] { value1, value2 }.Where(value => value != null && value != "").ToArray(), datatype, isGlobal, isInstanceDB)
        {
        }

        public XmlGlobal(XmlNetwork network, string[] components, string datatype, bool isGlobal = false, bool isInstanceDB = false): base(network)
        {
            Components = components.Where(value => value != null && value != "").ToList();
            Datatype = datatype;

            if(isGlobal) Xml.SetAttributeValue("Scope", "GlobalVariable");
            if(!isGlobal) Xml.SetAttributeValue("Scope", "LocalVariable");

            var symbol = new XElement("Symbol");
            Xml.Add(symbol);

            foreach (var componentName in Components)
            {
                var component = new XElement("Component");
                component.SetAttributeValue("Name", componentName);
                symbol.Add(component);
            }

            Value1 = Components.Count > 0 ? Components[0] : null;
            Value2 = Components.Count > 1 ? Components[1] : null;
            Value = string.Join("|", Components);

            if(isGlobal && Components.Count == 2 && (isInstanceDB || !Components[0].StartsWith("TP")))
            {
                SiemensPortal.Current.GetValueFromGlobalDB(Components[0], Components[1], datatype);
            }

            Network.Block.HasChanged = true;
        }
        public XmlGlobal(XmlNetwork network, XElement xml): base(network, xml)
        {
            Components = xml.Descendants(_Namespace + "Component")
                .Select(component => component.Attribute("Name") != null ? component.Attribute("Name").Value : "")
                .Where(value => value != "")
                .ToList();

            Value1 = Components.Count > 0 ? Components[0] : null;
            Value2 = Components.Count > 1 ? Components[1] : null;
            Value = string.Join("|", Components);
        }

        public override void Set(string value)
        {
            var components = value.Split('|').Where(component => component != null && component != "").ToList();

            if (components.SequenceEqual(Components))
            {
                return;
            }

            Components = components;

            var symbol = Xml.Descendants(_Namespace + "Symbol").FirstOrDefault();
            if (symbol == null)
            {
                symbol = new XElement("Symbol");
                Xml.Add(symbol);
            }

            symbol.Elements(_Namespace + "Component").Remove();
            symbol.Elements("Component").Remove();

            foreach (var componentName in Components)
            {
                var component = new XElement("Component");
                component.SetAttributeValue("Name", componentName);
                symbol.Add(component);
            }

            Value1 = Components.Count > 0 ? Components[0] : null;
            Value2 = Components.Count > 1 ? Components[1] : null;
            Value = string.Join("|", Components);

            Network.Block.HasChanged = true;
        }
        public string Value1;
        public string Value2;
        public string Value;
        public string Datatype;
        public List<string> Components = new List<string>();
        private XNamespace _Namespace = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v4";
    }
}