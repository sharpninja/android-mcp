using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DesignGradleCliPlanningRealTests
{
    [Theory,MemberData(nameof(DesignGradleCliPlanningCases.Cases),MemberType=typeof(DesignGradleCliPlanningCases))]
    public void SameCategoryParametersBindRootOwnedRegistry(DesignGradleCliPlanningCase test)=>
        DesignGradleCliPlanningCases.AssertPlan(test,new OperationPlanner(new OperationRegistry()));
    [Fact]
    public void SamePolicyMetadataAndScriptAssertionsBindExistingProjection()
    {
        var test=DesignGradleCliPlanningCases.ProjectionCase();
        DesignGradleProjectionCases.AssertProjection(test,new NativeDesignGradleProjection(new CanonicalJson(),new ArtifactReceiptVerifier(new NativeContentHash())));
    }
}
