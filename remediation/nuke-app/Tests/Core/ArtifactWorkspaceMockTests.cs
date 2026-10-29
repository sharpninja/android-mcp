using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Tests.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed class ScriptedArtifactWorkspaceCaseProvider(Func<string,JsonObject> response):IArtifactWorkspaceCaseProvider
{
    public List<string> Calls { get; }=[];
    public Task<JsonObject> ExecuteAsync(string scenario){Calls.Add(scenario);return Task.FromResult(response(scenario));}
}
[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004"),Trait("Requirement","TEST-BUILD-007")]
public sealed class ArtifactWorkspaceMockTests
{
    [Theory,MemberData(nameof(ArtifactWorkspaceContractCases.Cases),MemberType=typeof(ArtifactWorkspaceContractCases))]
    public async Task CorrectMocksPassSameFutureNativeOutcomeOrderingAndNoWriteAssertions(string id)
    {
        var test=ArtifactWorkspaceContractCases.Get(id);var provider=new ScriptedArtifactWorkspaceCaseProvider(_=>(JsonObject)test.Expected.DeepClone());
        await ArtifactWorkspaceContractCases.AssertContract(test,provider);Assert.Equal(id,Assert.Single(provider.Calls));
    }
    [Theory,MemberData(nameof(ArtifactWorkspaceContractCases.Mutations),MemberType=typeof(ArtifactWorkspaceContractCases))]
    public async Task EveryAssertedPropertyRejectsOmissionAndIncorrectValue(string id,SourceMutation mutation)
    {
        var test=ArtifactWorkspaceContractCases.Get(id);var wrong=SourceMutationCases.Apply(test.MutationCase(),mutation).Records[0];
        Assert.NotNull(await Record.ExceptionAsync(()=>ArtifactWorkspaceContractCases.AssertContract(test,new ScriptedArtifactWorkspaceCaseProvider(_=>wrong))));
    }
    [Theory,MemberData(nameof(ArtifactWorkspaceContractCases.Cases),MemberType=typeof(ArtifactWorkspaceContractCases))]
    public async Task UnexpectedWriteFailureAndReorderedCallsCannotPass(string id)
    {
        var test=ArtifactWorkspaceContractCases.Get(id);var wrong=(JsonObject)test.Expected.DeepClone();wrong["set_calls"]=99;
        Assert.NotNull(await Record.ExceptionAsync(()=>ArtifactWorkspaceContractCases.AssertContract(test,new ScriptedArtifactWorkspaceCaseProvider(_=>wrong))));
        Assert.NotNull(await Record.ExceptionAsync(()=>ArtifactWorkspaceContractCases.AssertContract(test,new ScriptedArtifactWorkspaceCaseProvider(_=>throw new IOException("fixture failure")))));
        if(test.Expected["trace"] is JsonArray {Count:>1} trace)
        {
            var reordered=(JsonObject)test.Expected.DeepClone();reordered["trace"]=new JsonArray(trace.Reverse().Select(node=>node?.DeepClone()).ToArray());
            Assert.NotNull(await Record.ExceptionAsync(()=>ArtifactWorkspaceContractCases.AssertContract(test,new ScriptedArtifactWorkspaceCaseProvider(_=>reordered))));
        }
    }
}
