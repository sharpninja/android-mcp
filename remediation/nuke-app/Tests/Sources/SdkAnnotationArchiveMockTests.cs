using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class ScriptedAnnotationArchive(Func<ReadOnlyMemory<byte>, JsonObject> response) : ISdkAnnotationArchiveSourceFacts
{ public JsonObject Parse(ReadOnlyMemory<byte> archive) => response(archive); }

[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
public sealed class SdkAnnotationArchiveMockTests
{
    [Theory, MemberData(nameof(SdkAnnotationArchiveContractCases.Cases), MemberType = typeof(SdkAnnotationArchiveContractCases))]
    public void CorrectMocksPassEveryRawArchiveConcernAssertion(string id)
    {
        var test = SdkAnnotationArchiveContractCases.Get(id);
        SdkAnnotationArchiveContractCases.AssertContract(test, new ScriptedAnnotationArchive(bytes =>
        { Assert.Equal(test.Input(), bytes.ToArray()); return (JsonObject)test.Expected.DeepClone(); }));
    }
    [Theory, MemberData(nameof(SdkAnnotationArchiveContractCases.Mutations), MemberType = typeof(SdkAnnotationArchiveContractCases))]
    public void EachIndividualRawTargetOrdinalValueStatusAndCountMutationIsRejected(string id, SourceMutation mutation)
    {
        var test = SdkAnnotationArchiveContractCases.Get(id);
        var wrong = SourceMutationCases.Apply(test.MutationCase(), mutation).Records[0];
        Assert.NotNull(Record.Exception(() => SdkAnnotationArchiveContractCases.AssertContract(test, new ScriptedAnnotationArchive(_ => wrong))));
    }
    [Theory, MemberData(nameof(SdkAnnotationArchiveContractCases.Failures), MemberType = typeof(SdkAnnotationArchiveContractCases))]
    public void InvalidArchivesRejectOmittedOrIncorrectFailure(string id, byte[] input)
    {
        Assert.StartsWith("ANN-Z-invalid-", id);
        SdkAnnotationArchiveContractCases.AssertFailure(input, new ScriptedAnnotationArchive(_ => throw new InvalidDataException("Malformed ZIP")));
        Assert.NotNull(Record.Exception(() => SdkAnnotationArchiveContractCases.AssertFailure(input, new ScriptedAnnotationArchive(_ => new()))));
        Assert.NotNull(Record.Exception(() => SdkAnnotationArchiveContractCases.AssertFailure(input, new ScriptedAnnotationArchive(_ => throw new IOException("Infrastructure failure")))));
    }
}
