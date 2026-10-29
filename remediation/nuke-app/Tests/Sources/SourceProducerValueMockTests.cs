using System.Text.Json.Nodes;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Mock"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SourceProducerValueMockTests
{
    [Theory, MemberData(nameof(SourceProducerValueContractCases.Cases), MemberType = typeof(SourceProducerValueContractCases))]
    public void SameValueOutputErrorAndImmutableInputAssertionsRejectWrongMocks(string id)
    {
        var test = SourceProducerValueContractCases.Get(id); var canonical = new RecordingFactCanonical(test.Contract.IdentityHashes ?? []);
        SourceProducerValueContractCases.AssertContract(test, new ScriptedSourceValueProducer(test, canonical));
        if (test.Error is null) FactCanonicalAssertions.AssertCalls(test.Contract, canonical); else Assert.Empty(canonical.Calls);
        Assert.NotNull(Record.Exception(() => SourceProducerValueContractCases.AssertContract(test, new ScriptedSourceValueProducer(test, new RecordingFactCanonical([]), wrong: true))));
        if (test.Input is JsonArray or JsonObject)
        {
            var copy = test with { Input = test.Input.DeepClone() };
            Assert.NotNull(Record.Exception(() => SourceProducerValueContractCases.AssertContract(copy, new ScriptedSourceValueProducer(copy, new RecordingFactCanonical(copy.Contract.IdentityHashes ?? []), mutate: true))));
        }
    }
}
