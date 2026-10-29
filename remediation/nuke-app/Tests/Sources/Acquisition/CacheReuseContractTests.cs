using AndroidMcp.BuildTool.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;
public sealed record CacheReuseCase(string Id,string Error);
public interface ICacheReuseProbe
{
    Task VerifyAsync(CacheReuseCase test);
    int Writes {get;}
    bool OriginalReceiptRetained {get;}
}
public static class CacheReuseCases
{
    public static IEnumerable<object[]> All=>new[]{new CacheReuseCase("missing-sealed-data","FileNotFoundException"),new CacheReuseCase("tampered-data","CACHE_HASH"),new CacheReuseCase("changed-producer","CACHE_IDENTITY"),new CacheReuseCase("changed-recipe","CACHE_IDENTITY"),new CacheReuseCase("changed-records","CACHE_IDENTITY")}.Select(x=>new object[]{x});
    public static async Task AssertCase(CacheReuseCase test,ICacheReuseProbe provider)
    {
        var error=await Record.ExceptionAsync(()=>provider.VerifyAsync(test));Assert.NotNull(error);
        if(test.Error=="FileNotFoundException")Assert.IsType<FileNotFoundException>(error);else{Assert.IsType<TransportException>(error);Assert.Equal(test.Error,((TransportException)error!).Code);}
        Assert.Equal(0,provider.Writes);Assert.True(provider.OriginalReceiptRetained);
    }
}
public sealed class ScriptedCacheReuse(string? mutation=null):ICacheReuseProbe
{
    public int Writes=>mutation=="writes"?1:0;public bool OriginalReceiptRetained=>mutation!="receipt";
    public Task VerifyAsync(CacheReuseCase test)
    {if(mutation=="error")return Task.CompletedTask;if(test.Error=="FileNotFoundException")throw new FileNotFoundException();throw new TransportException(mutation=="code"?"wrong":test.Error,"Scripted sealed cache rejection");}
}
[Trait("Gate","Mock")]
public sealed class CacheReuseMockTests
{
    [Theory,MemberData(nameof(CacheReuseCases.All),MemberType=typeof(CacheReuseCases))]
    public Task CorrectRetentionAndRejections(CacheReuseCase test)=>CacheReuseCases.AssertCase(test,new ScriptedCacheReuse());
    [Theory,MemberData(nameof(CacheReuseCases.All),MemberType=typeof(CacheReuseCases))]
    public async Task ErrorWriteAndRetentionMutationsRejected(CacheReuseCase test)
    {foreach(var mutation in test.Error=="FileNotFoundException"?new[]{"error","writes","receipt"}:new[]{"error","code","writes","receipt"})Assert.NotNull(await Record.ExceptionAsync(()=>CacheReuseCases.AssertCase(test,new ScriptedCacheReuse(mutation))));}
}
