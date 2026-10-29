using System.Text.Json;
using AndroidMcp.BuildTool.Catalog;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
public sealed class CatalogMetaSchemaConcurrencyRealTests
{
    [Fact]
    public async Task ConcurrentValidAndMalformedSchemasRetainIndependentOutcomes()
    {
        using var valid=JsonDocument.Parse("""
            {"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","properties":{"x":{"type":"string"}},"required":["x"]}
            """);
        using var invalid=JsonDocument.Parse("""
            {"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","properties":{"x":{"type":"string"}},"required":["x","x"]}
            """);
        var beforeValid=valid.RootElement.GetRawText();var beforeInvalid=invalid.RootElement.GetRawText();
        var start=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var workers=Enumerable.Range(0,8).Select(worker=>Task.Run(async()=>
        {
            await start.Task;
            for(var iteration=0;iteration<8;iteration++)
            {
                var expected=(worker+iteration)%2==0;
                Assert.Equal(expected,CatalogSourceMetadata.IsValidSchema(expected?valid.RootElement:invalid.RootElement));
            }
            return 8;
        })).ToArray();
        start.SetResult(true);
        Assert.Equal(64,(await Task.WhenAll(workers)).Sum());
        Assert.Equal(beforeValid,valid.RootElement.GetRawText());
        Assert.Equal(beforeInvalid,invalid.RootElement.GetRawText());
    }
}
