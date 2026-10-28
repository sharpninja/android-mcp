using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SourceDispatcherRealTests
{
    [Theory, MemberData(nameof(SourceDispatcherContractCases.Cases), MemberType = typeof(SourceDispatcherContractCases))]
    public void NativeDispatcherUsesTheSameProfileRouteFailureAndInteractionAssertions(string id) =>
        SourceDispatcherContractCases.AssertContract(SourceDispatcherContractCases.Get(id), (extractors, profile) => new SourcesExtractionDispatcher(extractors, profile));
    [Theory]
    [InlineData("REG-duplicate")]
    [InlineData("REG-empty")]
    [InlineData("REG-null-profile")]
    public void NativeRegistrationUsesTheSameIsolatedPreconditionAssertions(string id) =>
        SourceDispatcherContractCases.AssertRegistrationFailure(id, (extractors, profile) => new SourcesExtractionDispatcher(extractors, profile));
}
