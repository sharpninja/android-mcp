using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class RecordingFactCanonical(IReadOnlyList<string> hashes) : IProfiledCanonicalJson
{
    public List<(JsonNode Input, JsonSerializationProfile Profile)> Calls { get; } = [];
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
        var index = Calls.Count; Calls.Add((JsonNode.Parse(value.GetRawText())!, profile));
        Assert.True(index < hashes.Count, "Unexpected canonical identity request");
        return hashes[index];
    }
}

public sealed class ScriptedSourceFacts(SourceFactCase test, RecordingFactCanonical canonical, JsonNode? response = null,
    bool mutateInput = false, bool omitIdentity = false, bool wrongProfile = false) : ICallbackSourceFacts, IFunctionalTaxonomySourceFacts, IInterfaceApplicabilitySourceFacts
{
    public List<FactOperation> Calls { get; } = [];
    private JsonNode Invoke(FactOperation operation, JsonObject? input = null)
    {
        Calls.Add(operation);
        if (mutateInput && input is not null) input["unexpected-source-edit"] = true;
        if (!omitIdentity)
            foreach (var identity in test.IdentityInputs ?? [])
            {
                using var document = JsonDocument.Parse(identity.ToJsonString());
                canonical.Hash(document.RootElement, wrongProfile ? JsonSerializationProfile.Utf8Canonical : JsonSerializationProfile.LegacyAsciiIdentity);
            }
        return (response ?? test.Expected).DeepClone();
    }
    public JsonObject TypeCandidate(JsonObject declaration) => Invoke(FactOperation.TypeCandidate, declaration).AsObject();
    public JsonObject EndpointMethod(JsonObject declaration, JsonObject? owner = null) => Invoke(FactOperation.Endpoint, declaration).AsObject();
    public JsonArray RegistrationCandidates(JsonObject declaration, IReadOnlyDictionary<string, JsonObject> resolvedTypes) => Invoke(FactOperation.Registration, declaration).AsArray();
    public JsonArray PossiblePairs(JsonArray candidates) => Invoke(FactOperation.Pairs).AsArray();
    public JsonObject Taxonomy(JsonObject declaration) => Invoke(FactOperation.Taxonomy, declaration).AsObject();
    public JsonObject Produce(JsonObject source, JsonObject linkage, JsonArray? hidlHashes = null) => Invoke(FactOperation.Applicability, source).AsObject();
}

public static class FactCanonicalAssertions
{
    public static void AssertCalls(SourceFactCase test, RecordingFactCanonical canonical)
    {
        var expected = test.IdentityInputs ?? [];
        Assert.Equal(expected.Count, canonical.Calls.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(JsonSerializationProfile.LegacyAsciiIdentity, canonical.Calls[i].Profile);
            Assert.True(JsonNode.DeepEquals(expected[i], canonical.Calls[i].Input), "Candidate identity key fields changed or type was guessed");
        }
    }
}

