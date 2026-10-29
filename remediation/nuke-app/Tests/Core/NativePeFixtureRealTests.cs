using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed class NativePeFixtureRealTests
{
    [Fact]
    public void Same_stable_PE_contract_binds_the_shared_actual_fixture_builder()
    {
        var first=NativeFactsFixture.Pe(PeFixtureCases.Module);
        var repeated=NativeFactsFixture.Pe(PeFixtureCases.Module);
        using var firstPe=new PEReader(new MemoryStream(first,false));
        using var repeatedPe=new PEReader(new MemoryStream(repeated,false));
        var firstMetadata=firstPe.GetMetadataReader();var repeatedMetadata=repeatedPe.GetMetadataReader();
        PeFixtureCases.AssertContract(new(ContentHash.Sha256(first),ContentHash.Sha256(repeated),
            firstMetadata.GetGuid(firstMetadata.GetModuleDefinition().Mvid),repeatedMetadata.GetGuid(repeatedMetadata.GetModuleDefinition().Mvid),
            firstPe.PEHeaders.CoffHeader.TimeDateStamp,repeatedPe.PEHeaders.CoffHeader.TimeDateStamp,first.Length,repeated.Length));
    }
}
