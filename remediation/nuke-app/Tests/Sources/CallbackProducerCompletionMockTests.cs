using System.Text.Json.Nodes;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Mock"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class CallbackProducerCompletionMockTests
{
    [Theory, MemberData(nameof(CallbackProducerCompletionContractCases.Cases), MemberType = typeof(CallbackProducerCompletionContractCases))]
    public void CorrectMocksPassSameOutputInputAndIdentityAssertions(string id)
    {
        var test = CallbackProducerCompletionContractCases.Get(id); var canonical = new RecordingFactCanonical(test.IdentityHashes ?? []);
        var provider = new ScriptedSourceFacts(test, canonical);
        SourceFactContractCases.AssertContract(test, provider, provider, provider); FactCanonicalAssertions.AssertCalls(test, canonical);
        Assert.Equal(test.Operation, Assert.Single(provider.Calls));
    }
    [Theory, MemberData(nameof(CallbackProducerCompletionContractCases.Mutations), MemberType = typeof(CallbackProducerCompletionContractCases))]
    public void EveryAssertedOutputPropertyRejectsItsOwnWrongOrMissingValue(string id, SourceMutation mutation)
    {
        var test = CallbackProducerCompletionContractCases.Get(id); var wrong = SourceMutationCases.Apply(test.Extraction(), mutation);
        if (wrong.Records[0]["output"] is not { } output)
        {
            var adapter = new ScriptedSourceExtractor("source-facts", _ => wrong);
            Assert.NotNull(Record.Exception(() => SourceExtractionContractCases.AssertContract(test.Extraction(), adapter))); return;
        }
        var provider = new ScriptedSourceFacts(test, new RecordingFactCanonical(test.IdentityHashes ?? []), output);
        Assert.NotNull(Record.Exception(() => SourceFactContractCases.AssertContract(test, provider, provider, provider)));
    }
    [Theory, MemberData(nameof(CallbackProducerCompletionContractCases.Cases), MemberType = typeof(CallbackProducerCompletionContractCases))]
    public void InputMutationAndProviderFailureAreRejected(string id)
    {
        var original = CallbackProducerCompletionContractCases.Get(id);
        var test = original with { Input = (JsonObject)original.Input.DeepClone(), Additional = (JsonArray?)original.Additional?.DeepClone() };
        foreach (var provider in new[] {
            new ScriptedSourceFacts(test, new RecordingFactCanonical(test.IdentityHashes ?? []), mutateInput: true),
            new ScriptedSourceFacts(test, new RecordingFactCanonical(test.IdentityHashes ?? []), failure: new IOException("fixture")) })
            Assert.NotNull(Record.Exception(() => SourceFactContractCases.AssertContract(test, provider, provider, provider)));
    }
    [Theory, MemberData(nameof(CallbackProducerCompletionContractCases.Cases), MemberType = typeof(CallbackProducerCompletionContractCases))]
    public void IdentityRequestsCannotBeOmittedReprofiledOrChanged(string id)
    {
        var test = CallbackProducerCompletionContractCases.Get(id);
        if ((test.IdentityInputs?.Count ?? 0) == 0) return;
        foreach (var omit in new[] { true, false })
        {
            var canonical = new RecordingFactCanonical(test.IdentityHashes ?? []);
            var provider = new ScriptedSourceFacts(test, canonical, omitIdentity: omit, wrongProfile: !omit);
            SourceFactContractCases.AssertContract(test, provider, provider, provider);
            Assert.NotNull(Record.Exception(() => FactCanonicalAssertions.AssertCalls(test, canonical)));
        }
        foreach (var key in test.IdentityInputs![0].Select(item => item.Key))
        {
            var canonical = new RecordingFactCanonical(test.IdentityHashes ?? []);
            var provider = new ScriptedSourceFacts(test, canonical, identityMutation: value => value.Remove(key));
            SourceFactContractCases.AssertContract(test, provider, provider, provider);
            Assert.NotNull(Record.Exception(() => FactCanonicalAssertions.AssertCalls(test, canonical)));
        }
    }
    [Theory, MemberData(nameof(CallbackProducerCompletionContractCases.FailureCases), MemberType = typeof(CallbackProducerCompletionContractCases))]
    public void ExactFailureFieldAndImmutableInputAssertionsRejectIncorrectMocks(string id)
    {
        var test = CallbackProducerCompletionContractCases.GetFailure(id);
        var context = new SourceFactCase(test.Id, FactOperation.Pairs, new(), new JsonArray(), "Original indexed-key failure", Additional: test.Rows);
        CallbackProducerCompletionContractCases.AssertFailure(test, new ScriptedSourceFacts(context, new RecordingFactCanonical([]), failure: new ArgumentException("Required source fact field: " + test.Field)));
        foreach (var failure in new Exception?[] { null, new FormatException("Required source fact field: " + test.Field), new ArgumentException("wrong field") })
            Assert.NotNull(Record.Exception(() => CallbackProducerCompletionContractCases.AssertFailure(test, new ScriptedSourceFacts(context, new RecordingFactCanonical([]), failure: failure))));
        var changed = test with { Rows = (JsonArray)test.Rows.DeepClone() };
        var mutatingContext = context with { Additional = changed.Rows };
        Assert.NotNull(Record.Exception(() => CallbackProducerCompletionContractCases.AssertFailure(changed,
            new ScriptedSourceFacts(mutatingContext, new RecordingFactCanonical([]), mutateInput: true, failure: new ArgumentException("Required source fact field: " + test.Field)))));
    }
}
