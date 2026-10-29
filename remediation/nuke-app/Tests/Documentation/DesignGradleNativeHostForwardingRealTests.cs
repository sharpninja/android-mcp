using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DesignGradleNativeHostForwardingRealTests
{
    [Theory,MemberData(nameof(DesignGradleProjectionCases.Cases),MemberType=typeof(DesignGradleProjectionCases))]
    public void SameIndependentHostForwardingCasesBindNativeProjection(DesignGradleProjectionCase test)=>
        DesignGradleNativeHostForwardingCases.AssertProjection(test,new NativeDesignGradleProjection(new CanonicalJson(),new ArtifactReceiptVerifier(new NativeContentHash())));
}
