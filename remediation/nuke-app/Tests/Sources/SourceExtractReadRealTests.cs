using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
public sealed class SourceExtractReadRealTests
{
    [Theory, MemberData(nameof(SourceExtractReadContractCases.Cases), MemberType = typeof(SourceExtractReadContractCases))]
    public Task SameAdmissionLookupRawResolverAndOwnershipAssertionsUseNativeFacade(string id) => SourceExtractReadContractCases.AssertContract(SourceExtractReadContractCases.Get(id),
        new SourceExtractReadProbe((_, dependencies) => new SourcesExtractService(dependencies.Raw, dependencies.Generations, dependencies.Runtimes, dependencies.Cache, new CanonicalJson())));
}
