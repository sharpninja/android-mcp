using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SourceProducerDomainRealTests
{
    [Theory, MemberData(nameof(SourceProducerDomainContractCases.Cases), MemberType = typeof(SourceProducerDomainContractCases))]
    public void SameOriginalOutputsInputsAndIdentityRequestsBindExistingProducer(string id)
    {
        var test = SourceProducerDomainContractCases.Get(id); var canonical = new AuditedRealFactCanonical(new CanonicalJson()); var producer = new SourceFactProducer(canonical);
        SourceFactContractCases.AssertContract(test, producer, producer, producer); FactCanonicalAssertions.AssertCalls(test, canonical);
    }
    [Theory, MemberData(nameof(SourceProducerDomainContractCases.ErrorCases), MemberType = typeof(SourceProducerDomainContractCases))]
    public void SameReachedOriginalErrorsBindExistingProducer(string id)
    {
        var canonical = new AuditedRealFactCanonical(new CanonicalJson()); var producer = new SourceFactProducer(canonical);
        SourceProducerDomainContractCases.AssertFailure(SourceProducerDomainContractCases.GetFailure(id), producer, producer, producer);
        Assert.Empty(canonical.Calls);
    }
    [Theory, InlineData("release011"), InlineData("current-candidate")]
    public void SameDomainCasesUseExistingProfileConcerns(string profile)
    {
        using var runtime = new SourcesRuntimeFactory().Create(profile == "release011" ? SourceExtractionProfile.RELEASE011 : SourceExtractionProfile.CurrentCandidate);
        foreach (var test in SourceProducerDomainContractCases.All) SourceFactContractCases.AssertContract(test, runtime.Concerns.Callbacks, runtime.Concerns.Taxonomy, runtime.Concerns.Applicability);
        foreach (var test in SourceProducerDomainContractCases.Failures) SourceProducerDomainContractCases.AssertFailure(test, runtime.Concerns.Callbacks, runtime.Concerns.Taxonomy, runtime.Concerns.Applicability);
    }
}
