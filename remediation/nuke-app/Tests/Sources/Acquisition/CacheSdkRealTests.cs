using System.IO.Compression;
using System.Text;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;
public sealed class CacheWireRealProbe:ICacheWireProbe
{
    public async Task<byte[]> ReadDecodedPayloadAsync()
    {
        var store=new MemoryAcquisitionStore();var cache=new SourceCache(store,new ScriptedLeaseProvider(),new CanonicalJson());await cache.WriteAsync(SourceCacheCases.Request);
        using var input=new MemoryStream((await store.ReadAsync(SourceCacheCases.Request.Path)).ToArray());using var gzip=new GZipStream(input,CompressionMode.Decompress);using var output=new MemoryStream();await gzip.CopyToAsync(output);return output.ToArray();
    }
}
public sealed class CacheReuseRealProbe:ICacheReuseProbe
{
    private readonly MemoryAcquisitionStore store=new();private int baseline;private byte[]? original;
    public int Writes=>store.Writes-baseline;
    public bool OriginalReceiptRetained=>original is not null&&store.Files[SourceCacheCases.Request.Path+".csharp-cache.json"].Bytes.Span.SequenceEqual(original);
    public async Task VerifyAsync(CacheReuseCase test)
    {
        var cache=new SourceCache(store,new ScriptedLeaseProvider(),new CanonicalJson());var request=SourceCacheCases.Request;
        await cache.WriteAsync(request);baseline=store.Writes;original=(await store.ReadAsync(request.Path+".csharp-cache.json")).ToArray();
        switch(test.Id)
        {
            case "missing-sealed-data":store.Files.Remove(request.Path);break;
            case "tampered-data":store.Files[request.Path]=new(request.Path,new byte[]{0},"application/gzip");break;
            case "changed-producer":request=request with{ProducerFiles=new Dictionary<string,string>{{"producer.cs",new string('4',64)}}};break;
            case "changed-recipe":request=request with{RecipeSha256=new string('5',64)};break;
            case "changed-records":request=request with{RecordsJson=Encoding.UTF8.GetBytes("[{\"value\":2}]")};break;
            default:throw new ArgumentException(test.Id);
        }
        await cache.WriteAsync(request);
    }
}
[Trait("Gate","Real")]
public sealed class CacheSdkRealTests
{
    [Theory,MemberData(nameof(SdkVersionCases.All),MemberType=typeof(SdkVersionCases))]
    public void SameSdkXmlVersionCases(SdkVersionCase test)=>SdkVersionCases.AssertCase(test,new SdkArchiveVersionValidator());
    [Fact]public Task SameCacheJsonLinesWireAssertions()=>CacheWireCases.AssertCase(new CacheWireRealProbe());
    [Theory,MemberData(nameof(CacheReuseCases.All),MemberType=typeof(CacheReuseCases))]
    public Task SameCacheReuseRetentionAssertions(CacheReuseCase test)=>CacheReuseCases.AssertCase(test,new CacheReuseRealProbe());
}
