using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed class ScriptedArtifactDirectory : ICreateNewArtifactDirectory
{
    public required Func<string, CancellationToken, Task> Response { get; init; }
    public List<(string Path, CancellationToken Token)> Calls { get; } = [];
    public Task CreateNewDirectoryAsync(string path, CancellationToken token = default)
    { Calls.Add((path, token)); return Response(path, token); }
}
[Trait("Gate", "Mock"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class ArtifactDirectoryMockTests
{
    private static Exception Error(string kind) => kind switch
    { "io" => new IOException("retained target"), "argument" => new ArgumentException("unsafe path"), "cancel" => new OperationCanceledException(), _ => new InvalidOperationException("unexpected error kind") };
    private static (ArtifactDirectoryRig Rig, ScriptedArtifactDirectory Provider) Rig(ArtifactDirectoryCase test, string? mutation = null)
    {
        var state = test.Before.Copy(); var count = 0;
        var provider = new ScriptedArtifactDirectory
        {
            Response = (path, token) =>
            {
                Assert.Equal(test.Path, path); count++;
                if (count > 1) return mutation == "retry" ? Task.CompletedTask : Task.FromException(new IOException("existing retained directory"));
                Assert.Equal(test.Cancelled, token.IsCancellationRequested);
                state = test.After.Copy();
                if (mutation == "missing") state = state with { Directories = state.Directories.Where(x => x != test.Path).ToArray() };
                if (mutation == "extra") state = state with { Directories = state.Directories.Append("invented").ToArray() };
                if (mutation == "files") state = state with { Files = new Dictionary<string, string>(state.Files) { ["invented.txt"] = "overwritten" } };
                if (mutation == "links") state = state with { Links = state.Links.Append("invented-link").ToArray() };
                if (mutation == "wrong-error") return Task.FromException(new InvalidOperationException("wrong error"));
                return test.Error is not null && mutation != "swallow" ? Task.FromException(Error(test.Error)) : Task.CompletedTask;
            }
        };
        return (new(provider, () => state, () => mutation == "hash" ? 1 : 0), provider);
    }
    [Theory, MemberData(nameof(ArtifactDirectoryContractCases.Cases), MemberType = typeof(ArtifactDirectoryContractCases))]
    public async Task CorrectDirectoryMocksProveEffectsErrorsAndRetainedRetry(string id)
    {
        var test = ArtifactDirectoryContractCases.Get(id); var rig = Rig(test);
        await ArtifactDirectoryContractCases.AssertContract(test, rig.Rig);
        Assert.Equal(test.LaterFailure ? 2 : 1, rig.Provider.Calls.Count);
        Assert.All(rig.Provider.Calls, call => Assert.Equal(test.Path, call.Path));
    }
    [Theory, MemberData(nameof(ArtifactDirectoryContractCases.Cases), MemberType = typeof(ArtifactDirectoryContractCases))]
    public async Task IndependentWrongDirectoryResponsesAreRejected(string id)
    {
        var test = ArtifactDirectoryContractCases.Get(id);
        var controls = new List<string> { "extra", "files", "links", "wrong-error" };
        if (test.Error is null) controls.Add("missing"); else controls.Add("swallow");
        if (test.Error is null) controls.Add("hash");
        if (test.LaterFailure) controls.Add("retry");
        foreach (var control in controls)
        {
            var rig = Rig(test, control);
            Assert.NotNull(await Record.ExceptionAsync(() => ArtifactDirectoryContractCases.AssertContract(test, rig.Rig)));
        }
    }
}
