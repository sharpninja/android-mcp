using System.Text.Json.Nodes;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Mock"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SourceProducerDomainMockTests
{
    [Theory, MemberData(nameof(SourceProducerDomainContractCases.Cases), MemberType = typeof(SourceProducerDomainContractCases))]
    public void SameFullOutputAndInputAssertionsDiscriminateScriptedResponses(string id)
    {
        var test = SourceProducerDomainContractCases.Get(id);
        var mock = new ScriptedSourceFacts(test, new RecordingFactCanonical(test.IdentityHashes ?? []));
        SourceFactContractCases.AssertContract(test, mock, mock, mock);
        foreach (var mutation in SourceMutationCases.For(test.Extraction()))
        {
            var wrong = SourceMutationCases.Apply(test.Extraction(), (SourceMutation)mutation[1]);
            var output = wrong.Records[0]["output"];
            if (output is null)
            {
                var omitted = new ScriptedSourceExtractor("source-facts", _ => wrong);
                Assert.NotNull(Record.Exception(() => SourceExtractionContractCases.AssertContract(test.Extraction(), omitted))); continue;
            }
            var changed = new ScriptedSourceFacts(test, new RecordingFactCanonical(test.IdentityHashes ?? []), output);
            Assert.NotNull(Record.Exception(() => SourceFactContractCases.AssertContract(test, changed, changed, changed)));
        }
        var copied = test with { Input = (JsonObject)test.Input.DeepClone(), Additional = (JsonArray?)test.Additional?.DeepClone() };
        var mutating = new ScriptedSourceFacts(copied, new RecordingFactCanonical(test.IdentityHashes ?? []), mutateInput: true);
        Assert.NotNull(Record.Exception(() => SourceFactContractCases.AssertContract(copied, mutating, mutating, mutating)));
    }
    [Theory, MemberData(nameof(SourceProducerDomainContractCases.ErrorCases), MemberType = typeof(SourceProducerDomainContractCases))]
    public void ReachedErrorAssertionsRejectAcceptanceWrongErrorsAndMutation(string id)
    {
        var test = SourceProducerDomainContractCases.GetFailure(id);
        var correct = new ScriptedSourceFacts(test.Case, new RecordingFactCanonical([]), failure: new ArgumentException(test.Error));
        SourceProducerDomainContractCases.AssertFailure(test, correct, correct, correct);
        foreach (var error in new Exception?[] { null, new FormatException(test.Error), new ArgumentException("wrong operation") })
        {
            var wrong = new ScriptedSourceFacts(test.Case, new RecordingFactCanonical(test.Case.IdentityHashes ?? []), failure: error);
            Assert.NotNull(Record.Exception(() => SourceProducerDomainContractCases.AssertFailure(test, wrong, wrong, wrong)));
        }
        var copy = test with { Case = test.Case with { Input = (JsonObject)test.Case.Input.DeepClone() } };
        var mutating = new ScriptedSourceFacts(copy.Case, new RecordingFactCanonical([]), mutateInput: true, failure: new ArgumentException(test.Error));
        Assert.NotNull(Record.Exception(() => SourceProducerDomainContractCases.AssertFailure(copy, mutating, mutating, mutating)));
    }
}
