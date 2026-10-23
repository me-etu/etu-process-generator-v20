using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace TIA_LIB.Generation
{
    public sealed class GeneratedLogicalNetwork
    {
        public GeneratedNetworkModel Body { get; }
        public DeviceCategory? Category { get; }
        public GeneratedLogicalNetwork(GeneratedNetworkModel body, DeviceCategory? category = null)
        {
            Body = body ?? throw new ArgumentNullException(nameof(body));
            if (category.HasValue && !Enum.IsDefined(typeof(DeviceCategory), category.Value)) throw new ArgumentOutOfRangeException(nameof(category));
            Category = category;
        }
    }

    // Device regrouping may cross another generated network only after proving that
    // it does not reverse a read/write dependency. Manual networks are not adapted.
    public sealed class GeneratedBlockBody
    {
        public ReadOnlyCollection<GeneratedLogicalNetwork> Networks { get; }
        public GeneratedBlockBody(IEnumerable<GeneratedLogicalNetwork> networks)
        {
            var original = networks.ToList();
            if (original.Select(n => n.Body.Identity).Distinct(StringComparer.Ordinal).Count() != original.Count)
                throw new InvalidOperationException("Repeated logical network identity in generated block.");
            var devices = original.Where(n => n.Category.HasValue).ToArray();
            if (devices.Length != 0)
            {
                int start = original.IndexOf(devices[0]);
                var schedule = DeviceSchedule.Group(devices.Select(n => new ScheduledNetwork(n.Body.Identity, n.Body.Title, n.Category.Value, n.Body.Reads, n.Body.Writes)));
                var byId = devices.ToDictionary(n => n.Body.Identity);
                var grouped = original.Where(n => !n.Category.HasValue).ToList();
                grouped.InsertRange(start, schedule.Select(n => byId[n.Identity]));
                DeviceSchedule.ValidateOrder(original.Select(n => new ScheduledNetwork(n.Body.Identity, n.Body.Title,
                    n.Category ?? DeviceCategory.Digital_Sensors, n.Body.Reads, n.Body.Writes)), grouped.Select(n => n.Body.Identity));
                original = grouped;
            }
            Networks = original.AsReadOnly();
        }
        public string RenderScl()
        {
            var text = new StringBuilder();
            bool devices = false;
            DeviceCategory? category = null;
            foreach (var network in Networks)
            {
                if (network.Category.HasValue)
                {
                    if (!devices) { text.AppendLine("REGION Devices"); devices = true; }
                    if (category != network.Category)
                    {
                        if (category.HasValue) text.AppendLine("END_REGION");
                        category = network.Category;
                        text.AppendLine("REGION " + category.Value);
                    }
                }
                else if (devices)
                {
                    text.AppendLine("END_REGION"); text.AppendLine("END_REGION"); devices = false; category = null;
                }
                text.Append(network.Body.RenderScl());
            }
            if (devices) { text.AppendLine("END_REGION"); text.AppendLine("END_REGION"); }
            return text.ToString();
        }
        public ReadOnlyCollection<XElement> RenderFbd()
        {
            var networks = new List<XElement>();
            foreach (var network in Networks)
            {
                var xml = network.Body.RenderFbd();
                if (network.Category.HasValue)
                {
                    var comment = xml.Descendants("MultilingualText").SingleOrDefault(e => (string)e.Attribute("CompositionName") == "Comment");
                    foreach (var text in comment == null ? Enumerable.Empty<XElement>() : comment.Descendants("Text"))
                    {
                        string existing = text.Value;
                        int newline = existing.IndexOf('\n');
                        string firstLine = (newline < 0 ? existing : existing.Substring(0, newline)).TrimEnd('\r');
                        if (Enum.GetNames(typeof(DeviceCategory)).Any(c => firstLine == "[ETU group: Devices/" + c + "]"))
                            existing = newline < 0 ? "" : existing.Substring(newline + 1);
                        // Titles already identify device networks. Remove only our
                        // former structural header, preserving every other comment.
                        text.Value = existing;
                    }
                }
                networks.Add(xml);
            }
            return networks.AsReadOnly();
        }
        public static DeviceCategory CategoryFor(string libraryBlock)
        {
            switch (libraryBlock)
            {
                case "fbMonDi": return DeviceCategory.Digital_Sensors;
                case "fbMonAn": return DeviceCategory.Analog_Sensors;
                case "fbVlv2Out": return DeviceCategory.Digital_Valves;
                case "fbVlvCtrl": return DeviceCategory.Analog_Valves;
                case "fbMotS": return DeviceCategory.Motors_Direct;
                case "fbMotAn": return DeviceCategory.Motors_Control;
                case "fbPIDCtrl": return DeviceCategory.PID_Controllers;
                default: throw new InvalidOperationException("No declared device category for " + libraryBlock + ".");
            }
        }
    }
}
