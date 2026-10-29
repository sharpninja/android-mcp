using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Mock"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SourceProducerSpellingMockTests
{
    [Theory, MemberData(nameof(SourceProducerSpellingContractCases.Cases), MemberType = typeof(SourceProducerSpellingContractCases))]
    public void SameSourceSpellingClausesRejectEveryWrongOutputProperty(string id)
    {
        var test = SourceProducerSpellingContractCases.Get(id); var mock = new ScriptedSourceFacts(test, new RecordingFactCanonical(test.IdentityHashes ?? []));
        SourceFactContractCases.AssertContract(test, mock, mock, mock);
        foreach (var raw in SourceMutationCases.For(test.Extraction()))
        {
            var wrong = SourceMutationCases.Apply(test.Extraction(), (SourceMutation)raw[1]); var output = wrong.Records[0]["output"];
            if (output is null) { var omitted = new ScriptedSourceExtractor("source-facts", _ => wrong); Assert.NotNull(Record.Exception(() => SourceExtractionContractCases.AssertContract(test.Extraction(), omitted))); continue; }
            var changed = new ScriptedSourceFacts(test, new RecordingFactCanonical(test.IdentityHashes ?? []), output);
            Assert.NotNull(Record.Exception(() => SourceFactContractCases.AssertContract(test, changed, changed, changed)));
        }
    }
}
