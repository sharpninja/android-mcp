using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class ArtifactGenerationPrecommitHookRealTests
{
    [Theory,MemberData(nameof(ArtifactGenerationPrecommitHookCases.Data),MemberType=typeof(ArtifactGenerationPrecommitHookCases))]
    public Task NativeRuntimeBindsTheSameSixHookScopeCases(PrecommitHookScope scope)
    {
        var e=new PrecommitHookEnvironment(scope);
        return ArtifactGenerationPrecommitHookCases.AssertContract(e,new ArtifactGenerationRuntime(e.Source,e.Target,e.Leases,e.Receipts,e.Manifests,"fixture-root"));
    }
}
