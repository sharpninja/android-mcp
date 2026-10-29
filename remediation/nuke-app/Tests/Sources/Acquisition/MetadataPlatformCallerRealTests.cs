using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

[Trait("Gate","Real")]
public sealed class MetadataPlatformCallerRealTests
{
    [Theory,MemberData(nameof(MetadataPlatformCallerCases.All),MemberType=typeof(MetadataPlatformCallerCases))]
    public Task SameCallerCasesUseSoleProjectionAndSharedAcquisition(MetadataPlatformCallerCase c)
        =>MetadataPlatformCallerCases.AssertCase(c,new NativeMetadataPlatformCallerProbe());
}
