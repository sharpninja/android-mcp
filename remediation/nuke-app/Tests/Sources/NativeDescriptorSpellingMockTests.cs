using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Mock"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class NativeDescriptorSpellingMockTests
{
    private sealed class Scripted(JsonObject output) : INativeDescriptorSourceFacts { public JsonObject Parse(string text, string language) => (JsonObject)output.DeepClone(); }
    [Theory, MemberData(nameof(NativeDescriptorSpellingContractCases.Cases), MemberType = typeof(NativeDescriptorSpellingContractCases))]
    public void OriginalSpellingExpectedValuesRejectAllOutputMutations(string id)
    {
        var test = NativeDescriptorSpellingContractCases.Get(id); NativeDescriptorContractCases.AssertContract(test, new Scripted(test.Expected));
        foreach (var item in SourceMutationCases.For(test.MutationCase()))
        {
            var wrong = SourceMutationCases.Apply(test.MutationCase(), (SourceMutation)item[1]);
            Assert.NotNull(Record.Exception(() => NativeDescriptorContractCases.AssertContract(test, new Scripted(wrong.Records[0]))));
        }
    }
}
