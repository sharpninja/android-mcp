using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed record ArtifactDirectoryState(string[] Directories, IReadOnlyDictionary<string, string> Files, string[] Links)
{
    public ArtifactDirectoryState Copy() => new(Directories.ToArray(), Files.ToDictionary(), Links.ToArray());
}
public sealed record ArtifactDirectoryCase(string Id, string Path, ArtifactDirectoryState Before,
    ArtifactDirectoryState After, string? Error = null, bool Cancelled = false, bool LaterFailure = false);
public sealed class ArtifactDirectoryRig(ICreateNewArtifactDirectory provider, Func<ArtifactDirectoryState> observe,
    Func<int> hashCalls)
{
    public ICreateNewArtifactDirectory Provider { get; } = provider;
    public Func<ArtifactDirectoryState> Observe { get; } = observe;
    public Func<int> HashCalls { get; } = hashCalls;
}
public static class ArtifactDirectoryContractCases
{
    private static ArtifactDirectoryState State(string[]? directories = null, string[]? links = null,
        params (string Path, string Text)[] files) => new(directories ?? [], files.ToDictionary(x => x.Path, x => x.Text), links ?? []);
    public static IReadOnlyList<ArtifactDirectoryCase> All { get; } = Create();
    private static IReadOnlyList<ArtifactDirectoryCase> Create()
    {
        var cases = new List<ArtifactDirectoryCase>
        {
            new("DIR-01", "out", State(), State(["out"])),
            new("DIR-02", "a/b/out", State(), State(["a", "a/b", "a/b/out"])),
            new("DIR-03", "a/out", State(["a", "keep"], files: [("keep/input.txt", "original")]), State(["a", "a/out", "keep"], files: [("keep/input.txt", "original")])),
            new("DIR-04", "out", State(["out"]), State(["out"]), "io"),
            new("DIR-05", "out", State(["out"], files: [("out/kept.txt", "untouched")]), State(["out"], files: [("out/kept.txt", "untouched")]), "io"),
            new("DIR-06", "out", State(files: [("out", "regular-file")]), State(files: [("out", "regular-file")]), "io"),
            new("DIR-07", "a/out", State(files: [("a", "parent-file")]), State(files: [("a", "parent-file")]), "io"),
            new("DIR-08", "out", State(), State(), "cancel", true),
            new("DIR-09", "out", State(), State(["out"]), LaterFailure: true),
            new("DIR-10", "a/out", State(["a"], files: [("input", "pinned")]), State(["a", "a/out"], files: [("input", "pinned")]), LaterFailure: true),
            new("DIR-11", "link/out", State(links: ["link"]), State(links: ["link"]), "argument"),
            new("DIR-12", "out", State(links: ["out"]), State(links: ["out"]), "argument"),
            new("DIR-13", "out", State(links: ["$root"]), State(links: ["$root"]), "argument"),
            new("DIR-14", "out", State(links: ["$ancestor"]), State(links: ["$ancestor"]), "argument")
        };
        var index = 15;
        foreach (var path in new[] { "", " ", "/absolute", "a//b", "a/./b", "a/../b", "a\\b", "C:x" })
            cases.Add(new("DIR-" + index++, path, State(files: [("keep", "original")]), State(files: [("keep", "original")]), "argument"));
        return cases;
    }
    public static IEnumerable<object[]> Cases => All.Select(x => new object[] { x.Id });
    public static ArtifactDirectoryCase Get(string id) => All.Single(x => x.Id == id);
    private static void AssertState(ArtifactDirectoryState expected, ArtifactDirectoryState actual)
    {
        Assert.Equal(expected.Directories.Order(StringComparer.Ordinal), actual.Directories.Order(StringComparer.Ordinal));
        Assert.Equal(expected.Links.Order(StringComparer.Ordinal), actual.Links.Order(StringComparer.Ordinal));
        Assert.Equal(expected.Files.OrderBy(x => x.Key, StringComparer.Ordinal), actual.Files.OrderBy(x => x.Key, StringComparer.Ordinal));
    }
    public static async Task AssertContract(ArtifactDirectoryCase test, ArtifactDirectoryRig rig)
    {
        AssertState(test.Before, rig.Observe());
        using var cancellation = new CancellationTokenSource(); if (test.Cancelled) cancellation.Cancel();
        var error = await Record.ExceptionAsync(() => rig.Provider.CreateNewDirectoryAsync(test.Path, cancellation.Token));
        switch (test.Error)
        {
            case null: Assert.Null(error); break;
            case "io": Assert.IsAssignableFrom<IOException>(error); break;
            case "argument": Assert.IsType<ArgumentException>(error); break;
            case "cancel": Assert.IsAssignableFrom<OperationCanceledException>(error); break;
            default: throw new InvalidOperationException("Unknown directory fixture error");
        }
        AssertState(test.After, rig.Observe()); Assert.Equal(0, rig.HashCalls());
        if (test.LaterFailure)
        {
            // The workflow's later read/property/parser failure must not roll
            // back this already successful directory capability.
            var later = new FormatException("independent later parser failure");
            Assert.Same(later, await Record.ExceptionAsync(() => Task.FromException(later)));
            AssertState(test.After, rig.Observe());
            await Assert.ThrowsAnyAsync<IOException>(() => rig.Provider.CreateNewDirectoryAsync(test.Path));
            AssertState(test.After, rig.Observe()); Assert.Equal(0, rig.HashCalls());
        }
    }
}
