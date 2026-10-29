using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class ScriptedSoongSourceFacts : ISoongSourceFacts
{
    public Func<string,SoongModuleFamily,JsonArray>? ModulesResponse { get; init; }
    public Func<JsonObject,string?,JsonNode?>? LiteralResponse { get; init; }
    public Func<string,IReadOnlyList<string>,IReadOnlyList<string>,IReadOnlyList<string>,IReadOnlyList<string>>? MatchResponse { get; init; }
    public Func<string,JsonObject,string?>? ManifestResponse { get; init; }
    public List<string> Calls { get; } = [];
    public JsonArray Modules(string text, SoongModuleFamily family) { Calls.Add("modules:" + family + ":" + text); return ModulesResponse!(text, family); }
    public JsonNode? Literal(JsonObject node, string? property = null) { Calls.Add("literal:" + property); return LiteralResponse!(node, property); }
    public IReadOnlyList<string> MatchSources(string buildPath, IReadOnlyList<string> patterns, IReadOnlyList<string> excluded, IReadOnlyList<string> declaredPaths) { Calls.Add("match:" + buildPath); return MatchResponse!(buildPath, patterns, excluded, declaredPaths); }
    public string? ExplicitManifestPath(string buildPath, JsonObject apexModule) { Calls.Add("manifest:" + buildPath); return ManifestResponse!(buildPath, apexModule); }
}
[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SoongMockGateTests
{
    [Theory, MemberData(nameof(SoongContractCases.Cases), MemberType = typeof(SoongContractCases))]
    public void CorrectStaticBuildMocksPassSameFutureRealAssertions(string id)
    {
        var test = SoongContractCases.Get(id); var fake = new ScriptedSoongSourceFacts { ModulesResponse = (_, _) => (JsonArray)test.Expected.DeepClone() };
        SoongContractCases.AssertContract(test, fake); Assert.Equal("modules:" + test.Family + ":" + test.Input, Assert.Single(fake.Calls));
    }
    [Theory, MemberData(nameof(SoongContractCases.Mutations), MemberType = typeof(SoongContractCases))]
    public void EveryBuildFactPropertyRejectsWrongAndMissing(string id, SourceMutation mutation)
    {
        var test = SoongContractCases.Get(id); var wrong = SourceMutationCases.Apply(test.MutationCase(), mutation).Records;
        Assert.NotNull(Record.Exception(() => SoongContractCases.AssertContract(test, new ScriptedSoongSourceFacts { ModulesResponse = (_, _) => new JsonArray(wrong.Select(x => (JsonNode)x).ToArray()) })));
    }
    [Theory, MemberData(nameof(SoongContractCases.Cases), MemberType = typeof(SoongContractCases))]
    public void ModuleDenominatorOrderAndUnexpectedFailureAreDiscriminated(string id)
    {
        var test = SoongContractCases.Get(id); var controls = new List<JsonArray>();
        var extra = (JsonArray)test.Expected.DeepClone(); extra.Add(new JsonObject { ["kind"] = "invented" }); controls.Add(extra);
        if (test.Expected.Count > 0) controls.Add(new JsonArray(test.Expected.Skip(1).Select(x => x?.DeepClone()).ToArray()));
        if (test.Expected.Count > 1) controls.Add(new JsonArray(test.Expected.Reverse().Select(x => x?.DeepClone()).ToArray()));
        foreach (var wrong in controls) Assert.NotNull(Record.Exception(() => SoongContractCases.AssertContract(test, new ScriptedSoongSourceFacts { ModulesResponse = (_, _) => wrong })));
        Assert.NotNull(Record.Exception(() => SoongContractCases.AssertContract(test, new ScriptedSoongSourceFacts { ModulesResponse = (_, _) => throw new IOException("scripted failure") })));
    }
    [Theory, MemberData(nameof(SoongAuxiliaryCases.LiteralCases), MemberType = typeof(SoongAuxiliaryCases))]
    public void LiteralMocksDiscriminateValuesUnknownsAndMutation(string id)
    {
        var test = SoongAuxiliaryCases.Literals.Single(x => x.Id == id); var fake = new ScriptedSoongSourceFacts { LiteralResponse = (_, _) => test.Expected?.DeepClone() };
        SoongAuxiliaryCases.AssertLiteral(test, fake); Assert.Equal("literal:" + test.Property, Assert.Single(fake.Calls));
        Assert.NotNull(Record.Exception(() => SoongAuxiliaryCases.AssertLiteral(test, new ScriptedSoongSourceFacts { LiteralResponse = (_, _) => JsonValue.Create("invented") })));
        foreach (var wrong in new JsonNode?[] { null, JsonValue.Create(false), JsonValue.Create(0) }.Where(x => !JsonNode.DeepEquals(x, test.Expected)))
            Assert.NotNull(Record.Exception(() => SoongAuxiliaryCases.AssertLiteral(test, new ScriptedSoongSourceFacts { LiteralResponse = (_, _) => wrong })));
        Assert.NotNull(Record.Exception(() => SoongAuxiliaryCases.AssertLiteral(test, new ScriptedSoongSourceFacts { LiteralResponse = (node, _) => { node["kind"] = "invented"; return test.Expected?.DeepClone(); } })));
    }
    [Theory, MemberData(nameof(SoongAuxiliaryCases.MatchCases), MemberType = typeof(SoongAuxiliaryCases))]
    public void SourceSelectionMocksProveActualSafeAndRejectedSeams(string id)
    {
        var test = SoongAuxiliaryCases.Matches.Single(x => x.Id == id);
        var fake = new ScriptedSoongSourceFacts { MatchResponse = (_, _, _, _) => test.Failure is null ? test.Expected! : throw new ArgumentException(test.Failure + " fixture rejection") };
        SoongAuxiliaryCases.AssertMatch(test, fake); Assert.Single(fake.Calls);
        var wrong = test.Failure is null ? new ScriptedSoongSourceFacts { MatchResponse = (_, _, _, _) => ["invented"] } : new ScriptedSoongSourceFacts { MatchResponse = (_, _, _, _) => [] };
        Assert.NotNull(Record.Exception(() => SoongAuxiliaryCases.AssertMatch(test, wrong)));
        if (test.Expected is { Length: > 1 }) Assert.NotNull(Record.Exception(() => SoongAuxiliaryCases.AssertMatch(test, new ScriptedSoongSourceFacts { MatchResponse = (_, _, _, _) => test.Expected.Reverse().ToArray() })));
        foreach (var input in new[] { "patterns", "excluded", "paths" })
        {
            var selected = input == "patterns" ? test.Patterns : input == "excluded" ? test.Excluded : test.Paths;
            if (selected.Length == 0) continue;
            var mutating = new ScriptedSoongSourceFacts { MatchResponse = (_, patterns, excluded, paths) =>
            { var array = (string[])(input == "patterns" ? patterns : input == "excluded" ? excluded : paths); array[0] = "invented"; return test.Failure is null ? test.Expected! : throw new ArgumentException(test.Failure); } };
            Assert.NotNull(Record.Exception(() => SoongAuxiliaryCases.AssertMatch(test, mutating)));
        }
        if (test.Failure is not null)
        {
            Assert.NotNull(Record.Exception(() => SoongAuxiliaryCases.AssertMatch(test, new ScriptedSoongSourceFacts { MatchResponse = (_, _, _, _) => throw new IOException("wrong type") })));
            Assert.NotNull(Record.Exception(() => SoongAuxiliaryCases.AssertMatch(test, new ScriptedSoongSourceFacts { MatchResponse = (_, _, _, _) => throw new ArgumentException("unrelated") })));
        }
    }
    [Theory, MemberData(nameof(SoongAuxiliaryCases.ManifestCases), MemberType = typeof(SoongAuxiliaryCases))]
    public void ExplicitManifestMocksDoNotInferDefaultsOrUnsafePaths(string id)
    {
        var test = SoongAuxiliaryCases.Manifests.Single(x => x.Id == id); var fake = new ScriptedSoongSourceFacts { ManifestResponse = (_, _) => test.Expected };
        SoongAuxiliaryCases.AssertManifest(test, fake); Assert.Single(fake.Calls);
        Assert.NotNull(Record.Exception(() => SoongAuxiliaryCases.AssertManifest(test, new ScriptedSoongSourceFacts { ManifestResponse = (_, _) => "invented.json" })));
        Assert.NotNull(Record.Exception(() => SoongAuxiliaryCases.AssertManifest(test, new ScriptedSoongSourceFacts { ManifestResponse = (_, module) => { module["raw"] = "invented"; return test.Expected; } })));
    }
}
