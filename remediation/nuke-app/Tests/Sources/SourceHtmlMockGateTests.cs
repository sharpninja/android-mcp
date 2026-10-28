using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class ScriptedHtmlDecoder(Func<string,string> response) : ISourceHtmlDecoder
{
    public List<string> Calls { get; } = [];
    public string Decode(string text) { Calls.Add(text); return response(text); }
}
public sealed class ScriptedEntityAudit(Func<ReadOnlyMemory<byte>,string,JsonObject> response) : IHtmlEntityDataAudit
{
    public List<(ReadOnlyMemory<byte> Bytes,string Legacy)> Calls { get; } = [];
    public JsonObject Compare(ReadOnlyMemory<byte> officialJson, string legacySourceText) { Calls.Add((officialJson, legacySourceText)); return response(officialJson, legacySourceText); }
}
[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SourceHtmlMockGateTests
{
    [Theory, MemberData(nameof(SourceHtmlContractCases.DecoderCases), MemberType = typeof(SourceHtmlContractCases))]
    public void CorrectLegacyHtmlDecodeMocksPassFutureRealAssertions(string id)
    {
        var test = SourceHtmlContractCases.Decoder(id); var fake = new ScriptedHtmlDecoder(_ => test.Expected);
        SourceHtmlContractCases.AssertDecode(test, fake); Assert.Equal(test.Input, Assert.Single(fake.Calls));
    }
    [Theory, MemberData(nameof(SourceHtmlContractCases.DecoderCases), MemberType = typeof(SourceHtmlContractCases))]
    public void WrongDecoderOutputsAndFailureAreRejected(string id)
    {
        var test = SourceHtmlContractCases.Decoder(id);
        foreach (var wrong in new[] { test.Expected + "wrong", null! }) Assert.NotNull(Record.Exception(() => SourceHtmlContractCases.AssertDecode(test, new ScriptedHtmlDecoder(_ => wrong))));
        Assert.NotNull(Record.Exception(() => SourceHtmlContractCases.AssertDecode(test, new ScriptedHtmlDecoder(_ => throw new IOException("scripted failure")))));
    }
    [Theory, MemberData(nameof(SourceHtmlContractCases.AuditCases), MemberType = typeof(SourceHtmlContractCases))]
    public void CorrectRestrictedLiteralDataAuditMocksPassActualSeam(string id)
    {
        var test = SourceHtmlContractCases.Audit(id); var fake = new ScriptedEntityAudit((_, _) => (JsonObject)test.Expected.DeepClone());
        SourceHtmlContractCases.AssertAudit(test, fake); Assert.Equal(test.Legacy, Assert.Single(fake.Calls).Legacy);
    }
    [Theory, MemberData(nameof(SourceHtmlContractCases.AuditMutations), MemberType = typeof(SourceHtmlContractCases))]
    public void EveryEntityDataAuditPropertyRejectsWrongAndMissing(string id, SourceMutation mutation)
    {
        var test = SourceHtmlContractCases.Audit(id); var wrong = SourceMutationCases.Apply(test.MutationCase(), mutation).Records[0];
        Assert.NotNull(Record.Exception(() => SourceHtmlContractCases.AssertAudit(test, new ScriptedEntityAudit((_, _) => wrong))));
    }
    [Theory, MemberData(nameof(SourceHtmlContractCases.AuditCases), MemberType = typeof(SourceHtmlContractCases))]
    public void AuditInputMutationAndProviderFailuresAreRejected(string id)
    {
        var test = SourceHtmlContractCases.Audit(id);
        Assert.NotNull(Record.Exception(() => SourceHtmlContractCases.AssertAudit(test, new ScriptedEntityAudit((bytes, _) =>
        { Assert.True(MemoryMarshal.TryGetArray(bytes, out var segment)); segment.Array![segment.Offset] ^= 1; return (JsonObject)test.Expected.DeepClone(); }))));
        Assert.NotNull(Record.Exception(() => SourceHtmlContractCases.AssertAudit(test, new ScriptedEntityAudit((_, _) => throw new IOException("scripted failure")))));
    }
    [Theory, MemberData(nameof(SourceHtmlFailureCases.Cases), MemberType = typeof(SourceHtmlFailureCases))]
    public void CorrectMalformedEntityDataMocksRejectAtActualAuditBoundary(string id)
    {
        var test = SourceHtmlFailureCases.Get(id); var fake = new ScriptedEntityAudit((_, _) => throw new FormatException(test.Message + " fixture rejection"));
        SourceHtmlFailureCases.AssertContract(test, fake); Assert.Single(fake.Calls);
    }
    [Theory, MemberData(nameof(SourceHtmlFailureCases.Cases), MemberType = typeof(SourceHtmlFailureCases))]
    public void MissingWrongTypeAndWrongReasonAuditFailuresAreRejected(string id)
    {
        var test = SourceHtmlFailureCases.Get(id);
        foreach (var wrong in new[] { new ScriptedEntityAudit((_, _) => new JsonObject()), new ScriptedEntityAudit((_, _) => throw new IOException("wrong failure")), new ScriptedEntityAudit((_, _) => throw new FormatException("unrelated failure")) })
            Assert.NotNull(Record.Exception(() => SourceHtmlFailureCases.AssertContract(test, wrong)));
    }
}
