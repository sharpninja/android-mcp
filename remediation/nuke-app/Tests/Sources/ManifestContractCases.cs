using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record ManifestContractCase(SourceExtractionCase Source, JsonObject Metadata)
{
    public ExtractionResult CorrectResult() => Source.CorrectResult() with { Metadata = (JsonObject)Metadata.DeepClone() };
    public SourceExtractionCase MetadataCase() => Source with { ExpectedRecords = [Metadata] };
}

public static class ManifestContractCases
{
    private const string Android = "{http://schemas.android.com/apk/res/android}";
    public static IReadOnlyList<ManifestContractCase> All { get; } = Create();
    public static IEnumerable<object[]> Cases => All.Select(x => new object[] { x.Source.Id });
    public static IEnumerable<object[]> RecordMutations => All.SelectMany(x => SourceMutationCases.For(x.Source));
    public static IEnumerable<object[]> MetadataMutations => All.SelectMany(x => SourceMutationCases.For(x.MetadataCase()));
    public static ManifestContractCase Get(string id) => All.Single(x => x.Source.Id == id);
    private static JsonObject Row(string tag, string? name, string evidence, int line, JsonObject attributes, JsonArray? children = null) => new()
    {
        ["kind"] = "manifest-" + tag, ["name"] = name, ["line"] = line,
        ["attributes"] = attributes, ["children"] = children ?? new(), ["evidence_kind"] = evidence,
        ["protection_level_raw"] = attributes[Android + "protectionLevel"]?.DeepClone(),
        ["os_authority"] = "unknown", ["mcp_client_grants"] = "not-inferred"
    };
    private static ManifestContractCase Case(string id, string text, JsonObject[] records, JsonObject? normalization = null,
        string[]? issues = null, int? count = null) => new(
        new(id, "manifest", text, issues is { Length: > 0 } ? StructuralParseStatus.Partial : StructuralParseStatus.Clean,
            records, issues ?? [], "manifest_source six legacy fixture methods; build_catalog namespace permission-definition contract"),
        new() { ["status"] = "parsed", ["normalization"] = normalization, ["permission_elements"] = count ?? records.Count(x => x["kind"]!.GetValue<string>() == "manifest-permission"), ["valid_android_manifest"] = true });
    private static IReadOnlyList<ManifestContractCase> Create()
    {
        const string late = "<!-- license -->\n<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<manifest><permission name=\"p\"/></manifest>";
        const string declaration = "<?xml version=\"1.0\" encoding=\"utf-8\"?>";
        return
        [
            Case("MAN-01", late, [Row("permission", null, "definition-name-unresolved", 3, new() { ["name"] = "p" })],
                new() { ["kind"] = "late-utf8-xml-declaration-masked-after-comments", ["character_span"] = new JsonArray(17, 55), ["raw_declaration"] = declaration, ["raw_source_unchanged"] = true }, ["manifest-permission-name"]),
            Case("MAN-02", "<?xml version=\"1.0\"?><manifest/>", []),
            Case("MAN-03", "<!-- <?xml version=\"1.0\"?> --><manifest/>", []),
            new(new("MAN-04", "manifest", "", StructuralParseStatus.Clean, [], [], "test_empty_source_not_claimed_valid_manifest"),
                new() { ["status"] = "empty-source-no-declarations", ["permission_elements"] = 0, ["valid_android_manifest"] = false }),
            Case("MAN-05", "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\">\n<permission android:name=\"android.permission.CAMERA\" android:protectionLevel=\"dangerous\"/>\n<uses-permission android:name=\"android.permission.CAMERA\"/>\n</manifest>",
                [Row("permission", "android.permission.CAMERA", "definition", 2, new() { [Android + "name"] = "android.permission.CAMERA", [Android + "protectionLevel"] = "dangerous" }),
                 Row("uses-permission", "android.permission.CAMERA", "reference", 3, new() { [Android + "name"] = "android.permission.CAMERA" })]),
            Case("MAN-06", "<manifest xmlns:a=\"http://schemas.android.com/apk/res/android\"><permission-tree a:name=\"p.tree\"/><permission-group a:name=\"p.group\"/><uses-permission-sdk-23 a:name=\"p.use\" a:maxSdkVersion=\"35\"/></manifest>",
                [Row("permission-tree", "p.tree", "tree-definition", 1, new() { [Android + "name"] = "p.tree" }),
                 Row("permission-group", "p.group", "group-definition", 1, new() { [Android + "name"] = "p.group" }),
                 Row("uses-permission-sdk-23", "p.use", "reference", 1, new() { [Android + "name"] = "p.use", [Android + "maxSdkVersion"] = "35" })]),
            Case("MAN-07", "<manifest xmlns:a=\"http://schemas.android.com/apk/res/android\"><permission a:name=\"p\"/><permission a:name=\"p\"/></manifest>",
                [Row("permission", "p", "definition", 1, new() { [Android + "name"] = "p" }), Row("permission", "p", "definition", 1, new() { [Android + "name"] = "p" })], issues: ["manifest-duplicate-permission"]),
            Case("MAN-08", "<manifest xmlns:a=\"http://schemas.android.com/apk/res/android\"><permission a:name=\"p\"><meta-data a:name=\"k\" a:value=\"v\"/></permission></manifest>",
                [Row("permission", "p", "definition", 1, new() { [Android + "name"] = "p" }, new(new JsonObject { ["tag"] = "meta-data", ["attributes"] = new JsonObject { [Android + "name"] = "k", [Android + "value"] = "v" }, ["children"] = new JsonArray(), ["text"] = null, ["tail"] = null }))]),
            Case("MAN-09", "\uFEFF<manifest/>", []),
            Case("MAN-10", "<manifest xmlns:x=\"urn:wrong\"><permission x:name=\"p\"/></manifest>",
                [Row("permission", null, "definition-name-unresolved", 1, new() { ["{urn:wrong}name"] = "p" })], issues: ["manifest-permission-name"]),
            Case("MAN-11", "<manifest><application><permission name=\"nested\"/></application></manifest>",
                [Row("permission", null, "definition-name-unresolved", 1, new() { ["name"] = "nested" })], issues: ["manifest-permission-name"]),
            Case("MAN-12", "<manifest xmlns:a=\"http://schemas.android.com/apk/res/android\" xmlns:x=\"urn:foreign\"><x:permission a:name=\"p\"/></manifest>", [])
        ];
    }
    public static void AssertContract(ManifestContractCase test, ISourceExtractor provider)
    {
        Assert.Equal("manifest", provider.Kind); ExtractionResult? result = null;
        SourceExtractionContractCases.AssertContract(test.Source, new ScriptedSourceExtractor("manifest", source => result = provider.Extract(source)));
        Assert.NotNull(result);
        Assert.True(JsonNode.DeepEquals(test.Metadata, result.Metadata), "Manifest receipt metadata differs");
        for (var i = 0; i < test.Source.ExpectedRecords.Count; i++)
        {
            Assert.True(JsonNode.DeepEquals(test.Source.ExpectedRecords[i], result.Records[i]), "Manifest literal evidence differs");
        }
    }
}

