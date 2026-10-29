using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
public sealed class SourceBinaryDispatcherRealTests
{
    [Theory, MemberData(nameof(SourceBinaryDispatcherContractCases.Cases), MemberType = typeof(SourceBinaryDispatcherContractCases))]
    public void SameDeclaredBinaryInputsAndInteractionsUseTheSingleNativeDispatcher(string id) => SourceBinaryDispatcherContractCases.AssertContract(
        SourceBinaryDispatcherContractCases.Get(id), (provider, profile) => new SourcesExtractionDispatcher([provider], profile));
}
