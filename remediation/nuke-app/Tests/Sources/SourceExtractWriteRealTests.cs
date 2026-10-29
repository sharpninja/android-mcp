using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
public sealed class SourceExtractWriteRealTests
{
    [Theory, MemberData(nameof(SourceExtractWriteContractCases.Cases), MemberType = typeof(SourceExtractWriteContractCases))]
    public Task NativeWriterUsesSameSixteenSelectionAliasSealContextAndCommitAssertions(string id) =>
        SourceExtractWriteContractCases.AssertContract(SourceExtractWriteContractCases.Get(id),
            new SourceExtractWriteProbe((_, rig) => new SourcesExtractService(rig.Raw, rig.Generations, rig.Runtimes, rig.Cache, new CanonicalJson(), rig.Leases)));
}
