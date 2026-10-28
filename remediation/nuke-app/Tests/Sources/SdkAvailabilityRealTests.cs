using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
public sealed class SdkAvailabilityRealTests
{
    [Theory, MemberData(nameof(SdkAvailabilityContractCases.Cases), MemberType = typeof(SdkAvailabilityContractCases))]
    public void SameLiteralLifecycleCasesUseNativeFacts(string id) => SdkAvailabilityContractCases.AssertContract(SdkAvailabilityContractCases.Get(id), new SdkAvailabilitySourceFacts());
    [Theory, MemberData(nameof(SdkAvailabilityContractCases.FailureCases), MemberType = typeof(SdkAvailabilityContractCases))]
    public void SameMalformedLifecycleCasesUseNativeFacts(string id) => SdkAvailabilityContractCases.AssertFailure(SdkAvailabilityContractCases.Failures.Single(x => x.Id == id), new SdkAvailabilitySourceFacts());
}
