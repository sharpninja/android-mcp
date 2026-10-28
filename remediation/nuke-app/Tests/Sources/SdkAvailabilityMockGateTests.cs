using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources;
using Xunit;
using Xunit.Sdk;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class ScriptedSdkAvailabilityFacts(Func<ReadOnlyMemory<byte>, SdkAvailabilityEvidence> response) : ISdkAvailabilitySourceFacts
{
    public List<ReadOnlyMemory<byte>> Calls { get; } = [];
    public SdkAvailabilityEvidence Enumerate(ReadOnlyMemory<byte> xml) { Calls.Add(xml); return response(xml); }
}

[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SdkAvailabilityMockGateTests
{
    [Theory, MemberData(nameof(SdkAvailabilityContractCases.Cases), MemberType = typeof(SdkAvailabilityContractCases))]
    public void CorrectMocksProveExactFutureRawAvailabilityAssertions(string id)
    {
        var test = SdkAvailabilityContractCases.Get(id); var fake = new ScriptedSdkAvailabilityFacts(_ => test.Correct());
        SdkAvailabilityContractCases.AssertContract(test, fake); Assert.Single(fake.Calls);
    }
    [Theory, MemberData(nameof(SdkAvailabilityContractCases.Mutations), MemberType = typeof(SdkAvailabilityContractCases))]
    public void EveryRawOwnerMemberSdkAttributeAndArrayMutantIsRejected(string id, SourceMutation mutation)
    {
        var test = SdkAvailabilityContractCases.Get(id); var wrong = SourceMutationCases.Apply(test.MutationCase(), mutation).Records[0];
        Assert.ThrowsAny<XunitException>(() => SdkAvailabilityContractCases.AssertContract(test, new ScriptedSdkAvailabilityFacts(_ => new SdkAvailabilityEvidence(
            Objects(wrong["rows"])!, Objects(wrong["sdks"])!))));
    }
    [Theory, MemberData(nameof(SdkAvailabilityContractCases.Cases), MemberType = typeof(SdkAvailabilityContractCases))]
    public void ProviderFailureAndMutatedSourceBytesAreRejected(string id)
    {
        var test = SdkAvailabilityContractCases.Get(id);
        Assert.Throws<IOException>(() => SdkAvailabilityContractCases.AssertContract(test, new ScriptedSdkAvailabilityFacts(_ => throw new IOException("scripted failure"))));
        Assert.ThrowsAny<XunitException>(() => SdkAvailabilityContractCases.AssertContract(test, new ScriptedSdkAvailabilityFacts(bytes =>
        { Assert.True(MemoryMarshal.TryGetArray(bytes, out var input)); input.Array![input.Offset] ^= 1; return test.Correct(); })));
    }
    [Theory, MemberData(nameof(SdkAvailabilityContractCases.Cases), MemberType = typeof(SdkAvailabilityContractCases))]
    public void MissingDuplicatedReorderedAndInventedFactsAreRejected(string id)
    {
        var test = SdkAvailabilityContractCases.Get(id); var correct = test.Correct();
        var wrong = new List<SdkAvailabilityEvidence>
        {
            correct with { Records = correct.Records.Concat([new JsonObject { ["invented"] = true }]).ToArray() },
            correct with { SdkDefinitions = correct.SdkDefinitions.Concat([new JsonObject { ["invented"] = true }]).ToArray() }
        };
        if (correct.Records.Count > 0)
        {
            wrong.Add(correct with { Records = correct.Records.Skip(1).ToArray() });
            wrong.Add(correct with { Records = correct.Records.Concat([correct.Records[0]]).ToArray() });
            if (!JsonNode.DeepEquals(test.Expected()["rows"], new JsonArray(correct.Records.Reverse().Select(x => (JsonNode)x.DeepClone()).ToArray())))
                wrong.Add(correct with { Records = correct.Records.Reverse().ToArray() });
        }
        if (correct.SdkDefinitions.Count > 0)
        {
            wrong.Add(correct with { SdkDefinitions = correct.SdkDefinitions.Skip(1).ToArray() });
            wrong.Add(correct with { SdkDefinitions = correct.SdkDefinitions.Concat([correct.SdkDefinitions[0]]).ToArray() });
        }
        foreach (var response in wrong)
            Assert.ThrowsAny<XunitException>(() => SdkAvailabilityContractCases.AssertContract(test, new ScriptedSdkAvailabilityFacts(_ => response)));
    }
    [Theory, MemberData(nameof(SdkAvailabilityContractCases.FailureCases), MemberType = typeof(SdkAvailabilityContractCases))]
    public void TypedLegacyFailureMocksAndWrongExceptionControlsUseActualBoundary(string id)
    {
        var test = SdkAvailabilityContractCases.Failures.Single(x => x.Id == id);
        SdkAvailabilityContractCases.AssertFailure(test, new ScriptedSdkAvailabilityFacts(_ => throw new FormatException(test.Message)));
        foreach (var wrong in new[] { new ScriptedSdkAvailabilityFacts(_ => new SdkAvailabilityEvidence([], [])), new ScriptedSdkAvailabilityFacts(_ => throw new IOException("wrong")), new ScriptedSdkAvailabilityFacts(_ => throw new FormatException("wrong reason")) })
            Assert.ThrowsAny<XunitException>(() => SdkAvailabilityContractCases.AssertFailure(test, wrong));
    }
    private static JsonObject[]? Objects(JsonNode? node) => node is JsonArray array && array.All(x => x is JsonObject) ? array.Cast<JsonObject>().ToArray() : null;
}
