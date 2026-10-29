using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
public sealed class SourcesFetchPacingCliRealTests
{
    [Fact]
    public Task ExistingGenericSnapshotAndReplayProbeForwardsZeroDelay()
        =>SourcesFetchPacingCliCases.AssertCase(new NativeSourcesFetchCliProbe(SourcesFetchAliasCliCases.Request(),0));
}
