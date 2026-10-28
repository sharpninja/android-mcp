using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class ScriptedNativeAbi(Func<string,JsonObject> response) : INativeAbiSourceFacts
{
    public List<string> Calls { get; } = [];
    public JsonObject Parse(string text) { Calls.Add(text); return response(text); }
}
[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class NativeAbiMockGateTests
{
    [Theory, MemberData(nameof(NativeAbiContractCases.Cases), MemberType = typeof(NativeAbiContractCases))]
    public void CorrectNativeAbiMocksPassSameFutureRealAssertions(string id)
    {
        var test = NativeAbiContractCases.Get(id); var fake = new ScriptedNativeAbi(_ => test.CorrectResult());
        NativeAbiContractCases.AssertContract(test, fake); Assert.Equal(test.Input, Assert.Single(fake.Calls));
    }
    [Theory, MemberData(nameof(NativeAbiContractCases.Mutations), MemberType = typeof(NativeAbiContractCases))]
    public void EveryNativeMetadataPropertyRejectsWrongAndMissing(string id, SourceMutation mutation)
    {
        var test = NativeAbiContractCases.Get(id); var wrong = SourceMutationCases.Apply(test.MutationCase(), mutation).Records[0];
        if (test.Diagnostic) wrong["reason"] = "Fixture diagnostic";
        Assert.NotNull(Record.Exception(() => NativeAbiContractCases.AssertContract(test, new ScriptedNativeAbi(_ => wrong))));
    }
    [Theory, MemberData(nameof(NativeAbiContractCases.Cases), MemberType = typeof(NativeAbiContractCases))]
    public void InventedWireAuthorityAndProviderFailuresAreRejected(string id)
    {
        var test = NativeAbiContractCases.Get(id); var wrong = test.CorrectResult(); wrong["wire_codec"] = "invented";
        Assert.NotNull(Record.Exception(() => NativeAbiContractCases.AssertContract(test, new ScriptedNativeAbi(_ => wrong))));
        Assert.NotNull(Record.Exception(() => NativeAbiContractCases.AssertContract(test, new ScriptedNativeAbi(_ => throw new IOException("scripted failure")))));
        if (test.Diagnostic)
            foreach (var reason in new string?[] { null, "", " " })
            { var missing = test.CorrectResult(); if (reason is null) missing.Remove("reason"); else missing["reason"] = reason; Assert.NotNull(Record.Exception(() => NativeAbiContractCases.AssertContract(test, new ScriptedNativeAbi(_ => missing)))); }
    }
}