[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SourceFactMockGateTests
{
    public static IEnumerable<object[]> IdentityCases => SourceFactContractCases.All.Where(x => (x.IdentityInputs?.Count ?? 0) > 0).Select(x => new object[] { x.Id });
    public static IEnumerable<object[]> MutableInputCases => SourceFactContractCases.All.Where(x => x.Operation != FactOperation.Pairs).Select(x => new object[] { x.Id });

    [Theory, MemberData(nameof(SourceFactContractCases.Cases), MemberType = typeof(SourceFactContractCases))]
    public void CorrectFactsExerciseOutputInputAndCanonicalBoundaryAssertions(string id)
    {
        var test = SourceFactContractCases.Get(id); var canonical = new RecordingFactCanonical(test.IdentityHashes ?? []);
        var producer = new ScriptedSourceFacts(test, canonical);
        SourceFactContractCases.AssertContract(test, producer, producer, producer);
        Assert.Equal(test.Operation, Assert.Single(producer.Calls));
        FactCanonicalAssertions.AssertCalls(test, canonical);
    }

    [Theory, MemberData(nameof(SourceFactContractCases.Mutations), MemberType = typeof(SourceFactContractCases))]
    public void EveryFactPropertyRejectsItsOwnWrongAndOmittedMock(string id, SourceMutation mutation)
    {
        var test = SourceFactContractCases.Get(id); var mutated = SourceMutationCases.Apply(test.Extraction(), mutation);
        var canonical = new RecordingFactCanonical(test.IdentityHashes ?? []);
        // An omitted wrapper property is represented by an actual null result,
        // so it cannot silently fall back to the correct scripted response.
        var output = mutated.Records[0].ContainsKey("output") ? mutated.Records[0]["output"] : null;
        if (output is null)
        {
            var adapter = new ScriptedSourceExtractor("source-facts", _ => mutated);
            Assert.NotNull(Record.Exception(() => SourceExtractionContractCases.AssertContract(test.Extraction(), adapter)));
            return;
        }
        var producer = new ScriptedSourceFacts(test, canonical, output);
        Assert.NotNull(Record.Exception(() => SourceFactContractCases.AssertContract(test, producer, producer, producer)));
    }

    [Theory, MemberData(nameof(IdentityCases))]
    public void MissingOrWrongProfileCanonicalInteractionsAreRejected(string id)
    {
        var test = SourceFactContractCases.Get(id);
        foreach (var (omit, wrong) in new[] { (true, false), (false, true) })
        {
            var canonical = new RecordingFactCanonical(test.IdentityHashes ?? []);
            var producer = new ScriptedSourceFacts(test, canonical, omitIdentity: omit, wrongProfile: wrong);
            SourceFactContractCases.AssertContract(test, producer, producer, producer);
            Assert.NotNull(Record.Exception(() => FactCanonicalAssertions.AssertCalls(test, canonical)));
        }
    }

    [Theory, MemberData(nameof(MutableInputCases))]
    public void InputMutationIsRejectedWhilePreservingSharedFixture(string id)
    {
        var original = SourceFactContractCases.Get(id);
        var test = original with { Input = (JsonObject)original.Input.DeepClone() };
        var canonical = new RecordingFactCanonical(test.IdentityHashes ?? []);
        var producer = new ScriptedSourceFacts(test, canonical, mutateInput: true);
        Assert.NotNull(Record.Exception(() => SourceFactContractCases.AssertContract(test, producer, producer, producer)));
        Assert.False(original.Input.ContainsKey("unexpected-source-edit"));
    }

    [Theory]
    [InlineData("build")]
    [InlineData("aidl-hash")]
    [InlineData("hidl-hash")]
    public void SameRevisionFailuresHaveDiscriminatingTypedMocks(string mismatch)
    {
        var source = SourceFactContractCases.EvidenceSource(); var linkage = SourceFactContractCases.Linkage();
        JsonArray? hashes = null;
        if (mismatch == "build") linkage["build_evidence"]!["revision"] = "other";
        else if (mismatch == "aidl-hash") linkage["published_hash_lines"]![0]!["source"]!["revision"] = "other";
        else hashes = new(new JsonObject { ["source"] = new JsonObject { ["repository"] = "r", ["revision"] = "other" }, ["literal"] = "opaque" });
        AssertMismatch(() => throw new ArgumentException("Proof and declaration must share repository/revision"));
        Assert.NotNull(Record.Exception(() => AssertMismatch(() => new JsonObject())));
        Assert.NotNull(Record.Exception(() => AssertMismatch(() => throw new FormatException("repository/revision"))));
        Assert.NotNull(Record.Exception(() => AssertMismatch(() => throw new ArgumentException("unrelated"))));
        Assert.Equal("c", source["revision"]!.GetValue<string>());
        Assert.NotNull(linkage); Assert.True(mismatch != "hidl-hash" || hashes is not null);
    }

    internal static void AssertMismatch(Func<JsonObject> invoke)
    {
        var error = Assert.Throws<ArgumentException>(invoke);
        Assert.Contains("repository/revision", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceFactFixtureDenominatorAndIndependentIdentityPinsStayDistinct()
    {
        Assert.Equal(33, SourceFactContractCases.All.Count);
        Assert.Equal(22, SourceFactContractCases.All.Count(x => x.Id.StartsWith("FACT-CB", StringComparison.Ordinal)));
        Assert.Equal(7, SourceFactContractCases.All.Count(x => x.Id.StartsWith("FACT-FN", StringComparison.Ordinal)));
        Assert.Equal(4, SourceFactContractCases.All.Count(x => x.Id.StartsWith("FACT-IF", StringComparison.Ordinal)));
        Assert.Equal(6, new[] { SourceFactContractCases.RegistrationHash, SourceFactContractCases.OverloadHash, SourceFactContractCases.PositionHash,
            SourceFactContractCases.UnresolvedHash, SourceFactContractCases.UnicodeHash, SourceFactContractCases.BinderHash }.Distinct(StringComparer.Ordinal).Count());
    }
}
