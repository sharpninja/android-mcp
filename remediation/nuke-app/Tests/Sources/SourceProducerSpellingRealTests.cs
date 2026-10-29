using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SourceProducerSpellingRealTests
{
    [Theory, MemberData(nameof(SourceProducerSpellingContractCases.Cases), MemberType = typeof(SourceProducerSpellingContractCases))]
    public void SameSpellingCasesBindExistingProducerAndSharedPrimitives(string id)
    {
        var test = SourceProducerSpellingContractCases.Get(id); var canonical = new AuditedRealFactCanonical(new CanonicalJson()); var producer = new SourceFactProducer(canonical);
        SourceFactContractCases.AssertContract(test, producer, producer, producer); FactCanonicalAssertions.AssertCalls(test, canonical);
    }
}
