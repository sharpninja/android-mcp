using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record ApexManifestCase(string Id, string Input, JsonObject Expected, bool Diagnostic = false)
{
    public JsonObject CorrectResult() { var result = (JsonObject)Expected.DeepClone(); if (Diagnostic) result["reason"] = "Malformed JSON fixture diagnostic"; return result; }
    public SourceExtractionCase MutationCase() => new(Id, "apex-manifest", Input, StructuralParseStatus.Clean, [Expected], [], "apex_manifest_facts nine legacy test methods, separate build-module fixtures in Soong");
}
public static class ApexManifestContractCases
{
    private const string Applicability = "Product selection, build version overrides and installed module version range are unproven";
    private static ApexManifestCase Parsed(string id, string text, JsonObject raw, string? normalized, bool comments = false)
    {
        var name = raw["name"] is JsonValue value && value.TryGetValue<string>(out var literalName) && literalName.Length > 0 ? literalName : null;
        return new(id, text, new()
        {
            ["status"] = "source-manifest-literals-parsed", ["name"] = name, ["version_literal"] = raw["version"]?.DeepClone(), ["normalized_version_code"] = normalized,
            ["raw_properties"] = raw, ["comment_normalization"] = comments ? "comments masked without altering string literals or line positions" : null, ["raw_source_text"] = text,
            ["version_domain"] = "APEX source-manifest version code, not Android API or AIDL frozen version", ["installed_identity_or_version_proven"] = false,
            ["applicability"] = new JsonObject { ["unknown_reason"] = Applicability }
        });
    }
    private static ApexManifestCase Ambiguous(string id, string text, params string[] duplicates) => new(id, text, new()
    { ["status"] = "ambiguous-json", ["duplicate_keys"] = new JsonArray(duplicates.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()), ["raw"] = text, ["applicability"] = new JsonObject { ["unknown_reason"] = "APEX manifest object/keys unresolved" } });
    public static IReadOnlyList<ApexManifestCase> All { get; } =
    [
        Parsed("APEX-01", "{\"name\":\"com.android.foo\", // Placeholder version replaced during build\n \"version\":0}", new() { ["name"] = "com.android.foo", ["version"] = 0 }, "0", true),
        Parsed("APEX-02", "{\"name\":\"url://not-a-comment\",\"version\":1}", new() { ["name"] = "url://not-a-comment", ["version"] = 1 }, "1"),
        Parsed("APEX-03", "{\"name\":\"com.android.foo\",\"version\":1}", new() { ["name"] = "com.android.foo", ["version"] = 1 }, "1"),
        Parsed("APEX-04", "{\"version\":\"9223372036854775807\"}", new() { ["version"] = "9223372036854775807" }, "9223372036854775807"),
        Parsed("APEX-05", "{\"version\": true}", new() { ["version"] = true }, null),
        Parsed("APEX-06", "{\"version\": -1}", new() { ["version"] = -1 }, null),
        Parsed("APEX-07", "{\"version\": \"${BUILD_VERSION}\"}", new() { ["version"] = "${BUILD_VERSION}" }, null),
        Parsed("APEX-08", "{\"version\": 9223372036854775808}", new() { ["version"] = 9223372036854775808UL }, null),
        Ambiguous("APEX-09", "{\"name\":\"a\",\"name\":\"b\"}", "name"),
        new("APEX-10", "{unfinished", new() { ["status"] = "unsupported-json", ["raw"] = "{unfinished", ["applicability"] = new JsonObject { ["unknown_reason"] = "APEX source manifest could not be parsed" } }, true),
        Ambiguous("APEX-11", "[]"),
        Ambiguous("APEX-12", "{\"nested\":{\"x\":1,\"x\":2}}", "x"),
        Parsed("APEX-13", "{\"name\":\"\",\"version\":1}", new() { ["name"] = "", ["version"] = 1 }, "1"),
        Parsed("APEX-14", "{\"name\":true,\"version\":0}", new() { ["name"] = true, ["version"] = 0 }, "0"),
        Parsed("APEX-15", "{\"version\":\"01\"}", new() { ["version"] = "01" }, null),
        Parsed("APEX-16", "{\"version\":1.0}", new() { ["version"] = 1.0 }, null),
        Parsed("APEX-17", "{\"version\":1e0}", new() { ["version"] = 1.0 }, null),
        Parsed("APEX-18", "{\"version\":\"0\"}", new() { ["version"] = "0" }, "0"),
        Parsed("APEX-19", "{\"version\":\"9223372036854775808\"}", new() { ["version"] = "9223372036854775808" }, null),
        Parsed("APEX-20", "{\"version\":\"\"}", new() { ["version"] = "" }, null),
        Parsed("APEX-21", "{\"version\":\"-1\"}", new() { ["version"] = "-1" }, null),
        Parsed("APEX-22", "{\"version\":null}", new() { ["version"] = null }, null),
        Ambiguous("APEX-23", "{\"x\":1,\"nested\":{\"y\":1,\"y\":2},\"x\":2}", "y", "x")
    ];
    public static IEnumerable<object[]> Cases => All.Select(x => new object[] { x.Id });
    public static IEnumerable<object[]> Mutations => All.SelectMany(x => SourceMutationCases.For(x.MutationCase()));
    public static ApexManifestCase Get(string id) => All.Single(x => x.Id == id);
    public static void AssertContract(ApexManifestCase test, IApexManifestSourceFacts provider)
    {
        var actual = (JsonObject)provider.Manifest(test.Input).DeepClone();
        if (test.Diagnostic)
        { Assert.True(actual.ContainsKey("reason")); Assert.False(string.IsNullOrWhiteSpace(actual["reason"]?.GetValue<string>())); actual.Remove("reason"); }
        Assert.True(JsonNode.DeepEquals(test.Expected, actual), "APEX source literals differ: " + test.Id);
    }
}
