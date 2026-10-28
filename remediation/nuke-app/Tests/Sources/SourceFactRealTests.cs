using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class AuditedRealFactCanonical(IProfiledCanonicalJson actual) : IProfiledCanonicalJson, IFactCanonicalTrace
{
    private readonly List<(JsonNode Input, JsonSerializationProfile Profile)> calls = [];
    public IReadOnlyList<(JsonNode Input, JsonSerializationProfile Profile)> Calls => calls;
    public byte[] Serialize(ReadOnlyMemory<byte> rawUtf8) => throw new InvalidOperationException("Unprofiled serialization was not requested");
    public byte[] Serialize(JsonElement value) => throw new InvalidOperationException("Unprofiled serialization was not requested");
    public string Hash(ReadOnlyMemory<byte> rawUtf8) => throw new InvalidOperationException("Unprofiled identity hashing is forbidden");
    public string Hash(JsonElement value) => throw new InvalidOperationException("Unprofiled identity hashing is forbidden");
    public byte[] Serialize(ReadOnlyMemory<byte> rawUtf8, JsonSerializationProfile profile) => throw new InvalidOperationException("Identity producer must use canonical Hash");
    public byte[] Serialize(JsonElement value, JsonSerializationProfile profile) => throw new InvalidOperationException("Identity producer must use canonical Hash");
    public string Hash(ReadOnlyMemory<byte> rawUtf8, JsonSerializationProfile profile)
    { using var document = JsonDocument.Parse(rawUtf8); return Hash(document.RootElement, profile); }
    public string Hash(JsonElement value, JsonSerializationProfile profile)
    {
        calls.Add((JsonNode.Parse(value.GetRawText())!, profile));
        return actual.Hash(value, profile);
    }
}

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SourceFactRealTests
{
    [Theory, MemberData(nameof(SourceFactContractCases.Cases), MemberType = typeof(SourceFactContractCases))]
    public void RealFactsAndRealLegacyIdentityPassUnchangedAssertions(string id)
    {
        var test = SourceFactContractCases.Get(id);
        var canonical = new AuditedRealFactCanonical(new CanonicalJson());
        var producer = new SourceFactProducer(canonical);
        SourceFactContractCases.AssertContract(test, producer, producer, producer);
        FactCanonicalAssertions.AssertCalls(test, canonical);
    }

    [Theory, MemberData(nameof(SourceFactProofFailureCases.Cases), MemberType = typeof(SourceFactProofFailureCases))]
    public void RealProofFailuresUseUnchangedActualSeamAssertions(string id)
    {
        var canonical = new AuditedRealFactCanonical(new CanonicalJson());
        SourceFactProofFailureCases.AssertContract(SourceFactProofFailureCases.Create(id), new SourceFactProducer(canonical));
        Assert.Empty(canonical.Calls);
    }
}
