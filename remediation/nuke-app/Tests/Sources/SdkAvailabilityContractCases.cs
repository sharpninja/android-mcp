using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record SdkAvailabilityCase(string Id, string Xml, JsonObject[] Rows, JsonObject[] Sdks)
{
    public JsonObject Expected() => new() { ["rows"] = new JsonArray(Rows.Select(x => (JsonNode)x.DeepClone()).ToArray()), ["sdks"] = new JsonArray(Sdks.Select(x => (JsonNode)x.DeepClone()).ToArray()) };
    public SourceExtractionCase MutationCase() => new(Id, "sdk-availability-evidence", Xml, StructuralParseStatus.Clean, [Expected()], [], "sdk_availability_evidence.enumerate_evidence literal owner/member facts");
    public SdkAvailabilityEvidence Correct() => new(Rows.Select(x => (JsonObject)x.DeepClone()).ToArray(), Sdks.Select(x => (JsonObject)x.DeepClone()).ToArray());
}
public sealed record SdkAvailabilityFailure(string Id, string Xml, string Message);
public static class SdkAvailabilityContractCases
{
    private static JsonObject Attributes(params (string Key, string Value)[] values) => new(values.Select(x => new KeyValuePair<string, JsonNode?>(x.Key, JsonValue.Create(x.Value))));
    private static JsonObject Row(string? owner, string tag, int ordinal, JsonObject literal, JsonObject ownerLiteral, JsonArray? supertypes = null, int line = 1) => new()
    {
        ["owner_binary_name"] = owner, ["tag"] = tag, ["member_ordinal"] = ordinal, ["line"] = line, ["attributes"] = literal.DeepClone(), ["owner_attributes"] = ownerLiteral.DeepClone(),
        ["supertypes"] = supertypes, ["semantics"] = "Exact source facts; absent constraints unknown/not declared, never assumed unrestricted"
    };
    private static readonly JsonObject FirstOwner = Attributes(("name", "a/C"), ("since", "30"), ("removed", "35"), ("sdks", "30:4"), ("module", "m"));
    private static readonly JsonObject SecondOwner = Attributes(("name", "a/C"), ("since", "38"));
    private static readonly JsonObject ThirdOwner = Attributes(("name", "a/C"), ("since", "37.2"), ("future", "raw"));
    private static readonly JsonObject SuperOwner = Attributes(("name", "a/C"), ("unknown", "raw"));
    public static IReadOnlyList<SdkAvailabilityCase> All { get; } =
    [
        new("AVAIL-01", "<api version=\"4\"><sdk id=\"30\" name=\"R\"/><class name=\"a/C\" since=\"30\" removed=\"35\" sdks=\"30:4\" module=\"m\"><method name=\"m()V\" since=\"31\" removed=\"36\"/></class></api>",
            [Row("a/C", "class", 0, FirstOwner, FirstOwner, new()), Row("a/C", "method", 1, Attributes(("name", "m()V"), ("since", "31"), ("removed", "36")), FirstOwner)], [Attributes(("id", "30"), ("name", "R"))]),
        new("AVAIL-02", "<api version=\"4\"><class name=\"a/C\" since=\"38\"><field name=\"f\"/></class></api>",
            [Row("a/C", "class", 0, SecondOwner, SecondOwner, new()), Row("a/C", "field", 1, Attributes(("name", "f")), SecondOwner)], []),
        new("AVAIL-03", "<api version=\"4\"><class name=\"a/C\" since=\"37.2\" future=\"raw\"/></api>", [Row("a/C", "class", 0, ThirdOwner, ThirdOwner, new())], []),
        new("AVAIL-04", "<api version=\"4\">\n<class name=\"a/C\" unknown=\"raw\">\n<extends name=\"a/P\" since=\"32\"/><implements name=\"a/I\"/>\n<field name=\"f\"/><method name=\"m()V\"/>\n</class><sdk id=\"30\" future=\"kept\"/>\n</api>",
            [Row("a/C", "class", 0, SuperOwner, SuperOwner, new JsonArray(new JsonObject { ["name"] = "a/P", ["since"] = "32", ["relation"] = "extends" }, new JsonObject { ["name"] = "a/I", ["relation"] = "implements" }), 2),
             Row("a/C", "field", 1, Attributes(("name", "f")), SuperOwner, line: 4), Row("a/C", "method", 2, Attributes(("name", "m()V")), SuperOwner, line: 4)], [Attributes(("id", "30"), ("future", "kept"))]),
        new("AVAIL-05", "<api version=\"4\"/>", [], []),
        new("AVAIL-06", "<api version=\"4\"><class><field/></class></api>", [Row(null, "class", 0, new(), new(), new()), Row(null, "field", 1, new(), new())], []),
        new("AVAIL-07", "<api version=\"4\"><sdk id=\"30\"/><sdk id=\"30\"/><class name=\"a/C\"/><class name=\"a/C\"/></api>",
            [Row("a/C", "class", 0, Attributes(("name", "a/C")), Attributes(("name", "a/C")), new()), Row("a/C", "class", 0, Attributes(("name", "a/C")), Attributes(("name", "a/C")), new())], [Attributes(("id", "30")), Attributes(("id", "30"))])
    ];
    public static IReadOnlyList<SdkAvailabilityFailure> Failures { get; } =
    [
        new("AVAIL-ERR01", "<api version=\"3\"/>", "Unsupported lifecycle format"),
        new("AVAIL-ERR02", "<other version=\"4\"/>", "Unsupported lifecycle format"),
        new("AVAIL-ERR03", "<api xmlns=\"urn:other\" version=\"4\"/>", "Unsupported lifecycle format"),
        new("AVAIL-ERR04", "<api", "XML"),
        new("AVAIL-ERR05", "<api version=\"4\"><other><class name=\"a/C\"/></other></api>", "Uninventoried lifecycle declaration"),
        new("AVAIL-ERR06", "<api version=\"4\"><method/><class name=\"a/C\"/></api>", "Lifecycle locator mismatch")
    ];
    public static IEnumerable<object[]> Cases => All.Select(x => new object[] { x.Id });
    public static IEnumerable<object[]> Mutations => All.SelectMany(x => SourceMutationCases.For(x.MutationCase()));
    public static IEnumerable<object[]> FailureCases => Failures.Select(x => new object[] { x.Id });
    public static SdkAvailabilityCase Get(string id) => All.Single(x => x.Id == id);
    public static void AssertContract(SdkAvailabilityCase test, ISdkAvailabilitySourceFacts provider)
    {
        var bytes = Encoding.UTF8.GetBytes(test.Xml); var original = bytes.ToArray(); var result = provider.Enumerate(bytes);
        Assert.NotNull(result); Assert.NotNull(result.Records); Assert.NotNull(result.SdkDefinitions);
        var actual = new JsonObject { ["rows"] = new JsonArray(result.Records.Select(x => (JsonNode)x.DeepClone()).ToArray()), ["sdks"] = new JsonArray(result.SdkDefinitions.Select(x => (JsonNode)x.DeepClone()).ToArray()) };
        Assert.True(JsonNode.DeepEquals(test.Expected(), actual), "Literal availability facts differ: " + test.Id); Assert.Equal(original, bytes);
    }
    public static void AssertFailure(SdkAvailabilityFailure test, ISdkAvailabilitySourceFacts provider)
    {
        var bytes = Encoding.UTF8.GetBytes(test.Xml); var original = bytes.ToArray();
        Assert.Contains(test.Message, Assert.Throws<FormatException>(() => provider.Enumerate(bytes)).Message, StringComparison.OrdinalIgnoreCase); Assert.Equal(original, bytes);
    }
}
