using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;
using Xunit.Sdk;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
public sealed class AidlSchemaMockGateTests
{
    [Theory, MemberData(nameof(AidlSchemaContractCases.Cases), MemberType = typeof(AidlSchemaContractCases))]
    public void CorrectSchemaMocksExerciseSameAssertions(string id)
    { var test = AidlSchemaContractCases.Get(id); AidlSchemaContractCases.AssertContract(test, new ScriptedSourceExtractor("aidl", _ => test.Correct())); }
    [Theory, MemberData(nameof(AidlSchemaContractCases.MetadataMutations), MemberType = typeof(AidlSchemaContractCases))]
    public void EveryMissingAndWrongSchemaPropertyIsRejected(string id, SourceMutation mutation)
    {
        var test = AidlSchemaContractCases.Get(id); var incorrect = SourceMutationCases.Apply(test.MetadataCase(), mutation).Records[0];
        var result = test.Correct() with { Metadata = new JsonObject { ["aidl_schema"] = incorrect.DeepClone() } };
        Reject(test, result);
    }
    [Theory, MemberData(nameof(AidlSchemaContractCases.RecordMutations), MemberType = typeof(AidlSchemaContractCases))]
    public void EveryRequiredRecordPropertyRemainsAsserted(string id, SourceMutation mutation)
    {
        var test = AidlSchemaContractCases.Get(id); var incorrect = SourceMutationCases.Apply(test.Source, mutation);
        Reject(test, incorrect with { Metadata = test.Correct().Metadata });
    }
    [Theory, MemberData(nameof(AidlSchemaContractCases.Cases), MemberType = typeof(AidlSchemaContractCases))]
    public void DroppedMetadataRecordsIssuesAndWrongStatusAreRejected(string id)
    {
        var test = AidlSchemaContractCases.Get(id); var correct = test.Correct();
        Reject(test, correct with { Metadata = null }); Reject(test, correct with { Metadata = new JsonObject() });
        Reject(test, correct with { Records = correct.Records.Skip(1).ToArray() });
        Reject(test, correct with { Records = correct.Records.Concat([correct.Records[0]]).ToArray() });
        Reject(test, correct with { StructuralStatus = test.Source.Status == StructuralParseStatus.Clean ? StructuralParseStatus.Unsupported : StructuralParseStatus.Clean });
        Reject(test, correct with { Issues = correct.Issues.Concat([new SourceIssue("invented", "wrong", null, null)]).ToArray() });
        if (correct.Issues.Count > 0) Reject(test, correct with { Issues = [] });
        var declarations = test.Schema["transaction_metadata"]!["declarations"]!.AsArray();
        var dropped = (JsonObject)test.Schema.DeepClone(); dropped["transaction_metadata"]!["declarations"] = new JsonArray(declarations.Skip(1).Select(node => node!.DeepClone()).ToArray());
        Reject(test, correct with { Metadata = new JsonObject { ["aidl_schema"] = dropped } });
        var duplicated = (JsonObject)test.Schema.DeepClone(); duplicated["transaction_metadata"]!["declarations"] = new JsonArray(declarations.Concat([declarations[0]]).Select(node => node!.DeepClone()).ToArray());
        Reject(test, correct with { Metadata = new JsonObject { ["aidl_schema"] = duplicated } });
        if (declarations.Count > 1)
        {
            var reversed = (JsonObject)test.Schema.DeepClone(); reversed["transaction_metadata"]!["declarations"] = new JsonArray(declarations.Reverse().Select(node => node!.DeepClone()).ToArray());
            Reject(test, correct with { Metadata = new JsonObject { ["aidl_schema"] = reversed } });
        }
    }
    [Theory, MemberData(nameof(AidlSchemaContractCases.NonemptyCases), MemberType = typeof(AidlSchemaContractCases))]
    public void MutatedInputAndProviderFailureControlsAreExact(string id)
    {
        var test = AidlSchemaContractCases.Get(id);
        Assert.ThrowsAny<XunitException>(() => AidlSchemaContractCases.AssertContract(test, new ScriptedSourceExtractor("aidl", source =>
        { Assert.True(MemoryMarshal.TryGetArray(source.Bytes, out var bytes)); bytes.Array![bytes.Offset] ^= 1; return test.Correct(); })));
        Assert.Throws<IOException>(() => AidlSchemaContractCases.AssertContract(test, new ScriptedSourceExtractor("aidl", _ => throw new IOException("provider failure"))));
    }
    private static void Reject(AidlSchemaCase test, ExtractionResult result) => Assert.ThrowsAny<XunitException>(() =>
        AidlSchemaContractCases.AssertContract(test, new ScriptedSourceExtractor("aidl", _ => result)));
}
