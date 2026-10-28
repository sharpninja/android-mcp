using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Mock"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class ParserRemainingMockTests
{
    private static void Reject(ParserOriginalCase test, ExtractionResult wrong) => Assert.NotNull(Record.Exception(() =>
        ParserRemainingContractCases.AssertContract(test, new ScriptedSourceExtractor("aidl", _ => wrong))));

    [Theory, MemberData(nameof(ParserRemainingContractCases.Cases), MemberType = typeof(ParserRemainingContractCases))]
    public void CorrectMocksExerciseSameRemainingOriginalAssertions(string id)
    {
        var test = ParserRemainingContractCases.Get(id);
        var provider = new ScriptedSourceExtractor("aidl", _ => test.CorrectResult());
        ParserRemainingContractCases.AssertContract(test, provider);
        var call = Assert.Single(provider.Received); var expected = test.Source.Document();
        Assert.Equal(expected.RelativePath, call.RelativePath); Assert.Equal(expected.Context, call.Context);
        Assert.Equal(expected.Bytes.ToArray(), call.Bytes.ToArray());
    }

    [Theory, MemberData(nameof(ParserRemainingContractCases.SourceMutations), MemberType = typeof(ParserRemainingContractCases))]
    public void EachSourcePropertyHasIndependentOmissionAndWrongControls(string id, SourceMutation mutation)
    {
        var test = ParserRemainingContractCases.Get(id);
        var wrong = SourceMutationCases.Apply(test.Source, mutation) with { Metadata = (JsonObject)test.Metadata!.DeepClone() };
        Reject(test, wrong);
    }

    [Theory, MemberData(nameof(ParserRemainingContractCases.MetadataMutations), MemberType = typeof(ParserRemainingContractCases))]
    public void EachRawSchemaStatusAndUnknownWirePropertyHasIndependentControls(string id, SourceMutation mutation)
    {
        var test = ParserRemainingContractCases.Get(id);
        Reject(test, test.CorrectResult() with { Metadata = SourceMutationCases.Apply(test.MetadataMutationCase(), mutation).Records[0] });
    }

    [Theory, MemberData(nameof(ParserRemainingContractCases.Cases), MemberType = typeof(ParserRemainingContractCases))]
    public void MissingDuplicatedRecordsAndWrongStructuralStateAreRejected(string id)
    {
        var test = ParserRemainingContractCases.Get(id); var correct = test.CorrectResult();
        Reject(test, correct with { Records = correct.Records.Skip(1).ToArray() });
        Reject(test, correct with { Records = correct.Records.Concat([correct.Records[0]]).ToArray() });
        Reject(test, correct with { StructuralStatus = correct.StructuralStatus == StructuralParseStatus.Clean ? StructuralParseStatus.Partial : StructuralParseStatus.Clean });
        Reject(test, correct with { Issues = correct.Issues.Concat([new SourceIssue("invented", "wrong mock issue", null, null)]).ToArray() });
    }

    [Fact]
    public void RetiredStructuredPlaceholderIsNotMisrepresentedAsSatisfied()
    {
        var test = ParserRemainingContractCases.Get("PARSER-STRUCTURED-STATUS"); Assert.NotNull(test.RetiredAssertion);
        var wrong = test.CorrectResult();
        wrong.Metadata!["aidl_schema"]!["parse_status"] = "structured-body-retained; fields/enumerants/union tags unresolved";
        Reject(test, wrong);
    }
}
