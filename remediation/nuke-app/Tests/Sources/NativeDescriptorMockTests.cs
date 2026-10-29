using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class ScriptedNativeDescriptors(Func<string, string, JsonObject> response) : INativeDescriptorSourceFacts
{
    public List<(string Text, string Language)> Calls { get; } = [];
    public JsonObject Parse(string text, string language) { Calls.Add((text, language)); return response(text, language); }
}

[Trait("Gate", "Mock"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class NativeDescriptorMockTests
{
    [Fact]
    public void RuntimeCapabilityAssertionsRejectMissingOrUnusableConcerns()
    {
        var test = NativeDescriptorContractCases.Get("ND-01");
        NativeDescriptorContractCases.AssertCapability(() => new ScriptedNativeDescriptors((_, _) => (JsonObject)test.Expected.DeepClone()));
        Assert.NotNull(Record.Exception(() => NativeDescriptorContractCases.AssertCapability(() => null)));
        Assert.NotNull(Record.Exception(() => NativeDescriptorContractCases.AssertCapability(() => new ScriptedNativeDescriptors((_, _) => new()))));
    }
    [Theory, MemberData(nameof(NativeDescriptorContractCases.Cases), MemberType = typeof(NativeDescriptorContractCases))]
    public void CorrectMocksPassSameFutureNativeAssertions(string id)
    {
        var test = NativeDescriptorContractCases.Get(id); var mock = new ScriptedNativeDescriptors((_, _) => (JsonObject)test.Expected.DeepClone());
        NativeDescriptorContractCases.AssertContract(test, mock); Assert.Equal((test.Text, test.Language), Assert.Single(mock.Calls));
    }
    [Theory, MemberData(nameof(NativeDescriptorContractCases.Mutations), MemberType = typeof(NativeDescriptorContractCases))]
    public void EachAssertedPropertyRejectsOmissionAndWrongValue(string id, SourceMutation mutation)
    {
        var test = NativeDescriptorContractCases.Get(id); var wrong = SourceMutationCases.Apply(test.MutationCase(), mutation).Records[0];
        Assert.NotNull(Record.Exception(() => NativeDescriptorContractCases.AssertContract(test, new ScriptedNativeDescriptors((_, _) => wrong))));
    }
    [Theory, MemberData(nameof(NativeDescriptorContractCases.Cases), MemberType = typeof(NativeDescriptorContractCases))]
    public void ProviderFailureAndInventedAuthorityCannotPass(string id)
    {
        var test = NativeDescriptorContractCases.Get(id); var wrong = (JsonObject)test.Expected.DeepClone(); wrong["compiled_or_runtime_proven"] = true;
        Assert.NotNull(Record.Exception(() => NativeDescriptorContractCases.AssertContract(test, new ScriptedNativeDescriptors((_, _) => wrong))));
        Assert.NotNull(Record.Exception(() => NativeDescriptorContractCases.AssertContract(test, new ScriptedNativeDescriptors((_, _) => throw new IOException("fixture")))));
    }
    [Theory, InlineData(""), InlineData("c++"), InlineData("CPP"), InlineData("java")]
    public void ExactLanguageFailureAssertionsRejectSuccessWrongTypeAndWrongDiagnostic(string language)
    {
        NativeDescriptorContractCases.AssertInvalid(language, new ScriptedNativeDescriptors((_, _) => throw new ArgumentException("Explicit source language required")));
        foreach (var mock in new[] { new ScriptedNativeDescriptors((_, _) => new()), new ScriptedNativeDescriptors((_, _) => throw new FormatException("Explicit source language required")),
            new ScriptedNativeDescriptors((_, _) => throw new ArgumentException("wrong reason")) })
            Assert.NotNull(Record.Exception(() => NativeDescriptorContractCases.AssertInvalid(language, mock)));
    }
}
