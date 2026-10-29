using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class ScriptedApexManifest(Func<string,JsonObject> response) : IApexManifestSourceFacts
{
    public List<string> Calls { get; } = [];
    public JsonObject Manifest(string text) { Calls.Add(text); return response(text); }
}
[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class ApexManifestMockGateTests
{
    [Theory, MemberData(nameof(ApexManifestContractCases.Cases), MemberType = typeof(ApexManifestContractCases))]
    public void CorrectApexSourceMocksPassExactFutureRealAssertions(string id)
    {
        var test = ApexManifestContractCases.Get(id); var fake = new ScriptedApexManifest(_ => test.CorrectResult());
        ApexManifestContractCases.AssertContract(test, fake); Assert.Equal(test.Input, Assert.Single(fake.Calls));
    }
    [Theory, MemberData(nameof(ApexManifestContractCases.Mutations), MemberType = typeof(ApexManifestContractCases))]
    public void EveryApexSourcePropertyRejectsWrongAndMissing(string id, SourceMutation mutation)
    {
        var test = ApexManifestContractCases.Get(id); var wrong = SourceMutationCases.Apply(test.MutationCase(), mutation).Records[0];
        if (test.Diagnostic) wrong["reason"] = "Fixture diagnostic";
        Assert.NotNull(Record.Exception(() => ApexManifestContractCases.AssertContract(test, new ScriptedApexManifest(_ => wrong))));
    }
    [Theory, MemberData(nameof(ApexManifestContractCases.Cases), MemberType = typeof(ApexManifestContractCases))]
    public void InstalledAuthorityAndUnexpectedFailureAreRejected(string id)
    {
        var test = ApexManifestContractCases.Get(id); var wrong = test.CorrectResult(); wrong["installed_version"] = 1;
        Assert.NotNull(Record.Exception(() => ApexManifestContractCases.AssertContract(test, new ScriptedApexManifest(_ => wrong))));
        Assert.NotNull(Record.Exception(() => ApexManifestContractCases.AssertContract(test, new ScriptedApexManifest(_ => throw new IOException("scripted failure")))));
        if (test.Diagnostic)
            foreach (var reason in new string?[] { null, "", " " })
            { var missing = test.CorrectResult(); if (reason is null) missing.Remove("reason"); else missing["reason"] = reason; Assert.NotNull(Record.Exception(() => ApexManifestContractCases.AssertContract(test, new ScriptedApexManifest(_ => missing)))); }
    }
}
