using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class ScriptedSourceXmlFormatter(Func<JsonObject,string> response) : ISourceXmlFormatter
{
    public List<JsonObject> Calls { get; } = [];
    public string Serialize(JsonObject element) { Calls.Add(element); return response(element); }
}
[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SourceXmlMockGateTests
{
    [Theory, MemberData(nameof(SourceXmlContractCases.Cases), MemberType = typeof(SourceXmlContractCases))]
    public void CorrectLegacyXmlMocksPassExactFutureRealAssertions(string id)
    {
        var test = SourceXmlContractCases.Get(id); var fake = new ScriptedSourceXmlFormatter(_ => test.Expected);
        SourceXmlContractCases.AssertContract(test, fake); Assert.True(JsonNode.DeepEquals(test.Element, Assert.Single(fake.Calls)));
    }
    [Theory, MemberData(nameof(SourceXmlContractCases.Cases), MemberType = typeof(SourceXmlContractCases))]
    public void WrongXmlCharactersOrderInputMutationAndExceptionsAreRejected(string id)
    {
        var test = SourceXmlContractCases.Get(id);
        var wrong = new[] { "", test.Expected + "invented", test.Expected.Replace("<", "&lt;", StringComparison.Ordinal), test.Expected[..^1] };
        foreach (var value in wrong) Assert.NotNull(Record.Exception(() => SourceXmlContractCases.AssertContract(test, new ScriptedSourceXmlFormatter(_ => value))));
        Assert.NotNull(Record.Exception(() => SourceXmlContractCases.AssertContract(test, new ScriptedSourceXmlFormatter(element => { element["tag"] = "invented"; return test.Expected; }))));
        Assert.NotNull(Record.Exception(() => SourceXmlContractCases.AssertContract(test, new ScriptedSourceXmlFormatter(_ => throw new IOException("scripted failure")))));
    }
}
