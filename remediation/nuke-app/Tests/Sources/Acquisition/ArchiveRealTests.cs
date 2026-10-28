using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;
[Trait("Gate","Real")]
public sealed class ArchiveRealTests
{
    [Fact]public Task SameInventoryAndBoundedReadAssertions()=>ArchiveCases.AssertCase(new MetadataArchiveReader(),new(ArchiveCases.ZipFixture()));
    [Fact]public Task SameNativeArchiveAssertions()=>NativeArchiveCases.AssertCase(new NativeArchiveExtractor(),NativeArchiveCases.Fixture(),new());
}
