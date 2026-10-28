using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
public sealed class SourceExtractSelectionRealTests
{
    [Theory, MemberData(nameof(SourceExtractSelectionContractCases.Cases), MemberType = typeof(SourceExtractSelectionContractCases))]
    public Task NativeWriterHonorsSamePhysicalSelectionAndLogicalCacheLimitAssertions(string id) =>
        SourceExtractSelectionContractCases.AssertContract(id, new SourceExtractSelectionProbe((_, rig) =>
            new SourcesExtractService(rig.Raw, rig.Generations, rig.Runtimes, rig.Cache, new CanonicalJson(), rig.Leases)));
}
