using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DesignGradleProjectionRealTests
{
    private static IDesignGradleProjection Provider()=>new NativeDesignGradleProjection(new CanonicalJson(),new ArtifactReceiptVerifier(new NativeContentHash()));
    [Theory,MemberData(nameof(DesignGradleProjectionCases.Cases),MemberType=typeof(DesignGradleProjectionCases))]
    public void SameMetadataAndScriptAssertionsBindNativeProjection(DesignGradleProjectionCase test)=>DesignGradleProjectionCases.AssertProjection(test,Provider());
    [Theory,MemberData(nameof(DesignGradleProjectionCases.CheckCases),MemberType=typeof(DesignGradleProjectionCases))]
    public void SameRetainedConsistencyCasesBindNativeProjection(string id)=>DesignGradleProjectionCases.AssertCheck(DesignGradleProjectionCases.Original(),id,Provider());
}
