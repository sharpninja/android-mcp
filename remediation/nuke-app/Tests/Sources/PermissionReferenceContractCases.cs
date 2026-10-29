using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record PermissionReferenceCase(string Id, string Html, JsonObject Expected)
{
    public SourceExtractionCase MutationCase() => new(Id, "permission-reference", Html, StructuralParseStatus.Clean, [Expected], [], "permission_reference.parse literal named-section contracts");
}

public static class PermissionReferenceContractCases
{
    private const string Authority = "Documentation fact only; not a compiled definition, OS grant or MCP client permission";
    private const string Semantics = "Unversioned official API reference snapshot; raw introduced/deprecated/removed and protection metadata preserved separately";
    private const string Missing = "Missing or ambiguous documented constant literal";
    private static string Field(string name, string value = "", string attrs = "data-version-added=\"37.2\"") =>
        "<div " + attrs + "><h3 class=\"api-name\" id=\"" + name + "\">" + name + "</h3><div>Added in 37.2</div><pre class=\"api-signature\">public static final <a>String</a> " + name + "</pre><p>Protection level: internal|role</p>" + value + "</div>";
    private static JsonObject Row(string name, string? value, string? unknown = null, string introduced = "37.2", string? deprecated = null, string? removed = null, int line = 1) => new()
    {
        ["name"] = name, ["symbol"] = "android.Manifest.permission." + name, ["value"] = value, ["line"] = line,
        ["introduced_raw"] = introduced, ["deprecated_raw"] = deprecated, ["removed_raw"] = removed,
        ["protection_level_raw"] = "internal|role", ["signature"] = "public static final String " + name,
        ["value_status"] = value is null ? "unknown" : "documented-constant", ["unknown_reason"] = unknown, ["authority"] = Authority
    };
    private static JsonObject Result(JsonObject[] fields, string[]? duplicates = null, JsonObject[]? other = null, int unclosed = 0) => new()
    {
        ["fields"] = new JsonArray(fields.Cast<JsonNode>().ToArray()), ["duplicate_field_names"] = new JsonArray((duplicates ?? []).Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
        ["nonconstant_or_unsupported_sections"] = new JsonArray((other ?? []).Cast<JsonNode>().ToArray()), ["unclosed_version_sections"] = unclosed,
        ["known_values"] = fields.Count(x => x["value"] is not null), ["unknown_values"] = fields.Count(x => x["value"] is null), ["source_semantics"] = Semantics
    };
    public static IReadOnlyList<PermissionReferenceCase> All { get; } =
    [
        new("REF-01", Field("X", "<p>Constant Value: &quot;other.permission.X&quot;</p>"), Result([Row("X", "other.permission.X")])),
        new("REF-02", Field("X", attrs: "data-version-added=\"1\" data-version-deprecated=\"16\""), Result([Row("X", null, Missing, "1", "16")])),
        new("REF-03", Field("X", "Constant Value: \"a.permission.X\"") + Field("X", "Constant Value: \"b.permission.X\""), Result([Row("X", "a.permission.X"), Row("X", "b.permission.X")], ["X"])),
        new("REF-04", "<script>" + Field("FAKE", "Constant Value: \"a.permission.FAKE\"") + "</script>" + Field("REAL", "Constant Value: \"a.permission.REAL\""), Result([Row("REAL", "a.permission.REAL")])),
        new("REF-05", "<style>" + Field("FAKE", "Constant Value: \"a.permission.FAKE\"") + "</style>" + Field("REAL", "Constant Value: \"a.permission.REAL\""), Result([Row("REAL", "a.permission.REAL")])),
        new("REF-06", Field("X", "<p>Constant Value: \"a.permission.X\"</p><p>Constant Value: \"b.permission.X\"</p>"), Result([Row("X", null, Missing)])),
        new("REF-07", "\n\n" + Field("X", "Constant Value: \"a.permission.X\"", "data-version-added=\"1\" data-version-removed=\"37.2\""), Result([Row("X", "a.permission.X", introduced: "1", removed: "37.2", line: 3)])),
        new("REF-08", "<div data-version-added=\"1\"><h3 class=\"api-name\" id=\"N\">N</h3><pre class=\"api-signature\">public static final int N</pre></div>",
            Result([], other: [new() { ["name"] = "N", ["line"] = 1, ["signature"] = "public static final int N", ["reason"] = "Not a recognized documented String constant section" }])),
        new("REF-09", "<div data-version-added=\"1\"><h3 class=\"api-name\" id=\"X\">X</h3><pre class=\"api-signature\">public static final String X</pre>", Result([], unclosed: 1)),
        new("REF-10", "<!--" + Field("FAKE", "Constant Value: \"a.permission.FAKE\"") + "-->", Result([])),
        new("REF-11", "", Result([])),
        new("REF-12", Field("X", "Constant Value: \"a.permission.\\x\""), Result([Row("X", null, "Documented literal is not a recognized string encoding")])),
        new("REF-13", Field("X", "Constant Value: \"other.permission.\\u0041\""), Result([Row("X", "other.permission.A")]))
    ];
    public static IEnumerable<object[]> Cases => All.Select(x => new object[] { x.Id });
    public static IEnumerable<object[]> Mutations => All.SelectMany(x => SourceMutationCases.For(x.MutationCase()));
    public static PermissionReferenceCase Get(string id) => All.Single(x => x.Id == id);
    public static void AssertContract(PermissionReferenceCase test, IPermissionReferenceSourceFacts provider)
    {
        var bytes = Encoding.UTF8.GetBytes(test.Html); var original = bytes.ToArray(); var result = provider.Parse(bytes);
        Assert.True(JsonNode.DeepEquals(test.Expected, result), "Permission documentation facts differ: " + test.Id);
        Assert.Equal(original, bytes);
    }
}
