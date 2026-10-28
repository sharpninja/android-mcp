using System.Text.Json.Nodes;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogBuildCacheReply(CatalogBuildReply BuildReply,bool CacheBytesRetained,string Disposition,string HistoricalProducer,
    int DeclaredHistoricalRecords,string HistoricalContext,int NativeDeclarations,bool HistoricalRecordsConsumed,bool HistoricalIdentityClaimed);
public sealed record CatalogBuildCacheCase(string Id,CatalogBuildCall Call,CatalogBuildCacheReply Expected);
public interface ICatalogBuildCacheCaseProvider { CatalogBuildCacheReply Build(CatalogBuildCall call); }
public static class CatalogBuildCacheCases
{
    public const string CacheText="deliberately nongzip historical cache bytes; native raw extraction must not parse these";
    public const string Producer="2222222222222222222222222222222222222222222222222222222222222222";
    public const string Context="{\"imports\":[\"old.Type\"],\"package\":\"fixture-cache-only\",\"snapshot\":\"historical\"}";
    public static JsonObject HistoricalContext()=>new(){["imports"]=new JsonArray("old.Type"),["package"]="fixture-cache-only",["snapshot"]="historical"};
    public static IEnumerable<object[]> All=>new[]
    {
        new CatalogBuildCacheCase("CACHE-BUILD-01-verified-unused-stale-nongzip-context",new("verified-unused-cache"),new(CatalogBuildCases.Base,true,"verified-unused/native-raw-reextraction",Producer,999,Context,10,false,false)),
        new CatalogBuildCacheCase("CACHE-BUILD-02-raw-duplicate-occurrence-count",new("verified-unused-cache-duplicate-source"),new(CatalogBuildCases.Base with{Sources=4,Occurrences=18,Declarations=18,GoIntOccurrences=2},true,"verified-unused/native-raw-reextraction",Producer,999,Context,18,false,false))
    }.Select(value=>new object[]{value});
    public static void AssertCase(CatalogBuildCacheCase test,ICatalogBuildCacheCaseProvider provider)=>Assert.Equal(test.Expected,provider.Build(test.Call));
}
public sealed class ScriptedCatalogBuildCache(CatalogBuildCacheCase test,int wrong=-1):ICatalogBuildCacheCaseProvider
{
    public CatalogBuildCacheReply Build(CatalogBuildCall call)
    {
        Assert.Same(test.Call,call);var value=test.Expected;
        return wrong switch
        {
            0=>value with{BuildReply=value.BuildReply with{Declarations=value.BuildReply.Declarations+1}},1=>value with{CacheBytesRetained=!value.CacheBytesRetained},
            2=>value with{Disposition="historical-cache-consumed"},3=>value with{HistoricalProducer="native-relabelled"},4=>value with{DeclaredHistoricalRecords=10},
            5=>value with{HistoricalContext="{}"},6=>value with{NativeDeclarations=999},7=>value with{HistoricalRecordsConsumed=true},8=>value with{HistoricalIdentityClaimed=true},_=>value
        };
    }
}
[Trait("Gate","Mock")]
public sealed class CatalogBuildCacheMockTests
{
    [Theory,MemberData(nameof(CatalogBuildCacheCases.All),MemberType=typeof(CatalogBuildCacheCases))]
    public void CorrectRawExtractionAuditRepliesPass(CatalogBuildCacheCase test)=>CatalogBuildCacheCases.AssertCase(test,new ScriptedCatalogBuildCache(test));
    [Theory,MemberData(nameof(CatalogBuildCacheCases.All),MemberType=typeof(CatalogBuildCacheCases))]
    public void EveryCacheDispositionContextCountAndProducerMutationRejected(CatalogBuildCacheCase test)
    {for(var wrong=0;wrong<9;wrong++) Assert.NotNull(Record.Exception(()=>CatalogBuildCacheCases.AssertCase(test,new ScriptedCatalogBuildCache(test,wrong))));}
}
