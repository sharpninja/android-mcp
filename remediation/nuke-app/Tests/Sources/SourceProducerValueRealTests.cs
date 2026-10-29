using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SourceProducerValueRealTests
{
    [Theory, MemberData(nameof(SourceProducerValueContractCases.Cases), MemberType = typeof(SourceProducerValueContractCases))]
    public void SameValueCasesBindOneNativeProducerAuthority(string id)
    {
        var test = SourceProducerValueContractCases.Get(id); var canonical = new AuditedRealFactCanonical(new CanonicalJson());
        SourceProducerValueContractCases.AssertContract(test, new SourceFactProducer(canonical));
        if (test.Error is null) FactCanonicalAssertions.AssertCalls(test.Contract, canonical); else Assert.Empty(canonical.Calls);
    }
    [Theory, InlineData("release011"), InlineData("current-candidate")]
    public void SameValueCasesBindExistingSharedRuntimeConcern(string profile)
    {
        using var runtime = new SourcesRuntimeFactory().Create(profile == "release011" ? SourceExtractionProfile.RELEASE011 : SourceExtractionProfile.CurrentCandidate);
        var producer = Assert.IsAssignableFrom<ISourceFactValueProducer>(runtime.Concerns.Callbacks);
        foreach (var test in SourceProducerValueContractCases.All) SourceProducerValueContractCases.AssertContract(test, producer);
    }
}
