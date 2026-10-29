using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
public sealed class SdkLegacyMetadataMockTests
{
    [Theory, MemberData(nameof(SdkLegacyMetadataContractCases.Cases), MemberType = typeof(SdkLegacyMetadataContractCases))]
    public void CorrectMocksPreserveThreeOriginalSdkInputsAssertionsAndOuterFacts(string id)
    {
        var test = SdkLegacyMetadataContractCases.Get(id);
        SdkLegacyMetadataContractCases.AssertContract(test, Provider(test, (JsonObject)test.ExpectedMetadata.DeepClone()));
    }
    [Theory, MemberData(nameof(SdkLegacyMetadataContractCases.Mutations), MemberType = typeof(SdkLegacyMetadataContractCases))]
    public void EveryMissingOrChangedSdkSummaryIssueCountOrLocatorFactIsRejected(string id, string section, int ordinal, string key, bool missing)
    {
        var test = SdkLegacyMetadataContractCases.Get(id); var metadata = (JsonObject)test.ExpectedMetadata.DeepClone();
        var target = section == "top" ? metadata : section == "counts" ? metadata["counts_by_kind"]!.AsObject() : metadata[section]![ordinal]!.AsObject();
        if (missing) target.Remove(key);
        else target[key] = target[key] switch
        {
            JsonObject => new JsonObject(), JsonArray array => array.Count == 0 ? new JsonArray(new JsonObject()) : new JsonArray(),
            JsonValue scalar when scalar.TryGetValue<int>(out var count) => JsonValue.Create(count + 1), _ => JsonValue.Create("wrong")
        };
        Assert.NotNull(Record.Exception(() => SdkLegacyMetadataContractCases.AssertContract(test, Provider(test, metadata))));
    }
    private static ISourceExtractor Provider(SdkLegacyMetadataCase test, JsonObject metadata) => new ScriptedSourceExtractor("sdk-api-index", source =>
    {
        var expected = test.Declarations.Document(); Assert.Equal(expected.RelativePath, source.RelativePath);
        Assert.Equal(expected.Bytes.ToArray(), source.Bytes.ToArray()); Assert.Equal(expected.Context, source.Context);
        return new(test.Declarations.ExpectedRecords.Select(row => (JsonObject)row.DeepClone()).ToArray(),
            test.Declarations.IssueCodes.Select(code => new SourceIssue(code, "Unsupported JVM descriptor wat)", null, null)).ToArray(),
            test.Declarations.Status, metadata);
    });
}
