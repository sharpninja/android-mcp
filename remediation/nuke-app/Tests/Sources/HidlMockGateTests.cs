using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class HidlMockGateTests
{
    [Theory, MemberData(nameof(HidlContractCases.Cases), MemberType = typeof(HidlContractCases))]
    public void CorrectHidlMocksExerciseTheSameFutureRealAssertions(string id)
    {
        var test = HidlContractCases.Get(id);
        var provider = new ScriptedSourceExtractor("hidl-source", _ => test.CorrectResult());
        HidlContractCases.AssertContract(test, provider);
        var source = Assert.Single(provider.Received);
        Assert.Equal(test.Source.Document().Context, source.Context);
        Assert.Equal(test.Source.Document().RelativePath, source.RelativePath);
    }

    [Theory, MemberData(nameof(HidlContractCases.RecordMutations), MemberType = typeof(HidlContractCases))]
    public void EveryHidlRecordPropertyDiscriminatesWrongAndMissingValues(string id, SourceMutation mutation)
    {
        var test = HidlContractCases.Get(id);
        var wrong = SourceMutationCases.Apply(test.Source, mutation) with { Metadata = (JsonObject)test.Metadata.DeepClone() };
        Assert.NotNull(Record.Exception(() => HidlContractCases.AssertContract(test, new ScriptedSourceExtractor("hidl-source", _ => wrong))));
    }

    [Theory, MemberData(nameof(HidlContractCases.MetadataMutations), MemberType = typeof(HidlContractCases))]
    public void EveryHidlSummaryPropertyDiscriminatesWrongAndMissingValues(string id, SourceMutation mutation)
    {
        var test = HidlContractCases.Get(id);
        var wrongSummary = SourceMutationCases.Apply(test.MetadataCase(), mutation).Records[0];
        var wrong = test.CorrectResult() with { Metadata = wrongSummary };
        Assert.NotNull(Record.Exception(() => HidlContractCases.AssertContract(test, new ScriptedSourceExtractor("hidl-source", _ => wrong))));
    }

    [Theory, MemberData(nameof(HidlContractCases.Cases), MemberType = typeof(HidlContractCases))]
    public void RecordCountsMetadataStatusAndUnsupportedIssuesCannotBeLost(string id)
    {
        var test = HidlContractCases.Get(id); var correct = test.CorrectResult();
        var controls = new List<ExtractionResult>
        {
            correct with { Records = correct.Records.Skip(1).ToArray() },
            correct with { Records = correct.Records.Concat([correct.Records[0]]).ToArray() },
            correct with { Metadata = null },
            correct with { StructuralStatus = correct.StructuralStatus == StructuralParseStatus.Clean ? StructuralParseStatus.Partial : StructuralParseStatus.Clean },
            correct with { Issues = correct.Issues.Concat([new SourceIssue("invented", "incorrect mock issue", null, null)]).ToArray() }
        };
        if (correct.Records.Count > 1) controls.Add(correct with { Records = correct.Records.Reverse().ToArray() });
        if (correct.Issues.Count > 0) controls.Add(correct with { Issues = [] });
        foreach (var wrong in controls)
            Assert.NotNull(Record.Exception(() => HidlContractCases.AssertContract(test, new ScriptedSourceExtractor("hidl-source", _ => wrong))));
    }

    [Theory, MemberData(nameof(HidlContractCases.Cases), MemberType = typeof(HidlContractCases))]
    public void InputMutationAndUnexpectedProviderFailureAreRejected(string id)
    {
        var test = HidlContractCases.Get(id);
        Assert.NotNull(Record.Exception(() => HidlContractCases.AssertContract(test,
            new ScriptedSourceExtractor("incorrect-provider-kind", _ => test.CorrectResult()))));
        var mutating = new ScriptedSourceExtractor("hidl-source", source =>
        {
            Assert.True(MemoryMarshal.TryGetArray(source.Bytes, out var bytes)); bytes.Array![bytes.Offset] ^= 1;
            return test.CorrectResult();
        });
        Assert.NotNull(Record.Exception(() => HidlContractCases.AssertContract(test, mutating)));
        Assert.NotNull(Record.Exception(() => HidlContractCases.AssertContract(test,
            new ScriptedSourceExtractor("hidl-source", _ => throw new IOException("scripted provider failure")))));
    }

    [Fact]
    public void HidlFixtureInventoryPreservesLegacyAndNewBoundaries()
    {
        Assert.Equal(29, HidlContractCases.All.Count);
        Assert.Equal(HidlContractCases.All.Count, HidlContractCases.All.Select(x => x.Source.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(HidlContractCases.All, x => Assert.False(x.Metadata["source_complete"]!.GetValue<bool>()));
        Assert.All(HidlContractCases.All, x => Assert.False(string.IsNullOrWhiteSpace(x.Source.Evidence)));
        Assert.Contains(HidlContractCases.All, x => x.Source.Status == StructuralParseStatus.Unsupported);
        Assert.Contains(HidlContractCases.All, x => x.Source.Status == StructuralParseStatus.Malformed);
        Assert.Contains(HidlContractCases.All, x => x.Source.Status == StructuralParseStatus.Partial);
        Assert.Contains(HidlContractCases.All, x => x.Source.ExpectedRecords.Any(row => row["line"]!.GetValue<int>() == 302));
    }
}
