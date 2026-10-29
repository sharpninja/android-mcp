using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SourceProducerEffectRealTests
{
    [Theory, MemberData(nameof(SourceProducerEffectContractCases.Cases), MemberType = typeof(SourceProducerEffectContractCases))]
    public void SameOriginalIdentityEffectThenReachedErrorBindsNative(string id)
    {
        var test = SourceProducerEffectContractCases.Get(id); var canonical = new AuditedRealFactCanonical(new CanonicalJson());
        SourceProducerEffectContractCases.AssertContract(test, new SourceFactProducer(canonical), canonical);
    }
}
