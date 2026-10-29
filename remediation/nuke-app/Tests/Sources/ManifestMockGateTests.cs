using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class ManifestMockGateTests
{
    [Theory, MemberData(nameof(ManifestContractCases.Cases), MemberType = typeof(ManifestContractCases))]
    public void CorrectManifestMocksPassExactFutureRealAssertions(string id)
    {
        var test = ManifestContractCases.Get(id); var fake = new ScriptedSourceExtractor("manifest", _ => test.CorrectResult());
        ManifestContractCases.AssertContract(test, fake); var received = Assert.Single(fake.Received);
        Assert.Equal(test.Source.Document().Context, received.Context); Assert.Equal(test.Source.Document().RelativePath, received.RelativePath);
    }
    [Theory, MemberData(nameof(ManifestContractCases.RecordMutations), MemberType = typeof(ManifestContractCases))]
    public void EveryManifestRecordPropertyRejectsWrongAndMissing(string id, SourceMutation mutation)
    {
        var test = ManifestContractCases.Get(id);
        var wrong = SourceMutationCases.Apply(test.Source, mutation) with { Metadata = (JsonObject)test.Metadata.DeepClone() };
        Assert.NotNull(Record.Exception(() => ManifestContractCases.AssertContract(test, new ScriptedSourceExtractor("manifest", _ => wrong))));
    }
    [Theory, MemberData(nameof(ManifestContractCases.MetadataMutations), MemberType = typeof(ManifestContractCases))]
    public void EveryNormalizationReceiptPropertyRejectsWrongAndMissing(string id, SourceMutation mutation)
    {
        var test = ManifestContractCases.Get(id);
        var wrong = test.CorrectResult() with { Metadata = SourceMutationCases.Apply(test.MetadataCase(), mutation).Records[0] };
        Assert.NotNull(Record.Exception(() => ManifestContractCases.AssertContract(test, new ScriptedSourceExtractor("manifest", _ => wrong))));
    }
    [Theory, MemberData(nameof(ManifestContractCases.Cases), MemberType = typeof(ManifestContractCases))]
    public void CountsStatusInputAndAuthorityCannotBeInvented(string id)
    {
        var test = ManifestContractCases.Get(id); var correct = test.CorrectResult();
        var controls = new List<ExtractionResult>
        {
            correct with { Metadata = null },
            correct with { StructuralStatus = correct.StructuralStatus == StructuralParseStatus.Clean ? StructuralParseStatus.Partial : StructuralParseStatus.Clean },
            correct with { Issues = correct.Issues.Concat([new SourceIssue("invented", "wrong issue", null, null)]).ToArray() },
            correct with { Records = correct.Records.Concat([new JsonObject { ["kind"] = "invented-definition" }]).ToArray() }
        };
        if (correct.Records.Count > 0) { controls.Add(correct with { Records = correct.Records.Skip(1).ToArray() }); var authority = test.CorrectResult(); authority.Records[0]["granted"] = true; controls.Add(authority); }
        if (correct.Records.Count > 1 && !JsonNode.DeepEquals(correct.Records[0], correct.Records[^1])) controls.Add(correct with { Records = correct.Records.Reverse().ToArray() });
        if (correct.Issues.Count > 0) controls.Add(correct with { Issues = [] });
        foreach (var wrong in controls) Assert.NotNull(Record.Exception(() => ManifestContractCases.AssertContract(test, new ScriptedSourceExtractor("manifest", _ => wrong))));
        Assert.NotNull(Record.Exception(() => ManifestContractCases.AssertContract(test, new ScriptedSourceExtractor("wrong-kind", _ => correct))));
        if (test.Source.Text.Length > 0)
            Assert.NotNull(Record.Exception(() => ManifestContractCases.AssertContract(test, new ScriptedSourceExtractor("manifest", source =>
            { Assert.True(MemoryMarshal.TryGetArray(source.Bytes, out var bytes)); bytes.Array![bytes.Offset] ^= 1; return test.CorrectResult(); }))));
    }
    [Theory, MemberData(nameof(ManifestFailureCases.Cases), MemberType = typeof(ManifestFailureCases))]
    public void CorrectMalformedSourceMocksUseTypedActualExtractorSeam(string id)
    {
        var test = ManifestFailureCases.Get(id); var fake = new ScriptedSourceExtractor("manifest", _ => throw new FormatException(test.Message + " fixture rejection"));
        ManifestFailureCases.AssertContract(test, fake); Assert.Single(fake.Received);
    }
    [Theory, MemberData(nameof(ManifestFailureCases.Cases), MemberType = typeof(ManifestFailureCases))]
    public void MissingWrongTypeAndWrongReasonFailuresAreRejected(string id)
    {
        var test = ManifestFailureCases.Get(id);
        var wrong = new[]
        {
            new ScriptedSourceExtractor("manifest", _ => new([], [], StructuralParseStatus.Clean)),
            new ScriptedSourceExtractor("manifest", _ => throw new IOException("wrong failure")),
            new ScriptedSourceExtractor("manifest", _ => throw new FormatException("unrelated failure"))
        };
        foreach (var fake in wrong) Assert.NotNull(Record.Exception(() => ManifestFailureCases.AssertContract(test, fake)));
    }
}
