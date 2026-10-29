using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class ScriptedPublishedHashes(Func<string,string,JsonObject> response) : IPublishedInterfaceHashSourceFacts
{
    public List<(string Format,string Text)> Calls { get; } = [];
    private JsonObject Invoke(string format, string text) { Calls.Add((format, text)); return response(format, text); }
    public JsonObject AidlHashes(string text) => Invoke("aidl", text);
    public JsonObject HidlHashes(string text) => Invoke("hidl", text);
}
[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class PublishedInterfaceHashMockGateTests
{
    [Theory, MemberData(nameof(PublishedInterfaceHashContractCases.Cases), MemberType = typeof(PublishedInterfaceHashContractCases))]
    public void CorrectPublishedHashMocksPassExactFutureRealAssertions(string id)
    {
        var test = PublishedInterfaceHashContractCases.Get(id); var fake = new ScriptedPublishedHashes((_, _) => (JsonObject)test.Expected.DeepClone());
        PublishedInterfaceHashContractCases.AssertContract(test, fake); Assert.Equal((test.Format, test.Input), Assert.Single(fake.Calls));
    }
    [Theory, MemberData(nameof(PublishedInterfaceHashContractCases.Mutations), MemberType = typeof(PublishedInterfaceHashContractCases))]
    public void EveryLiteralHashPropertyRejectsWrongAndMissing(string id, SourceMutation mutation)
    {
        var test = PublishedInterfaceHashContractCases.Get(id); var wrong = SourceMutationCases.Apply(test.MutationCase(), mutation).Records[0];
        Assert.NotNull(Record.Exception(() => PublishedInterfaceHashContractCases.AssertContract(test, new ScriptedPublishedHashes((_, _) => wrong))));
    }
    [Theory, MemberData(nameof(PublishedInterfaceHashContractCases.Cases), MemberType = typeof(PublishedInterfaceHashContractCases))]
    public void InventedRuntimeAuthorityAndUnexpectedFailureAreRejected(string id)
    {
        var test = PublishedInterfaceHashContractCases.Get(id); var wrong = (JsonObject)test.Expected.DeepClone(); wrong["runtime_hash"] = "invented";
        Assert.NotNull(Record.Exception(() => PublishedInterfaceHashContractCases.AssertContract(test, new ScriptedPublishedHashes((_, _) => wrong))));
        Assert.NotNull(Record.Exception(() => PublishedInterfaceHashContractCases.AssertContract(test, new ScriptedPublishedHashes((_, _) => throw new IOException("scripted failure")))));
    }
}
