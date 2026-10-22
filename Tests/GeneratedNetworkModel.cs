using System;
using System.Linq;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using TIA_LIB.Generation;

public static class GeneratedNetworkModelTests
{
    static void Check(bool value, string detail) { if (!value) throw new Exception(detail); }
    static XElement Pin(string name, string direction, string type = "Bool")
    { return new XElement("Parameter", new XAttribute("Name", name), new XAttribute("Section", direction), new XAttribute("Type", type)); }
    static XElement Call(int id, string instance, params XElement[] pins)
    {
        return new XElement("Call", new XAttribute("UId", id), new XElement("CallInfo", new XAttribute("Name", "fbDevice"), new XAttribute("BlockType", "FB"),
            new XElement("Instance", new XAttribute("UId", id + 1), new XAttribute("Scope", "LocalVariable"), new XElement("Component", new XAttribute("Name", instance))), pins));
    }
    static XElement Symbol(int id, string scope, params string[] components)
    { return new XElement("Access", new XAttribute("UId", id), new XAttribute("Scope", scope), new XElement("Symbol", components.Select(c => new XElement("Component", new XAttribute("Name", c))))); }
    static XElement Endpoint(int id, string pin)
    { return new XElement("NameCon", new XAttribute("UId", id), new XAttribute("Name", pin)); }
    static XElement Ident(int id) { return new XElement("IdentCon", new XAttribute("UId", id)); }
    static XElement Wire(int id, params XElement[] ends) { return new XElement("Wire", new XAttribute("UId", id), ends); }
    static void Reject(XElement xml, string message)
    {
        try { GeneratedNetworkModel.FromGeneratedFbd("fixture", "Fixture", xml, new[] { "fbDevice" }); throw new Exception("Accepted " + message); }
        catch (InvalidOperationException ex) { Check(ex.Message.Contains("fixture"), "Diagnostic lacks stable identity."); }
    }
    public static string ReadStructuredText(XElement element)
    {
        // Independent token reader, also usable on the repository's original template.
        switch (element.Name.LocalName)
        {
            case "Token": return (string)element.Attribute("Text");
            case "Blank": return new string(' ', (int)element.Attribute("Num"));
            case "NewLine": return new string('\n', (int)element.Attribute("Num"));
            case "Text": return element.Value;
            case "Component": return "\"" + (string)element.Attribute("Name") + "\"";
            case "ConstantValue": return element.Value;
            case "Parameter": return "\"" + (string)element.Attribute("Name") + "\"" + string.Concat(element.Elements().Select(ReadStructuredText));
            case "Access":
                string scope = (string)element.Attribute("Scope");
                return (scope == "LocalVariable" ? "#" : "") + string.Concat(element.Elements().Select(ReadStructuredText));
            case "CallInfo":
                var instance = element.Elements().Single(e => e.Name.LocalName == "Instance");
                string reference = ((string)instance.Attribute("Scope") == "LocalVariable" ? "#" : "") +
                    string.Join(".", instance.Elements().Select(ReadStructuredText));
                return reference + string.Concat(element.Elements().Where(e => e != instance).Select(ReadStructuredText));
            case "ConstantType": return "";
            default: return string.Concat(element.Elements().Select(ReadStructuredText));
        }
    }
    public static void Run()
    {
        // Independent golden FlgNet, deliberately puts consumer before producer in Parts.
        var xml = new XElement("SW.Blocks.CompileUnit", new XElement("AttributeList", new XElement("NetworkSource",
            new XElement("FlgNet", new XElement("Parts",
                Call(10, "Consumer-二", Pin("IN", "Input"), Pin("CFG", "InOut", "typeCfg"), Pin("Q", "Output")),
                Call(20, "Producer", Pin("IN", "Input"), Pin("Q", "Output")),
                new XElement("Part", new XAttribute("UId", 30), new XAttribute("Name", "O"), new XElement("TemplateValue", new XAttribute("Name", "Card"), new XAttribute("Type", "Cardinality"), "2")),
                Symbol(40, "LocalVariable", "iReset"), Symbol(41, "GlobalVariable", "Inputs", "DI-1"),
                Symbol(42, "LocalVariable", "CFG", "Consumer-二"), Symbol(43, "GlobalVariable", "Outputs", "Q1")),
                new XElement("Wires", Wire(100, Endpoint(10, "IN"), Endpoint(30, "out")),
                    Wire(101, Endpoint(30, "in1"), Endpoint(20, "Q")), Wire(102, Endpoint(30, "in2"), Ident(40)),
                    Wire(103, Endpoint(20, "IN"), Ident(41)), Wire(104, Endpoint(10, "CFG"), Ident(42)),
                    Wire(105, Endpoint(10, "Q"), Ident(43)), Wire(106, Endpoint(10, "en"), new XElement("OpenCon", new XAttribute("UId", 107))))))));
        string original = xml.ToString();
        var model = GeneratedNetworkModel.FromGeneratedFbd("Unit/Device", "Level radar – B13", xml, new[] { "fbDevice" });
        string source = model.RenderScl();
        Check(original == xml.ToString() && XNode.DeepEquals(xml, model.RenderFbd()), "Adapter/FBD renderer changes original data.");
        Check(model.Invocations.Count == 2 && model.Invocations[0].Instance.Components[0] == "Producer", "Producer dependency not scheduled first.");
        Check(source.IndexOf("#\"Producer\"(", StringComparison.Ordinal) < source.IndexOf("#\"Consumer-二\"(", StringComparison.Ordinal), "Source reverses call dependencies.");
        Check(source.Contains("REGION Level radar – B13") && source.Contains("\"IN\" := (#\"Producer\".\"Q\" OR #\"iReset\")") &&
            source.Contains("\"CFG\" := #\"CFG\".\"Consumer-二\"") && source.Contains("\"Outputs\".\"Q1\" := #\"Consumer-二\".\"Q\";"), "Golden scopes/logic/directions differ.");
        Check(source.Split(new[] { "#\"Producer\"(" }, StringSplitOptions.None).Length == 2 && source.Split(new[] { "#\"Consumer-二\"(" }, StringSplitOptions.None).Length == 2, "Repeated stateful invocation.");
        var rendered = model.RenderFbd(); rendered.RemoveNodes();
        Check(original == model.RenderFbd().ToString(), "FBD snapshot is mutable.");
        var body = new GeneratedBlockBody(new[] { new GeneratedLogicalNetwork(model) });
        var tokenRenderer = new StructuredTextBodyRenderer();
        var tokens = tokenRenderer.Render(body);
        Check(Regex.Replace(ReadStructuredText(tokens), @"\s+", "") == Regex.Replace(body.RenderScl(), @"\s+", ""), "Tokenized SCL differs from readable SCL backend.");
        Check(XNode.DeepEquals(tokens, tokenRenderer.Render(body)), "Reused token renderer is nondeterministic.");
        Check(tokens.Descendants().Where(e => e.Attribute("UId") != null).Select(e => (string)e.Attribute("UId")).Distinct().Count() == tokens.Descendants().Count(e => e.Attribute("UId") != null), "Duplicate structured token UIds.");
        var negated = new XElement(xml);
        negated.Descendants("Call").First().Add(new XElement("Negated", new XAttribute("Name", "IN")), new XElement("Negated", new XAttribute("Name", "Q")));
        source = GeneratedNetworkModel.FromGeneratedFbd("negation", "Negation", negated, new[] { "fbDevice" }).RenderScl();
        Check(source.Contains("\"IN\" := NOT ((#\"Producer\".\"Q\" OR #\"iReset\"))") && source.Contains("Q1\" := NOT (#\"Consumer-二\".\"Q\");"), "Negation lost.");
        var bad = new XElement(xml);
        bad.Descendants("Part").Single().SetAttributeValue("Name", "Unknown"); Reject(bad, "unknown instruction");
        bad = new XElement(xml); bad.Descendants("Wires").Single().Add(Wire(200, Endpoint(10, "IN"), Ident(40))); Reject(bad, "duplicate driver");
        bad = new XElement(xml); bad.Descendants("Wires").Single().Add(Wire(201, Endpoint(10, "Q"), Endpoint(20, "IN"))); Reject(bad, "cycle/duplicate input");
        bad = new XElement(xml); bad.Descendants("CallInfo").First().SetAttributeValue("Name", "ManualFB"); Reject(bad, "unregistered block");
        bad = new XElement(xml); bad.Descendants("Part").Single().Add(new XElement("Negated", new XAttribute("Name", "in2")));
        Check(GeneratedNetworkModel.FromGeneratedFbd("negatedLogic", "Logic", bad, new[] { "fbDevice" }).RenderScl().Contains("OR NOT (#\"iReset\")"), "Logic pin inversion lost.");
        Console.WriteLine("PASS: supported FB/A/O model, FBD snapshot, SCL dependencies, scopes, Boolean logic, Unicode titles, negation, InOut, exactly-once calls and rejection fixtures.");
    }
}
