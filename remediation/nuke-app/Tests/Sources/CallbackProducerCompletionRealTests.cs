using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class CallbackProducerCompletionRealTests
{
    [Theory, MemberData(nameof(CallbackProducerCompletionContractCases.Cases), MemberType = typeof(CallbackProducerCompletionContractCases))]
    public void SameOriginalClauseCasesUseExistingProducerAndCanonicalIdentity(string id)
    {
        var test = CallbackProducerCompletionContractCases.Get(id); var canonical = new AuditedRealFactCanonical(new CanonicalJson());
        var provider = new SourceFactProducer(canonical);
        SourceFactContractCases.AssertContract(test, provider, provider, provider); FactCanonicalAssertions.AssertCalls(test, canonical);
    }
    [Theory, MemberData(nameof(CallbackProducerCompletionContractCases.FailureCases), MemberType = typeof(CallbackProducerCompletionContractCases))]
    public void SameIndexedFieldFailuresUseExistingProducer(string id)
    {
        var canonical = new AuditedRealFactCanonical(new CanonicalJson());
        CallbackProducerCompletionContractCases.AssertFailure(CallbackProducerCompletionContractCases.GetFailure(id), new SourceFactProducer(canonical));
        Assert.Empty(canonical.Calls);
    }
    [Theory, InlineData("release011"), InlineData("current-candidate")]
    public void SamePureHelperClausesUseTheSharedProfileRuntime(string profile)
    {
        using var runtime = new SourcesRuntimeFactory().Create(profile == "release011" ? SourceExtractionProfile.RELEASE011 : SourceExtractionProfile.CurrentCandidate);
        foreach (var test in CallbackProducerCompletionContractCases.All)
            SourceFactContractCases.AssertContract(test, runtime.Concerns.Callbacks, runtime.Concerns.Taxonomy, runtime.Concerns.Applicability);
    }
}
