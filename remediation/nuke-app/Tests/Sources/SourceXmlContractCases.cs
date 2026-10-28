using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record SourceXmlCase(string Id, JsonObject Element, string Expected);
public static class SourceXmlContractCases
{
    private const string Android = "{http://schemas.android.com/apk/res/android}";
    public static JsonObject Element(string tag, JsonObject? attributes = null, string? text = null, string? tail = null, params JsonObject[] children) => new()
    {
        ["tag"] = tag, ["attributes"] = attributes ?? new(), ["text"] = text, ["tail"] = tail, ["children"] = new JsonArray(children.Cast<JsonNode>().ToArray())
    };
    // Expected strings are pinned from the existing installed standard-library
    // source read as text: ElementTree.py _namespaces/_serialize_xml/_escape_*.
    // No interpreter or legacy baseline has been executed.
    public static IReadOnlyList<SourceXmlCase> All { get; } =
    [
        new("XML-01", Element("permission"), "<permission />"),
        new("XML-02", Element("x", new() { ["z"] = "last", ["a"] = "first" }), "<x z=\"last\" a=\"first\" />"),
        new("XML-03", Element("x", new() { ["a"] = "&<>\"\t\n\r'" }), "<x a=\"&amp;&lt;&gt;&quot;&#09;&#10;&#13;'\" />"),
        new("XML-04", Element("x", text: "&<>\"'", tail: " tail&"), "<x>&amp;&lt;&gt;\"'</x> tail&amp;"),
        new("XML-05", Element("parent", children: [Element("child", text: "value", tail: "\n")]), "<parent><child>value</child>\n</parent>"),
        new("XML-06", Element("meta-data", new() { [Android + "name"] = "k", [Android + "value"] = "v" }), "<meta-data xmlns:ns0=\"http://schemas.android.com/apk/res/android\" ns0:name=\"k\" ns0:value=\"v\" />"),
        new("XML-07", Element("{urn:a}root", new() { ["{urn:b}key"] = "v" }), "<ns0:root xmlns:ns0=\"urn:a\" xmlns:ns1=\"urn:b\" ns1:key=\"v\" />"),
        new("XML-08", Element("x", new() { ["{http://www.w3.org/XML/1998/namespace}lang"] = "en" }), "<x xml:lang=\"en\" />"),
        new("XML-09", Element("{http://www.w3.org/1999/xhtml}div"), "<html:div xmlns:html=\"http://www.w3.org/1999/xhtml\" />"),
        new("XML-10", Element("root", children: [Element("{urn:child}leaf")]), "<root xmlns:ns0=\"urn:child\"><ns0:leaf /></root>"),
        new("XML-11", Element("x", new() { ["name"] = "café😀" }, text: "café😀"), "<x name=\"café😀\">café😀</x>"),
        new("XML-12", Element("x", text: "", tail: ""), "<x />"),
        new("XML-13", Element("x", new() { ["{urn:b}attr"] = "a" }, children: [Element("{urn:a}child")]), "<x xmlns:ns0=\"urn:b\" xmlns:ns1=\"urn:a\" ns0:attr=\"a\"><ns1:child /></x>"),
        new("XML-14", Element("{http://www.w3.org/2001/XMLSchema-instance}x", new() { ["{urn:other}key"] = "v" }), "<xsi:x xmlns:ns1=\"urn:other\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" ns1:key=\"v\" />"),
        new("XML-15", Element("{http://www.w3.org/1999/02/22-rdf-syntax-ns#}x"), "<rdf:x xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\" />"),
        new("XML-16", Element("{http://schemas.xmlsoap.org/wsdl/}x"), "<wsdl:x xmlns:wsdl=\"http://schemas.xmlsoap.org/wsdl/\" />"),
        new("XML-17", Element("{http://www.w3.org/2001/XMLSchema}x"), "<xs:x xmlns:xs=\"http://www.w3.org/2001/XMLSchema\" />"),
        new("XML-18", Element("{http://purl.org/dc/elements/1.1/}x"), "<dc:x xmlns:dc=\"http://purl.org/dc/elements/1.1/\" />"),
        new("XML-19", Element("x", text: "\r\n\t", tail: "\r\n\t"), "<x>\r\n\t</x>\r\n\t")
    ];
    public static IEnumerable<object[]> Cases => All.Select(x => new object[] { x.Id });
    public static SourceXmlCase Get(string id) => All.Single(x => x.Id == id);
    public static void AssertContract(SourceXmlCase test, ISourceXmlFormatter provider)
    {
        var input = (JsonObject)test.Element.DeepClone(); Assert.Equal(test.Expected, provider.Serialize(input));
        Assert.True(JsonNode.DeepEquals(test.Element, input), "XML element evidence mutated");
    }
}
