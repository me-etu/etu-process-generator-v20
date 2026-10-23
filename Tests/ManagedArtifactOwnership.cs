using System;
using System.Linq;
using System.Xml.Linq;
using TIA_LIB.Generation;

public static class ManagedArtifactOwnershipTests
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Reject(Action action, string context)
    {
        try { action(); throw new Exception("Accepted " + context); }
        catch (InvalidOperationException) { }
    }
    static XElement Block(string language, string body)
    {
        return XElement.Parse("<Document><Engineering version='V21'/><DocumentInfo><Created>initial</Created></DocumentInfo>" +
            "<SW.Blocks.FB ID='0'><AttributeList><Name>fbA2</Name><ProgrammingLanguage>" + language + "</ProgrammingLanguage>" +
            "<Interface><Sections xmlns='http://www.siemens.com/automation/Openness/SW/Interface/v5'><Section Name='Static'>" +
            "<Member Name='PID-1' Datatype='fbPIDCtrl'><Comment><MultiLanguageText Lang='de-DE'>Preserve device comment</MultiLanguageText></Comment>" +
            "</Member></Section></Sections></Interface></AttributeList><ObjectList>" +
            "<SW.Blocks.CompileUnit ID='1'><AttributeList><NetworkSource>" + body + "</NetworkSource><ProgrammingLanguage>" + language + "</ProgrammingLanguage></AttributeList></SW.Blocks.CompileUnit>" +
            "</ObjectList></SW.Blocks.FB></Document>");
    }
    public static void Run()
    {
        var source = Block("FBD", "<FlgNet><Parts><Access UId='21' Scope='LocalVariable'><Symbol><Component Name='iReset'/></Symbol></Access></Parts><Wires><Wire UId='22'><IdentCon UId='21'/><NameCon UId='23' Name='IN'/></Wire></Wires></FlgNet>");
        var published = Block("SCL", "<StructuredText><Token UId='100' Text='REGION'/><Text UId='101'>Original title</Text><Token UId='102' Text='END_REGION'/></StructuredText>");
        XNamespace ns = "http://www.siemens.com/automation/Openness/SW/Interface/v5";
        published.Descendants(ns + "Member").Single().Add(new XElement(ns + "Sections", new XElement(ns + "Section", new XAttribute("Name", "Input"),
            new XElement(ns + "Member", new XAttribute("Name", "VALUE"), new XAttribute("Datatype", "Real"), new XElement(ns + "StartValue", "42.5")))));
        string sourceSnapshot = source.ToString(), publishedSnapshot = published.ToString();
        var captured = ManagedArtifactOwnership.Capture("Unit/A2", "SCL", published, source);
        var ledger = captured.ToXml();
        var reread = ManagedArtifactOwnership.Read(XElement.Parse(ledger.ToString()));
        var renumbered = new XElement(published);
        foreach (var attr in renumbered.DescendantsAndSelf().Attributes("ID")) attr.Value = "F" + attr.Value;
        foreach (var attr in renumbered.DescendantsAndSelf().Attributes("UId")) attr.Value = ((int)attr + 500).ToString();
        renumbered.Element("DocumentInfo").Element("Created").Value = "later export";
        var restored = reread.Restore("Unit/A2", "SCL", renumbered);
        Check(restored.Descendants("FlgNet").Count() == 1 && !restored.Descendants("StructuredText").Any(), "Editable body was not restored.");
        Check(restored.Descendants(ns + "StartValue").Single().Value == "42.5" && restored.Descendants(ns + "MultiLanguageText").Single().Value == "Preserve device comment", "Compiler-expanded values/metadata were lost.");
        Check(restored.Element("SW.Blocks.FB").Element("AttributeList").Element("ProgrammingLanguage").Value == "FBD", "Editable language differs.");
        restored.RemoveAll();
        Check(reread.Restore("Unit/A2", "SCL", published).Descendants("FlgNet").Any(), "Restoration mutates stored shadow model.");
        var edited = new XElement(published);
        edited.Descendants("Token").First().SetAttributeValue("Text", "MANUAL_CHANGE");
        Reject(() => reread.Restore("Unit/A2", "SCL", edited), "manual body edit");
        edited = new XElement(published); edited.Descendants(ns + "StartValue").Single().Value = "7";
        Reject(() => reread.Restore("Unit/A2", "SCL", edited), "changed offline configuration");
        edited = new XElement(published); edited.Descendants(ns + "MultiLanguageText").Single().Value = "Manual description";
        Reject(() => reread.Restore("Unit/A2", "SCL", edited), "manual instance description");
        Reject(() => reread.Restore("Unit/Other", "SCL", published), "different owner");
        Reject(() => reread.Restore("Unit/A2", "FBD", published), "implicit language conversion");
        edited = new XElement(ledger); edited.SetAttributeValue("Version", "2"); Reject(() => ManagedArtifactOwnership.Read(edited), "unknown ownership version");
        edited = new XElement(ledger); edited.Descendants("Component").First().SetAttributeValue("Name", "corrupt"); Reject(() => ManagedArtifactOwnership.Read(edited), "corrupt editable shadow");
        Check(sourceSnapshot == source.ToString() && publishedSnapshot == published.ToString(), "Ownership operations changed input evidence.");
        Check(ManagedArtifactOwnership.Fingerprint(source) == ManagedArtifactOwnership.Fingerprint(XElement.Parse(source.ToString())), "Fingerprint changes across XML round trip.");
        var brokenWire = new XElement(source); brokenWire.Descendants("IdentCon").Single().SetAttributeValue("UId", "24");
        Check(ManagedArtifactOwnership.Fingerprint(source) != ManagedArtifactOwnership.Fingerprint(brokenWire), "Fingerprint ignores changed wire reference identity.");
        Console.WriteLine("PASS: managed whole-block ownership format, immutable XML ledger, serialization identity normalization, metadata-preserving restore and manual/corrupt/language mismatch protection.");
    }
}
