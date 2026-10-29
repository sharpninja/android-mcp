using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Mock"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SoongLexicalCompletionMockTests
{
    [Theory, MemberData(nameof(SoongLexicalCompletionContractCases.Cases), MemberType = typeof(SoongLexicalCompletionContractCases))]
    public void IndependentScalarFullOutputsPassSameFutureNativeAssertions(string id)
    {
        var test = SoongLexicalCompletionContractCases.Get(id);
        SoongContractCases.AssertContract(test, new ScalarMock((_, _) => (JsonArray)test.Expected.DeepClone()));
        var wrong = (JsonArray)test.Expected.DeepClone(); wrong[0]!["properties"]!["complete"] = !wrong[0]!["properties"]!["complete"]!.GetValue<bool>();
        Assert.NotNull(Record.Exception(() => SoongContractCases.AssertContract(test, new ScalarMock((_, _) => wrong))));
        wrong = (JsonArray)test.Expected.DeepClone(); wrong[0]!["raw"] = "changed";
        Assert.NotNull(Record.Exception(() => SoongContractCases.AssertContract(test, new ScalarMock((_, _) => wrong))));
        wrong = (JsonArray)test.Expected.DeepClone();
        if (wrong[0]!["properties"]!["items"]!.AsArray().Count > 0) wrong[0]!["properties"]!["items"]![0]!["value"]!["kind"] = "invented-value";
        else wrong[0]!["properties"]!["items"]!.AsArray().Add(new JsonObject());
        Assert.NotNull(Record.Exception(() => SoongContractCases.AssertContract(test, new ScalarMock((_, _) => wrong))));
    }
    private sealed class ScalarMock(Func<string, SoongModuleFamily, JsonArray> response) : ISoongSourceFacts
    {
        public JsonArray Modules(string text, SoongModuleFamily family) => response(text, family);
        public JsonNode? Literal(JsonObject node, string? property = null) => throw new InvalidOperationException("Unrequested literal call");
        public IReadOnlyList<string> MatchSources(string buildPath, IReadOnlyList<string> patterns, IReadOnlyList<string> excluded, IReadOnlyList<string> declaredPaths) => throw new InvalidOperationException("Unrequested glob call");
        public string? ExplicitManifestPath(string buildPath, JsonObject apexModule) => throw new InvalidOperationException("Unrequested path call");
    }
}
