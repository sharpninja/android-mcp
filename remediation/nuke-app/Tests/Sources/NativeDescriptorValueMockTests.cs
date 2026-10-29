using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Mock"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class NativeDescriptorValueMockTests
{
    [Theory, MemberData(nameof(NativeDescriptorValueContractCases.Cases), MemberType = typeof(NativeDescriptorValueContractCases))]
    public void OriginalValueDomainAssertionsDiscriminateIndependentMocks(string id)
    {
        var test = NativeDescriptorValueContractCases.Get(id);
        NativeDescriptorValueContractCases.AssertContract(test, new ScriptedDescriptorValue(test));
        Assert.NotNull(Record.Exception(() => NativeDescriptorValueContractCases.AssertContract(test, new ScriptedDescriptorValue(test, wrong: true))));
        if (test.Text is System.Text.Json.Nodes.JsonArray or System.Text.Json.Nodes.JsonObject)
        {
            var copy = test with { Text = test.Text.DeepClone() };
            Assert.NotNull(Record.Exception(() => NativeDescriptorValueContractCases.AssertContract(copy, new ScriptedDescriptorValue(test, mutate: true))));
        }
    }
}
