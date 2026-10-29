using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record CallbackPairFailure(string Id, string Field, JsonArray Rows);

// Original callback_source_facts.py is the contract. Reuse the existing
// independent full-output/identity goldens, with only the stated clause delta.
public static class CallbackProducerCompletionContractCases
{
    public static IReadOnlyList<SourceFactCase> All { get; } = Create();
    private static SourceFactCase Registration(string id, string baseline, bool empty, bool spelled = true)
    {
        var original = SourceFactContractCases.Get(baseline);
        var expected = original.Expected.DeepClone();
        expected[0]!["confidence"] = empty ? "name-heuristic-unresolved-type" : "source-structure-plus-role-heuristic";
        expected[0]!["evidence_reasons"] = new JsonArray("callback-like parameter/type spelling");
        var types = new Dictionary<string, JsonObject>(StringComparer.Ordinal)
        { [SourceFactContractCases.TypeB] = empty ? new() : new() { ["name"] = "Ordinary", ["kind"] = "class" } };
        return original with
        {
            Id = id, Input = (JsonObject)original.Input.DeepClone(), Expected = spelled ? expected : new JsonArray(), Types = types,
            IdentityInputs = spelled ? original.IdentityInputs : [], IdentityHashes = spelled ? original.IdentityHashes : [],
            Evidence = "registration_candidates: empty dict is falsey, nonempty ordinary type is truthy; immutable literal identity remains unchanged"
        };
    }
    private static IReadOnlyList<SourceFactCase> Create()
    {
        var result = new List<SourceFactCase>
        {
            Registration("CB-COMP01", "FACT-CB06", true), Registration("CB-COMP02", "FACT-CB17", true, false),
            Registration("CB-COMP03", "FACT-CB06", false), Registration("CB-COMP04", "FACT-CB20", true)
        };
        var pair = SourceFactContractCases.Get("FACT-CB18");
        var nullContext = (JsonArray)pair.Additional!.DeepClone();
        foreach (var row in nullContext) row!["owner"] = null;
        result.Add(pair with { Id = "CB-COMP05", Additional = nullContext, Evidence = "possible_pairs preserves explicit null context equality, distinct from absent keys" });
        result.Add(pair with { Id = "CB-COMP06", Expected = new JsonArray(), Additional = new JsonArray(
            new JsonObject { ["role"] = "registration-candidate", ["owner"] = "a.A" }, new JsonObject { ["role"] = "unregistration-candidate", ["owner"] = "b.B" }),
            Evidence = "possible_pairs compares owner first and does not read later missing fields after mismatch" });
        result.Add(pair with { Id = "CB-COMP07", Expected = new JsonArray(), Additional = new JsonArray(new JsonObject { ["role"] = "callback-parameter-candidate" }),
            Evidence = "Nonregistration rows require no unused context or candidate identity" });
        result.Add(pair with { Id = "CB-COMP08", Expected = new JsonArray(), Additional = new JsonArray(new JsonObject { ["role"] = "registration-candidate" }),
            Evidence = "A registration without any removal does not access unused context fields" });
        return result;
    }
    public static IReadOnlyList<CallbackPairFailure> Failures { get; } = CreateFailures();
    private static IReadOnlyList<CallbackPairFailure> CreateFailures()
    {
        var result = new List<CallbackPairFailure> { new("CB-ERR-role", "role", new JsonArray(new JsonObject())) };
        foreach (var field in new[] { "owner", "language", "type_spelling", "type_id", "candidate_id" })
            foreach (var index in new[] { 0, 1 })
            {
                var rows = (JsonArray)SourceFactContractCases.Get("FACT-CB18").Additional!.DeepClone(); rows[index]!.AsObject().Remove(field);
                result.Add(new("CB-ERR-" + field + "-" + index, field, rows));
            }
        return result;
    }
    public static IEnumerable<object[]> Cases => All.Select(test => new object[] { test.Id });
    public static IEnumerable<object[]> Mutations => All.SelectMany(test => SourceMutationCases.For(test.Extraction()));
    public static IEnumerable<object[]> FailureCases => Failures.Select(test => new object[] { test.Id });
    public static SourceFactCase Get(string id) => All.Single(test => test.Id == id);
    public static CallbackPairFailure GetFailure(string id) => Failures.Single(test => test.Id == id);
    public static void AssertFailure(CallbackPairFailure test, ICallbackSourceFacts provider)
    {
        var before = test.Rows.DeepClone();
        var error = Assert.Throws<ArgumentException>(() => provider.PossiblePairs(test.Rows));
        Assert.Contains("Required source fact field: " + test.Field, error.Message, StringComparison.Ordinal);
        Assert.True(JsonNode.DeepEquals(before, test.Rows), "Rejected pairing changed caller input");
    }
}
