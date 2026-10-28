using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;
public sealed class PaxOverrideRealProbe:IPaxOverrideProbe
{
    private readonly MemoryAcquisitionStore store=new();public int Writes=>store.Writes;
    public Task ExecuteAsync(PaxOverrideCase test,byte[] fixture)=>new NativeArchiveExtractor().ExtractTarGZipAsync(fixture,store,"native");
}
[Trait("Gate","Real")]
public sealed class PaxHistoricalRealTests
{
    [Fact]public Task SameExactGlobalPaxMetadataAssertions()=>PaxArchiveCases.AssertCase(new NativeArchiveExtractor());
    [Theory,MemberData(nameof(PaxOverrideCases.All),MemberType=typeof(PaxOverrideCases))]
    public Task SameAdversarialGlobalOverrideAssertions(PaxOverrideCase test)=>PaxOverrideCases.AssertCase(test,new PaxOverrideRealProbe());
    [Theory,MemberData(nameof(HistoricalCacheCases.All),MemberType=typeof(HistoricalCacheCases))]
    public Task SameHistoricalCacheIdentityCases(HistoricalCacheCase test)
    {
        var store=new MemoryAcquisitionStore();store.Files[test.Pin.Path]=new(test.Pin.Path,HistoricalCacheCases.GZipFixture(),"application/gzip");
        return HistoricalCacheCases.AssertCase(test,new HistoricalSourceCache(store,new CanonicalJson()));
    }
}
