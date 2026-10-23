using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace TIA_LIB.Generation
{
    public static class ConfigurationMigrationApproval
    {
        static readonly string[] Acknowledgements = { "ExternalConsumersReviewed", "OfflineBackupVerified", "OnlineActualValuesAddressed", "LegacyMembersRetained", "DivergenceRiskAccepted" };
        static string Mapping(XElement path) { return (string)path.Attribute("Old") + "->" + (string)path.Attribute("New") + ":" + (string)path.Attribute("Type"); }
        public static XElement CreateTemplate(XElement plan)
        {
            return new XElement("ConfigurationMigrationApproval", new XAttribute("Version", "1"),
                new XAttribute("Context", (string)plan.Attribute("Context")), new XAttribute("PlanHash", ManagedArtifactOwnership.Fingerprint(plan)),
                new XAttribute("UnknownConsumers", "true"), Acknowledgements.Select(a => new XElement(a, "false")),
                new XElement("ExternalConsumerEvidence", ""), new XElement("OnlineValueEvidence", ""),
                new XElement("Symbols", plan.Descendants("Path").Select(p => new XElement(p))));
        }
        public static void Validate(XElement plan, XElement approval)
        {
            if (approval == null || approval.Name != "ConfigurationMigrationApproval" || (string)approval.Attribute("Version") != "1" ||
                (string)approval.Attribute("Context") != (string)plan.Attribute("Context") ||
                (string)approval.Attribute("PlanHash") != ManagedArtifactOwnership.Fingerprint(plan))
                throw new InvalidOperationException("Configuration migration approval does not match this exact project/CPU/plan snapshot.");
            if ((string)approval.Attribute("UnknownConsumers") != "false" || Acknowledgements.Any(a => approval.Elements(a).Count() != 1 || approval.Element(a).Value != "true") ||
                string.IsNullOrWhiteSpace((string)approval.Element("ExternalConsumerEvidence")) || string.IsNullOrWhiteSpace((string)approval.Element("OnlineValueEvidence")))
                throw new InvalidOperationException("Configuration migration still has unknown consumers, unverified backups or unaddressed online values/divergence. Complete the review evidence before applying it.");
            var expected = plan.Descendants("Path").Select(Mapping).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            var actual = approval.Elements("Symbols").SelectMany(s => s.Elements("Path")).Select(Mapping).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            if (!expected.SequenceEqual(actual)) throw new InvalidOperationException("Configuration migration approval does not cover the exact symbol/type mappings.");
        }
    }
}
