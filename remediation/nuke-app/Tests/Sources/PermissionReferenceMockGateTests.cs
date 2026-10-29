using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class ScriptedPermissionReference(Func<ReadOnlyMemory<byte>, JsonObject> response) : IPermissionReferenceSourceFacts
{
    public List<ReadOnlyMemory<byte>> Calls { get; } = [];
    public JsonObject Parse(ReadOnlyMemory<byte> html) { Calls.Add(html); return response(html); }
}
[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class PermissionReferenceMockGateTests
{
    [Theory, MemberData(nameof(PermissionReferenceContractCases.Cases), MemberType = typeof(PermissionReferenceContractCases))]
    public void CorrectDocumentationMocksPassExactFutureRealAssertions(string id)
    {
        var test = PermissionReferenceContractCases.Get(id); var fake = new ScriptedPermissionReference(_ => (JsonObject)test.Expected.DeepClone());
        PermissionReferenceContractCases.AssertContract(test, fake); Assert.Equal(System.Text.Encoding.UTF8.GetBytes(test.Html), Assert.Single(fake.Calls).ToArray());
    }
    [Theory, MemberData(nameof(PermissionReferenceContractCases.Mutations), MemberType = typeof(PermissionReferenceContractCases))]
    public void EveryDocumentationPropertyRejectsWrongAndMissing(string id, SourceMutation mutation)
    {
        var test = PermissionReferenceContractCases.Get(id); var wrong = SourceMutationCases.Apply(test.MutationCase(), mutation).Records[0];
        Assert.NotNull(Record.Exception(() => PermissionReferenceContractCases.AssertContract(test, new ScriptedPermissionReference(_ => wrong))));
    }
    [Theory, MemberData(nameof(PermissionReferenceContractCases.Cases), MemberType = typeof(PermissionReferenceContractCases))]
    public void InventedDefinitionAuthorityInputMutationAndFailureAreRejected(string id)
    {
        var test = PermissionReferenceContractCases.Get(id); var authority = (JsonObject)test.Expected.DeepClone(); authority["manifest_definition"] = true;
        Assert.NotNull(Record.Exception(() => PermissionReferenceContractCases.AssertContract(test, new ScriptedPermissionReference(_ => authority))));
        Assert.NotNull(Record.Exception(() => PermissionReferenceContractCases.AssertContract(test, new ScriptedPermissionReference(_ => throw new IOException("scripted failure")))));
        if (test.Html.Length > 0)
            Assert.NotNull(Record.Exception(() => PermissionReferenceContractCases.AssertContract(test, new ScriptedPermissionReference(bytes =>
            { Assert.True(MemoryMarshal.TryGetArray(bytes, out var segment)); segment.Array![segment.Offset] ^= 1; return (JsonObject)test.Expected.DeepClone(); }))));
    }
}
