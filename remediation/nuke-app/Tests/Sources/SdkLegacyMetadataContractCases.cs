using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record SdkLegacyMetadataCase(SourceExtractionCase Declarations, JsonObject ExpectedMetadata);
public static class SdkLegacyMetadataContractCases
{
    private const string Scope = "All lifecycle records retained; excluded is a historical not-current diagnostic, not omission from records";
    public static IReadOnlyList<SdkLegacyMetadataCase> All { get; } = Create();
    public static IEnumerable<object[]> Cases => All.Select(test => new object[] { test.Declarations.Id });
    public static SdkLegacyMetadataCase Get(string id) => All.Single(test => test.Declarations.Id == id);
    private static JsonObject Summary(JsonArray excluded, JsonArray issues, params (string Kind, int Count)[] counts) => new()
    {
        ["excluded_history_or_future"] = excluded.Count, ["excluded"] = excluded, ["records_scope"] = Scope, ["issues"] = issues,
        ["counts_by_kind"] = new JsonObject(counts.Select(pair => new KeyValuePair<string, JsonNode?>(pair.Kind, JsonValue.Create(pair.Count)))),
        ["target_platform_api"] = "37.2", ["index_format_version"] = "4", ["line_locator_semantics"] = "physical XML line from .NET XmlReader"
    };
    private static JsonObject Excluded(string field, string attribute, string value) => new()
    { ["owner"] = "a/Outer$Inner", ["tag"] = "field", ["attributes"] = new JsonObject { ["name"] = field, [attribute] = value }, ["reason"] = "not applicable at target platform API" };
    private static IReadOnlyList<SdkLegacyMetadataCase> Create()
    {
        var lifecycle = SdkXmlContractCases.Get("SDK-01"); var field = SdkXmlContractCases.Get("SDK-02"); var originalBroken = SdkXmlContractCases.Get("SDK-03");
        var broken = new SourceExtractionCase("SDK-LEGACY-BROKEN", "sdk-api-index",
            "<api version=\"4\"><class name=\"a/C\"><method name=\"broken(wat)\"/></class></api>", StructuralParseStatus.Partial,
            originalBroken.ExpectedRecords.Take(2).Select(row => (JsonObject)row.DeepClone()).ToArray(), ["sdk-descriptor"],
            "Exact canonical/release011 original test_invalid_method_descriptor_retained_unknown single-method input and final-row assertion");
        return
        [
            new(lifecycle, Summary(new JsonArray(Excluded("OLD", "removed", "37.2"), Excluded("FUTURE", "since", "37.3")), [], ("type", 1), ("constructor", 1), ("method", 1), ("field", 2))),
            new(field, Summary([], [], ("type", 1), ("field", 1))),
            new(broken, Summary([], new JsonArray(new JsonObject { ["owner"] = "a/C", ["name"] = "broken(wat)", ["error"] = "Unsupported JVM descriptor wat)" }), ("type", 1), ("unsupported", 1)))
        ];
    }
    public static void AssertContract(SdkLegacyMetadataCase test, ISourceExtractor provider)
    {
        ExtractionResult? observed = null;
        SdkXmlContractCases.AssertContract(test.Declarations, new ScriptedSourceExtractor(provider.Kind, source => observed = provider.Extract(source)));
        Assert.NotNull(observed); Assert.True(JsonNode.DeepEquals(test.ExpectedMetadata, observed.Metadata), "SDK outer metadata differs for " + test.Declarations.Id);
        if (test.Declarations.Id == "SDK-01")
        {
            Assert.Equal(5, observed.Records.Count); Assert.Equal(2, observed.Metadata!["excluded_history_or_future"]!.GetValue<int>());
            Assert.Equal(3, observed.Records.Count(row => row["extras"]!["lifecycle_status"]!.GetValue<string>() == "current-at-target"));
        }
        if (test.Declarations.Id == "SDK-LEGACY-BROKEN")
        { Assert.Equal("unsupported", observed.Records[^1]["kind"]!.GetValue<string>()); Assert.Contains("descriptor", observed.Records[^1]["extras"]!["parse_error"]!.GetValue<string>()); }
    }
    public static IEnumerable<object[]> Mutations
    {
        get
        {
            foreach (var test in All)
            {
                foreach (var pair in test.ExpectedMetadata) foreach (var missing in new[] { false, true }) yield return [test.Declarations.Id, "top", -1, pair.Key, missing];
                foreach (var pair in test.ExpectedMetadata["counts_by_kind"]!.AsObject()) foreach (var missing in new[] { false, true }) yield return [test.Declarations.Id, "counts", -1, pair.Key, missing];
                foreach (var section in new[] { "excluded", "issues" })
                    for (var index = 0; index < test.ExpectedMetadata[section]!.AsArray().Count; index++)
                        foreach (var pair in test.ExpectedMetadata[section]![index]!.AsObject()) foreach (var missing in new[] { false, true }) yield return [test.Declarations.Id, section, index, pair.Key, missing];
            }
        }
    }
}
