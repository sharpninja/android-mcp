using System.Text;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;
public static class SourceCacheCases
{
    public static SourceCacheRequest Request=>new("cache/items.json.gz","sdk-api-index","csharp-v1","candidate",new string('1',64),AcquisitionCases.AbcSha,Encoding.UTF8.GetBytes("[{ \"value\": 1 }]"),new string('2',64),ProducerFiles:new Dictionary<string,string>{{"producer.cs",new string('3',64)}});
    public static readonly byte[] Expected=Encoding.UTF8.GetBytes("[{\"value\":1}]");
    public static async Task AssertCase(ISourceCache cache)
    {
        var r=await cache.WriteAsync(Request);Assert.Equal(Request.Path,r.Path);Assert.Equal(Request.ProducerId,r.ProducerId);Assert.Equal(Request.ProfileId,r.ProfileId);
        Assert.Equal(Request.RecipeSha256,r.RecipeSha256);Assert.Equal(Request.SourceSha256,r.SourceSha256);Assert.Equal(1,r.RecordCount);
        Assert.Equal(Request.HistoricalGZipSha256,r.HistoricalGZipSha256);Assert.True(r.DerivedGeneration);Assert.Matches("^[a-f0-9]{64}$",r.ProducerFingerprint);Assert.NotEqual(Request.HistoricalGZipSha256,r.GZipSha256);
        Assert.Equal(Expected,(await cache.ReadAsync(Request)).ToArray());Assert.Equal(r,await cache.WriteAsync(Request));
        var strict=await Record.ExceptionAsync(()=>cache.WriteAsync(Request with{Path="cache/strict.json.gz",StrictHistoricalBytes=true}));Assert.IsType<TransportException>(strict);Assert.Equal("CACHE_BYTES",((TransportException)strict!).Code);
    }
}
public sealed class ScriptedSourceCache(string? mutation=null):ISourceCache
{
    public Task<SourceCacheReceipt> WriteAsync(SourceCacheRequest request,CancellationToken cancellationToken=default)
    {
        if(request.StrictHistoricalBytes&&mutation!="strict")throw new TransportException("CACHE_BYTES","Historical gzip identity differs.");
        var r=new SourceCacheReceipt(SourceCacheCases.Request.Path,"csharp-v1","candidate",new string('1',64),AcquisitionCases.AbcSha,new string('3',64),new string('4',64),new string('2',64),1,true);
        return Task.FromResult(mutation switch{"path"=>r with{Path="wrong"},"producer"=>r with{ProducerId="python"},"profile"=>r with{ProfileId="wrong"},"recipe"=>r with{RecipeSha256="wrong"},"source"=>r with{SourceSha256="wrong"},"count"=>r with{RecordCount=2},"historical"=>r with{HistoricalGZipSha256=null},"derived"=>r with{DerivedGeneration=false},"fingerprint"=>r with{ProducerFingerprint="wrong"},"gzip"=>r with{GZipSha256=new string('2',64)},_=>r});
    }
    public Task<ReadOnlyMemory<byte>> ReadAsync(SourceCacheRequest request,CancellationToken cancellationToken=default)=>Task.FromResult<ReadOnlyMemory<byte>>(mutation=="bytes"?new byte[]{0}:SourceCacheCases.Expected);
}
[Trait("Gate","Mock")]
public sealed class SourceCacheMockTests
{
    [Fact]public Task CorrectMock()=>SourceCacheCases.AssertCase(new ScriptedSourceCache());
    [Theory][InlineData("path")][InlineData("producer")][InlineData("profile")][InlineData("recipe")][InlineData("source")][InlineData("count")][InlineData("historical")][InlineData("derived")][InlineData("fingerprint")][InlineData("gzip")][InlineData("bytes")][InlineData("strict")]
    public async Task IndividualPinsAndBytePolicyMutationsRejected(string mutation)=>Assert.NotNull(await Record.ExceptionAsync(()=>SourceCacheCases.AssertCase(new ScriptedSourceCache(mutation))));
}
