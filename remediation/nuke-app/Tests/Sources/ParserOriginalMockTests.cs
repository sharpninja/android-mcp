using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class ParserOriginalMockTests
{
    private static ScriptedPermissionExpression CorrectPermission(ParserOriginalCase test) => new((_, _, _) =>
        test.Permission is null ? throw new InvalidOperationException("Unrequested permission call") : (JsonObject)test.Permission.Expected.DeepClone());

    private static void Reject(ParserOriginalCase test, ExtractionResult wrong) => Assert.NotNull(Record.Exception(() =>
        ParserOriginalContractCases.AssertContract(test, new ScriptedSourceExtractor(test.Source.Kind, _ => wrong), CorrectPermission(test))));

    [Theory, MemberData(nameof(ParserOriginalContractCases.Cases), MemberType = typeof(ParserOriginalContractCases))]
    public void CorrectScriptedServicesExerciseExactFutureNativeConsumerAssertions(string id)
    {
        var test = ParserOriginalContractCases.Get(id);
        var extractor = new ScriptedSourceExtractor(test.Source.Kind, _ => test.CorrectResult());
        var permission = CorrectPermission(test);
        ParserOriginalContractCases.AssertContract(test, extractor, permission);
        var document = Assert.Single(extractor.Received); var expected = test.Source.Document();
        Assert.Equal(expected.RelativePath, document.RelativePath); Assert.Equal(expected.Context, document.Context);
        Assert.Equal(expected.Bytes.ToArray(), document.Bytes.ToArray());
        Assert.Contains(test.OriginalFragment, test.Source.Text, StringComparison.Ordinal);
        if (test.Permission is null) Assert.Empty(permission.Calls);
        else
        {
            var call = Assert.Single(permission.Calls);
            Assert.True(JsonNode.DeepEquals(test.Source.ExpectedRecords[test.Permission.RecordIndex]["annotations"], call.Annotations));
            Assert.Equal(test.Permission.Aidl, call.Aidl);
            Assert.Equal(test.Permission.Symbols?.OrderBy(pair => pair.Key, StringComparer.Ordinal), call.Symbols?.OrderBy(pair => pair.Key, StringComparer.Ordinal));
        }
    }

    [Theory, MemberData(nameof(ParserOriginalContractCases.SourceMutations), MemberType = typeof(ParserOriginalContractCases))]
    public void EveryDeclaredOriginalSourceAssertionRejectsMissingAndWrongMocks(string id, SourceMutation mutation)
    {
        var test = ParserOriginalContractCases.Get(id);
        // Preserve every nonmutated concern so a different missing metadata
        // field cannot accidentally mask the intended negative control.
        var wrong = SourceMutationCases.Apply(test.Source, mutation) with { Metadata = (JsonObject?)test.Metadata?.DeepClone() };
        Reject(test, wrong);
    }

    [Theory, MemberData(nameof(ParserOriginalContractCases.PermissionMutations), MemberType = typeof(ParserOriginalContractCases))]
    public void EachPermissionOutputPropertyHasIndependentNegativeControls(string id, SourceMutation mutation)
    {
        var test = ParserOriginalContractCases.Get(id);
        var wrong = SourceMutationCases.Apply(test.PermissionMutationCase(), mutation).Records[0];
        Assert.NotNull(Record.Exception(() => ParserOriginalContractCases.AssertContract(test,
            new ScriptedSourceExtractor(test.Source.Kind, _ => test.CorrectResult()), new ScriptedPermissionExpression((_, _, _) => wrong))));
    }

    [Theory, MemberData(nameof(ParserOriginalContractCases.MetadataMutations), MemberType = typeof(ParserOriginalContractCases))]
    public void RawSchemaAndUnknownCodecFactsHaveIndependentNegativeControls(string id, SourceMutation mutation)
    {
        var test = ParserOriginalContractCases.Get(id);
        var wrong = SourceMutationCases.Apply(test.MetadataMutationCase(), mutation).Records[0];
        Reject(test, test.CorrectResult() with { Metadata = wrong });
    }

    [Theory, MemberData(nameof(ParserOriginalContractCases.Cases), MemberType = typeof(ParserOriginalContractCases))]
    public void RecordLossDuplicationStatusAndIssueFaultsAreSeparatelyRejected(string id)
    {
        var test = ParserOriginalContractCases.Get(id); var result = test.CorrectResult();
        Reject(test, result with { Records = result.Records.Skip(1).ToArray() });
        Reject(test, result with { Records = result.Records.Concat([result.Records[0]]).ToArray() });
        Reject(test, result with { StructuralStatus = result.StructuralStatus == StructuralParseStatus.Clean ? StructuralParseStatus.Partial : StructuralParseStatus.Clean });
        Reject(test, result with { Issues = result.Issues.Concat([new SourceIssue("invented", "independently wrong mock issue", null, null)]).ToArray() });
    }

    [Theory, MemberData(nameof(ParserOriginalContractCases.Cases), MemberType = typeof(ParserOriginalContractCases))]
    public void InputMutationExceptionsAndWrongKindAreNotSuccessfulExtraction(string id)
    {
        var test = ParserOriginalContractCases.Get(id);
        var mutated = new ScriptedSourceExtractor(test.Source.Kind, source =>
        {
            Assert.True(MemoryMarshal.TryGetArray(source.Bytes, out var segment));
            segment.Array![segment.Offset] ^= 1; return test.CorrectResult();
        });
        Assert.NotNull(Record.Exception(() => ParserOriginalContractCases.AssertContract(test, mutated, CorrectPermission(test))));
        Assert.NotNull(Record.Exception(() => ParserOriginalContractCases.AssertContract(test,
            new ScriptedSourceExtractor(test.Source.Kind, _ => throw new IOException("scripted failure")), CorrectPermission(test))));
        Assert.NotNull(Record.Exception(() => ParserOriginalContractCases.AssertContract(test,
            new ScriptedSourceExtractor("wrong-kind", _ => test.CorrectResult()), CorrectPermission(test))));
    }

    [Theory, MemberData(nameof(ParserOriginalContractCases.PermissionCases), MemberType = typeof(ParserOriginalContractCases))]
    public void PermissionMutationsAndInventedGrantAuthorityAreRejected(string id)
    {
        var test = ParserOriginalContractCases.Get(id); var expected = test.Permission ?? throw new InvalidOperationException("Permission case required");
        var extractor = new ScriptedSourceExtractor(test.Source.Kind, _ => test.CorrectResult());
        var mutated = new ScriptedPermissionExpression((annotations, _, _) =>
        { annotations.Add(new JsonObject { ["name"] = "invented" }); return (JsonObject)expected.Expected.DeepClone(); });
        Assert.NotNull(Record.Exception(() => ParserOriginalContractCases.AssertContract(test, extractor, mutated)));
        var authority = (JsonObject)expected.Expected.DeepClone(); authority["mcp_client_granted"] = true;
        Assert.NotNull(Record.Exception(() => ParserOriginalContractCases.AssertContract(test, extractor, new ScriptedPermissionExpression((_, _, _) => authority))));
        if (expected.Symbols is not null)
        {
            var changedSymbols = new ScriptedPermissionExpression((_, symbols, _) =>
            { ((IDictionary<string, string>)symbols!)[symbols!.Keys.First()] = "invented.permission"; return (JsonObject)expected.Expected.DeepClone(); });
            Assert.NotNull(Record.Exception(() => ParserOriginalContractCases.AssertContract(test, extractor, changedSymbols)));
        }
    }

    [Fact]
    public void RetiredIntermediateAssertionDoesNotReplaceFinalConstantInventoryProof()
    {
        var test = ParserOriginalContractCases.Get("PARSER-AIDL");
        Assert.NotNull(test.RetiredAssertion);
        var oldIntermediate = test.CorrectResult(); oldIntermediate.Records[3]["kind"] = "unsupported";
        Reject(test, oldIntermediate);
        Assert.Single(ParserOriginalContractCases.PendingOriginalMethods);
    }
}
