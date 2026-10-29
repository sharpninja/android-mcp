using System.Text.Json.Nodes;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Mock"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SourceProducerEffectMockTests
{
    [Theory, MemberData(nameof(SourceProducerEffectContractCases.Cases), MemberType = typeof(SourceProducerEffectContractCases))]
    public void OriginalHashBeforeReachedErrorAssertionsRejectMissingWrongOrMutatingMocks(string id)
    {
        var test = SourceProducerEffectContractCases.Get(id); var canonical = new RecordingFactCanonical(test.IdentityHashes!);
        SourceProducerEffectContractCases.AssertContract(test, new ScriptedSourceEffect(test, canonical), canonical);
        foreach (var mode in new[] { "omit", "profile", "mutate" })
        {
            var copy = test with { Input = (JsonObject)test.Input.DeepClone() }; var wrong = new RecordingFactCanonical(test.IdentityHashes!);
            Assert.NotNull(Record.Exception(() => SourceProducerEffectContractCases.AssertContract(copy, new ScriptedSourceEffect(copy, wrong, omitHash: mode == "omit", wrongProfile: mode == "profile", mutate: mode == "mutate"), wrong)));
        }
    }
}
