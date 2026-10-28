using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class PublishedInterfaceHashRealTests
{
    [Theory, MemberData(nameof(PublishedInterfaceHashContractCases.Cases), MemberType = typeof(PublishedInterfaceHashContractCases))]
    public void NativePublishedHashFactsUseTheSameLiteralAndUnknownAssertions(string id) =>
        PublishedInterfaceHashContractCases.AssertContract(PublishedInterfaceHashContractCases.Get(id), new PublishedInterfaceHashSourceFacts());
}

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class ApexManifestRealTests
{
    [Theory, MemberData(nameof(ApexManifestContractCases.Cases), MemberType = typeof(ApexManifestContractCases))]
    public void NativeApexLiteralsKeepTheSameUnknownVersionsAndDuplicateDiagnostics(string id) =>
        ApexManifestContractCases.AssertContract(ApexManifestContractCases.Get(id), new ApexManifestSourceFacts());
}

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class NativeAbiRealTests
{
    [Theory, MemberData(nameof(NativeAbiContractCases.Cases), MemberType = typeof(NativeAbiContractCases))]
    public void NativeAbiFactsKeepTheSameOpaqueMetadataAndConservativeGraphAssertions(string id) =>
        NativeAbiContractCases.AssertContract(NativeAbiContractCases.Get(id), new NativeAbiSourceFacts());
}