public sealed record ManifestFailureCase(string Id, byte[] Payload, string Message)
{
    public SourceDocument Document() => new("fixture/" + Id + ".xml", Payload.ToArray(), new("fixture", "pin", "snapshot", null));
}
public static class ManifestFailureCases
{
    private static ManifestFailureCase Text(string id, string value, string message = "XML") => new(id, Encoding.UTF8.GetBytes(value), message);
    public static IReadOnlyList<ManifestFailureCase> All { get; } =
    [
        Text("MAN-ERR01", "broken <?xml version=\"1.0\"?><manifest/>"),
        new("MAN-ERR02", [3, 0, 8, 0, 98, 105, 110, 97, 114, 121], "binary XML"),
        Text("MAN-ERR03", "<other/>", "manifest"),
        Text("MAN-ERR04", "<manifest xmlns=\"urn:foreign\"/>", "manifest"),
        Text("MAN-ERR05", "<manifest><permission"),
        Text("MAN-ERR06", "<!-- header -->\n<?xml version=\"1.0\" encoding=\"utf-16\"?><manifest/>"),
        Text("MAN-ERR07", "<!DOCTYPE manifest SYSTEM \"https://fixture.invalid/forbidden\"><manifest/>"),
        Text("MAN-ERR08", "<!DOCTYPE manifest [<!ENTITY x \"expanded\">]><manifest><permission name=\"&x;\"/></manifest>"),
        Text("MAN-ERR09", "<manifest duplicate=\"x\" duplicate=\"y\"/>"),
        Text("MAN-ERR10", "<!--x-->\n<?xml version=\"1.1\"?><manifest/>")
    ];
    public static IEnumerable<object[]> Cases => All.Select(x => new object[] { x.Id });
    public static ManifestFailureCase Get(string id) => All.Single(x => x.Id == id);
    public static void AssertContract(ManifestFailureCase test, ISourceExtractor provider)
    {
        var document = test.Document(); var original = document.Bytes.ToArray(); Assert.Equal("manifest", provider.Kind);
        var error = Assert.Throws<FormatException>(() => provider.Extract(document));
        Assert.Contains(test.Message, error.Message, StringComparison.OrdinalIgnoreCase); Assert.Equal(original, document.Bytes.ToArray());
    }
}
