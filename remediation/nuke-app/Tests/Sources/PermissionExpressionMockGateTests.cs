using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class ScriptedPermissionExpression(Func<JsonArray, IReadOnlyDictionary<string,string>?, bool, JsonObject> response) : IPermissionExpressionSourceFacts
{
    public List<(JsonArray Annotations, IReadOnlyDictionary<string,string>? Symbols, bool Aidl)> Calls { get; } = [];
    public JsonObject Permission(JsonArray annotations, IReadOnlyDictionary<string,string>? symbols = null, bool aidl = false)
    { Calls.Add((annotations, symbols, aidl)); return response(annotations, symbols, aidl); }
}

[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class PermissionExpressionMockGateTests
{
    [Theory, MemberData(nameof(PermissionExpressionContractCases.Cases), MemberType = typeof(PermissionExpressionContractCases))]
    public void CorrectMocksExerciseExactFutureRealExpressionAssertions(string id)
    {
        var test = PermissionExpressionContractCases.Get(id);
        var fake = new ScriptedPermissionExpression((_, _, _) => (JsonObject)test.Expected.DeepClone());
        PermissionExpressionContractCases.AssertContract(test, fake);
        var call = Assert.Single(fake.Calls);
        Assert.True(JsonNode.DeepEquals(test.Annotations, call.Annotations));
        Assert.Equal(test.Aidl, call.Aidl);
        Assert.Equal(test.Symbols?.OrderBy(x => x.Key), call.Symbols?.OrderBy(x => x.Key));
    }

    [Theory, MemberData(nameof(PermissionExpressionContractCases.Mutations), MemberType = typeof(PermissionExpressionContractCases))]
    public void EveryExpressionPropertyHasIndividuallyWrongAndMissingControls(string id, SourceMutation mutation)
    {
        var test = PermissionExpressionContractCases.Get(id);
        var wrong = SourceMutationCases.Apply(test.MutationCase(), mutation).Records[0];
        Assert.NotNull(Record.Exception(() => PermissionExpressionContractCases.AssertContract(test, new ScriptedPermissionExpression((_, _, _) => wrong))));
    }

    [Theory, MemberData(nameof(PermissionExpressionContractCases.Cases), MemberType = typeof(PermissionExpressionContractCases))]
    public void SourceMutationExceptionsAndUnrequestedAuthorityAreRejected(string id)
    {
        var test = PermissionExpressionContractCases.Get(id);
        var authority = (JsonObject)test.Expected.DeepClone(); authority["mcp_client_granted"] = true;
        var wrong = new ScriptedPermissionExpression((annotations, _, _) => { annotations.Add(new JsonObject { ["name"] = "invented" }); return (JsonObject)test.Expected.DeepClone(); });
        Assert.NotNull(Record.Exception(() => PermissionExpressionContractCases.AssertContract(test, wrong)));
        Assert.NotNull(Record.Exception(() => PermissionExpressionContractCases.AssertContract(test, new ScriptedPermissionExpression((_, _, _) => authority))));
        Assert.NotNull(Record.Exception(() => PermissionExpressionContractCases.AssertContract(test, new ScriptedPermissionExpression((_, _, _) => throw new IOException("scripted failure")))));
        if (test.Symbols is not null)
        {
            var changedSymbols = new ScriptedPermissionExpression((_, symbols, _) =>
            { ((Dictionary<string,string>)symbols!)[symbols!.Keys.First()] = "invented.permission"; return (JsonObject)test.Expected.DeepClone(); });
            Assert.NotNull(Record.Exception(() => PermissionExpressionContractCases.AssertContract(test, changedSymbols)));
        }
    }
}
