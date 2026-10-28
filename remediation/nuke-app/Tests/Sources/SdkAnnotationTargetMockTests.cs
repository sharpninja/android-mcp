using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources;
using Xunit;
using Xunit.Sdk;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class ScriptedAnnotationTargetSpelling(Func<string, JsonObject> response) : IAnnotationTargetSpellingSourceFacts
{ public JsonObject NormalizeTarget(string itemName) => response(itemName); }

[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
public sealed class SdkAnnotationTargetMockTests
{
    [Theory, MemberData(nameof(SdkAnnotationTargetContractCases.Cases), MemberType = typeof(SdkAnnotationTargetContractCases))]
    public void CorrectMocksPreserveLiteralTargetSpellingFacts(string id)
    {
        var test = SdkAnnotationTargetContractCases.Get(id);
        SdkAnnotationTargetContractCases.AssertContract(test, new ScriptedAnnotationTargetSpelling(item =>
        { Assert.Equal(test.Item, item); return (JsonObject)test.Expected.DeepClone(); }));
    }
    [Theory, MemberData(nameof(SdkAnnotationTargetContractCases.Mutations), MemberType = typeof(SdkAnnotationTargetContractCases))]
    public void EveryIndividualTargetScopeTypeAndRootMutationIsRejected(string id, SourceMutation mutation)
    {
        var test = SdkAnnotationTargetContractCases.Get(id); var incorrect = SourceMutationCases.Apply(test.MutationCase(), mutation).Records[0];
        Assert.ThrowsAny<XunitException>(() => SdkAnnotationTargetContractCases.AssertContract(test, new ScriptedAnnotationTargetSpelling(_ => incorrect)));
    }
    [Fact] public void SourceFixturesRetainEveryCatalogOriginalItemSpelling()
    {
        foreach (var data in AndroidMcp.BuildTool.Tests.Catalog.CatalogAnnotationImportCases.All)
        {
            var test = Assert.IsType<AndroidMcp.BuildTool.Tests.Catalog.CatalogAnnotationImportCase>(data[0]);
            Assert.Contains(SdkAnnotationTargetContractCases.All, source => source.Item == test.Call.Item);
        }
    }
}
