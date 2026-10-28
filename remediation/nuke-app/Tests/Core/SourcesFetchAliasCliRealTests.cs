using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004")]
public sealed class SourcesFetchAliasCliRealTests
{
    [Fact]
    public Task SameGenericAliasAndSealedResumeAssertions() =>
        SourcesFetchAliasCliCases.AssertCase(new NativeSourcesFetchCliProbe(SourcesFetchAliasCliCases.Request()));
}
