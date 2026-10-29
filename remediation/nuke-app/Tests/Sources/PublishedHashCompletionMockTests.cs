using System.Text.Json.Nodes;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Mock"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class PublishedHashCompletionMockTests
{
    [Theory, MemberData(nameof(PublishedHashCompletionContractCases.Cases), MemberType = typeof(PublishedHashCompletionContractCases))]
    public void IndependentFullOutputsDiscriminateBothAdmissionDirections(string id)
    {
        var test = PublishedHashCompletionContractCases.Get(id);
        var fake = new ScriptedPublishedHashes((_, _) => (JsonObject)test.Expected.DeepClone());
        PublishedHashCompletionContractCases.AssertCase(test, fake); Assert.Equal((test.Format, test.Input), Assert.Single(fake.Calls));
        var opposite = (JsonObject)test.Expected.DeepClone();
        if (opposite["facts"]!.AsArray().Count > 0) opposite["facts"] = new JsonArray();
        else opposite["facts"] = new JsonArray(new JsonObject { ["literal"] = "invented" });
        Assert.NotNull(Record.Exception(() => PublishedHashCompletionContractCases.AssertCase(test, new ScriptedPublishedHashes((_, _) => opposite))));
        var wrongRaw = (JsonObject)test.Expected.DeepClone();
        (wrongRaw["facts"]!.AsArray().Count > 0 ? wrongRaw["facts"] : wrongRaw["unknown_lines"])![0]!["raw"] = "changed";
        Assert.NotNull(Record.Exception(() => PublishedHashCompletionContractCases.AssertCase(test, new ScriptedPublishedHashes((_, _) => wrongRaw))));
    }
    [Theory, MemberData(nameof(PublishedHashCompletionContractCases.OriginalMethods), MemberType = typeof(PublishedHashCompletionContractCases))]
    public void ExactOriginalFourMethodsDiscriminateEveryOriginalAssertion(int method)
    {
        var test = PublishedHashCompletionContractCases.OriginalFixture(method);
        PublishedHashCompletionContractCases.AssertOriginal(method, new ScriptedPublishedHashes((_, _) => (JsonObject)test.Expected.DeepClone()));
        foreach (var wrong in PublishedHashCompletionContractCases.WrongOriginalResults(method))
            Assert.NotNull(Record.Exception(() => PublishedHashCompletionContractCases.AssertOriginal(method, new ScriptedPublishedHashes((_, _) => wrong))));
    }
}
