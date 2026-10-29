using System.Text.Json.Nodes;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

// Only two previously indirect original assertions. All pre-existing replies
// still pass their original assertion functions before these witnesses run.
public static class CatalogOriginalAssertionGapCases
{
    public static readonly CatalogBuildCase GroupCase=CatalogBuildCases.All.Select(row=>(CatalogBuildCase)row[0]).Single(test=>test.Id=="BUILD-05-same-identity-two-immutable-occurrences");
    public static readonly CatalogDevicePipelineCase SdkCase=CatalogDevicePipelineCases.All.Select(row=>(CatalogDevicePipelineCase)row[0]).Single(test=>test.Id=="PIPE-01-source-index-history-container");
    public const string OriginalGroupedSql="SELECT b.binding_id,count(*) FROM bindings b JOIN occurrences o USING(binding_id) WHERE b.signature='java:a.A#go(int)' GROUP BY b.binding_id";
    public static JsonArray ExpectedExtensions()=>JsonNode.Parse("""
        [{"sdk_id":"30","minimum_version":"4"},{"sdk_id":"0","minimum_version":"34"}]
        """)!.AsArray();
    public static void AssertGrouped(CatalogBuildReply observed,IReadOnlyList<(string BindingId,long Occurrences)> groups)
    {
        using var provider=new ObservedBuild(observed);
        CatalogBuildCases.AssertCase(GroupCase,provider);
        Assert.Equal(1,provider.Calls);
        Assert.Single(groups);Assert.Equal(2,groups[0].Occurrences);
    }
    public static async Task AssertExtensions(CatalogDevicePipelineReply observed,JsonArray extensions)
    {
        var provider=new ObservedPipeline(observed);
        await CatalogDevicePipelineCases.AssertCase(SdkCase,provider);
        Assert.Equal(1,provider.Calls);
        Assert.True(JsonNode.DeepEquals(ExpectedExtensions(),extensions),"Exact original sdk_extensions list/object equality");
    }
    private sealed class ObservedBuild(CatalogBuildReply observed):ICatalogBuildCaseProvider
    {
        public int Calls {get;private set;}
        public CatalogBuildReply Build(CatalogBuildCall call){Assert.Same(GroupCase.Call,call);Calls++;return observed;}
        public void Dispose() { }
    }
    private sealed class ObservedPipeline(CatalogDevicePipelineReply observed):ICatalogDevicePipelineCaseProvider
    {
        public int Calls {get;private set;}
        public Task<CatalogDevicePipelineReply> RunAsync(CatalogDevicePipelineCall call){Assert.Same(SdkCase.Call,call);Calls++;return Task.FromResult(observed);}
    }
}
