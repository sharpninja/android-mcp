using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public static class SourceProducerEffectContractCases
{
    public static IReadOnlyList<SourceFactCase> All { get; } = Create();
    private static IReadOnlyList<SourceFactCase> Create()
    {
        var result = new List<SourceFactCase>();
        foreach (var parameter in new JsonNode?[] { null, JsonValue.Create("x"), JsonValue.Create(false), new JsonArray() })
        {
            var baseline = SourceFactContractCases.Get("FACT-CB06"); var test = baseline with { Id = "REGISTRATION-HASH-BEFORE-PARAMETER-ERROR-" + result.Count, Input = (JsonObject)baseline.Input.DeepClone() };
            test.Input["extensions"]!["parameters"]![0] = parameter; result.Add(test);
        }
        return result;
    }
    public static IEnumerable<object[]> Cases => All.Select(test => new object[] { test.Id });
    public static SourceFactCase Get(string id) => All.Single(test => test.Id == id);
    public static void AssertContract(SourceFactCase test, ICallbackSourceFacts producer, IFactCanonicalTrace canonical)
    {
        var before = test.Input.DeepClone(); var error = Assert.Throws<ArgumentException>(() => producer.RegistrationCandidates(test.Input, test.Types!));
        Assert.Contains("object", error.Message, StringComparison.Ordinal); FactCanonicalAssertions.AssertCalls(test, canonical);
        Assert.True(JsonNode.DeepEquals(before, test.Input));
    }
}
public sealed class ScriptedSourceEffect(SourceFactCase test, RecordingFactCanonical canonical, bool omitHash = false, bool wrongProfile = false, bool mutate = false) : ICallbackSourceFacts
{
    public JsonArray RegistrationCandidates(JsonObject declaration, IReadOnlyDictionary<string, JsonObject> types)
    {
        if (!omitHash) canonical.Hash(JsonSerializer.SerializeToElement(test.IdentityInputs![0]), wrongProfile ? JsonSerializationProfile.Utf8Canonical : JsonSerializationProfile.LegacyAsciiIdentity);
        if (mutate) declaration["unexpected-edit"] = true;
        throw new ArgumentException("Expected source fact object");
    }
    public JsonObject TypeCandidate(JsonObject declaration) => throw new InvalidOperationException();
    public JsonObject EndpointMethod(JsonObject declaration, JsonObject? owner = null) => throw new InvalidOperationException();
    public JsonArray PossiblePairs(JsonArray candidates) => throw new InvalidOperationException();
}
