using System.IO.Compression;
using System.Text;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;
public sealed record HistoricalCacheCase(string Id,HistoricalCachePin Pin,string? Error);
public static class HistoricalCacheCases
{
    public static byte[] GZipFixture(){using var output=new MemoryStream();using(var gz=new GZipStream(output,CompressionLevel.Optimal,true))gz.Write(CacheWireCases.Expected);return output.ToArray();}
    public static HistoricalCachePin Pin=>new("cache/historical.json.gz","java-source",ContentHash.Sha256(GZipFixture()),new string('3',64),1,AcquisitionCases.AbcSha,new string('1',64));
    public static IEnumerable<object[]> All=>new[]{new HistoricalCacheCase("java",Pin,null),new HistoricalCacheCase("sdk",Pin with{Kind="sdk-api-index"},null),new HistoricalCacheCase("kind",Pin with{Kind="native"},"CACHE_KIND"),new HistoricalCacheCase("hash",Pin with{GZipSha256=new string('0',64)},"CACHE_HASH"),new HistoricalCacheCase("count",Pin with{RecordCount=2},"CACHE_RECORDS"),new HistoricalCacheCase("producer",Pin with{ProducerFingerprint="wrong"},"CACHE_IDENTITY")}.Select(x=>new object[]{x});
    public static async Task AssertCase(HistoricalCacheCase test,IHistoricalSourceCache cache)
    {
        if(test.Error is not null){var e=await Record.ExceptionAsync(()=>cache.ReadAsync(test.Pin));Assert.IsType<TransportException>(e);Assert.Equal(test.Error,((TransportException)e!).Code);return;}
        var result=await cache.ReadAsync(test.Pin);Assert.Equal(test.Pin,result.Pin);Assert.True(result.HistoricalProducer);Assert.Equal(SourceCacheCases.Expected,result.RecordsJson.ToArray());
    }
}
public sealed class ScriptedHistoricalCache(HistoricalCacheCase test,string? mutation=null):IHistoricalSourceCache
{
    public Task<HistoricalCacheResult> ReadAsync(HistoricalCachePin pin,CancellationToken cancellationToken=default)
    {
        Assert.Equal(test.Pin,pin);if(test.Error is not null&&mutation!="error")throw new TransportException(mutation=="code"?"wrong":test.Error,"Scripted historical rejection");
        var changed=mutation switch{"path"=>pin with{Path="wrong"},"kind"=>pin with{Kind="wrong"},"hash"=>pin with{GZipSha256=new string('4',64)},"producer"=>pin with{ProducerFingerprint=new string('4',64)},"count"=>pin with{RecordCount=2},"source"=>pin with{SourceSha256=new string('4',64)},"recipe"=>pin with{RecipeSha256=new string('4',64)},_=>pin};
        return Task.FromResult(new HistoricalCacheResult(changed,mutation=="bytes"?new byte[]{0}:SourceCacheCases.Expected,mutation!="identity"));
    }
}
[Trait("Gate","Mock")]
public sealed class HistoricalCacheMockTests
{
    [Theory,MemberData(nameof(HistoricalCacheCases.All),MemberType=typeof(HistoricalCacheCases))]
    public Task CorrectHistoricalIdentity(HistoricalCacheCase test)=>HistoricalCacheCases.AssertCase(test,new ScriptedHistoricalCache(test));
    [Theory,MemberData(nameof(HistoricalCacheCases.All),MemberType=typeof(HistoricalCacheCases))]
    public async Task IdentityBytesAndRejectionMutationsRejected(HistoricalCacheCase test)
    {foreach(var mutation in test.Error is null?new[]{"path","kind","hash","producer","count","source","recipe","bytes","identity"}:new[]{"error","code"})Assert.NotNull(await Record.ExceptionAsync(()=>HistoricalCacheCases.AssertCase(test,new ScriptedHistoricalCache(test,mutation))));}
}
