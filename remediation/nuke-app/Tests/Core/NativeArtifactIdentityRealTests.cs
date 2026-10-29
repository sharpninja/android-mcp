using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

// Production field mapper over controlled native syscall results. SAME29 case
// data and assertions, including high bits, epoch boundaries and borrowed-handle
// disposal. Does NOT execute native syscalls or prove actual Windows/Linux IDs.
[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-007")]
public sealed class NativeArtifactIdentityRealTests
{
    [Theory,MemberData(nameof(NativeArtifactIdentityCases.Cases),MemberType=typeof(NativeArtifactIdentityCases))]
    public void SameCasesWithProductionMapper(NativeIdentityCase c)=>
        NativeArtifactIdentityCases.AssertCase(c,calls=>new NativeArtifactIdentityReader(calls));
}
