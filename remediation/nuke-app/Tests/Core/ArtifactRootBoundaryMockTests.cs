using System.Text;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed record ArtifactRootBoundaryCase(string Id, bool Accepted, string RelativePath);
public sealed record ArtifactRootBoundaryBindings(IArtifactStore Store, IArtifactLeaseProvider Leases, Func<string[]> ObserveOutside);

// These cases bind the existing shared store/lease seams. A native fixture will
// supply each named root topology only after this discriminator gate passes.
public static class ArtifactRootBoundaryCases
{
    public static readonly ArtifactRootBoundaryCase[] All =
    [
        new("ordinary-existing-root", true, "generated.txt"),
        new("ordinary-new-root", true, "generated.txt"),
        new("root-directory-link", false, "generated.txt"),
        new("ancestor-link-existing-bound-root", false, "generated.txt"),
        new("ancestor-link-new-bound-root", false, "generated.txt"),
        new("dangling-root-link", false, "generated.txt"),
        new("artifact-child-link", false, "alias/generated.txt"),
        new("multiple-ancestors-above-bound-root", false, "generated.txt")
    ];
    public static IEnumerable<object[]> Data => All.Select(c => new object[] { c.Id });
    public static ArtifactRootBoundaryCase Get(string id) => All.Single(c => c.Id == id);
    public static readonly byte[] Bytes = Encoding.UTF8.GetBytes("abc");
    public const string Sha = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
    public static async Task AssertContract(ArtifactRootBoundaryCase test, ArtifactRootBoundaryBindings bindings)
    {
        var outsideBefore = bindings.ObserveOutside();
        try
        {
        var artifact = new Artifact(test.RelativePath, Bytes, "text/plain");
        if (test.Accepted)
        {
            var stored = await bindings.Store.WriteAsync(artifact);
            Assert.Equal(new StoredArtifact(test.RelativePath, Sha, 3, "text/plain"), stored);
            Assert.Equal(Bytes, (await bindings.Store.ReadAsync(test.RelativePath)).ToArray());
            await using var lease = await bindings.Leases.AcquireAsync("generation");
            Assert.NotNull(lease);
        }
        else
        {
            await Assert.ThrowsAsync<ArgumentException>(() => bindings.Store.WriteAsync(artifact));
            await Assert.ThrowsAsync<ArgumentException>(() => bindings.Store.ReadAsync(test.RelativePath));
            await Assert.ThrowsAsync<ArgumentException>(async () => { await using var lease = await bindings.Leases.AcquireAsync("generation"); });
        }
        }
        finally { Assert.Equal(outsideBefore, bindings.ObserveOutside()); }
    }
}

public sealed class RootBoundaryMockStore(bool accepted, string mutation = "") : IArtifactStore
{
    public Task<StoredArtifact> WriteAsync(Artifact artifact, CancellationToken token = default)
    {
        if (!accepted && mutation != "allow-write") throw new ArgumentException("Rejected root topology");
        var receipt = new StoredArtifact(artifact.RelativePath, ArtifactRootBoundaryCases.Sha, 3, "text/plain");
        receipt = mutation switch
        {
            "path" => receipt with { RelativePath = "wrong.txt" },
            "hash" => receipt with { Sha256 = new string('0', 64) },
            "length" => receipt with { Length = 4 },
            "media" => receipt with { MediaType = "application/octet-stream" },
            _ => receipt
        };
        return Task.FromResult(receipt);
    }
    public Task<ReadOnlyMemory<byte>> ReadAsync(string path, CancellationToken token = default)
    {
        if (!accepted && mutation != "allow-read") throw new ArgumentException("Rejected root topology");
        return Task.FromResult<ReadOnlyMemory<byte>>(mutation == "bytes" ? Encoding.UTF8.GetBytes("wrong") : ArtifactRootBoundaryCases.Bytes);
    }
}
public sealed class RootBoundaryMockLeases(bool accepted, string mutation = "") : IArtifactLeaseProvider
{
    public ValueTask<IAsyncDisposable> AcquireAsync(string scope, CancellationToken token = default)
    {
        if (!accepted && mutation != "allow-lease") throw new ArgumentException("Rejected root topology");
        return ValueTask.FromResult<IAsyncDisposable>(new Lease());
    }
    private sealed class Lease : IAsyncDisposable { public ValueTask DisposeAsync() => ValueTask.CompletedTask; }
}

[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-002")]
public sealed class ArtifactRootBoundaryMockTests
{
    private static ArtifactRootBoundaryBindings Bind(ArtifactRootBoundaryCase test, string mutation = "")
    {
        var observations = 0;
        return new(new RootBoundaryMockStore(test.Accepted, mutation), new RootBoundaryMockLeases(test.Accepted, mutation),
            () => mutation == "outside" && observations++ > 0 ? ["sentinel|changed", "unexpected-created-output"] : ["sentinel|original"]);
    }
    [Theory, MemberData(nameof(ArtifactRootBoundaryCases.Data), MemberType = typeof(ArtifactRootBoundaryCases))]
    public Task CorrectRootTopologyResponsesPass(string id)
    {
        var test = ArtifactRootBoundaryCases.Get(id);
        return ArtifactRootBoundaryCases.AssertContract(test, Bind(test));
    }
    [Theory, MemberData(nameof(ArtifactRootBoundaryCases.Data), MemberType = typeof(ArtifactRootBoundaryCases))]
    public async Task EachReceiptAndAdmissionAssertionRejectsItsOwnMutation(string id)
    {
        var test = ArtifactRootBoundaryCases.Get(id);
        var mutations = test.Accepted ? new[] { "path", "hash", "length", "media", "bytes", "outside" } : new[] { "allow-write", "allow-read", "allow-lease", "outside" };
        foreach (var mutation in mutations)
        {
            var bindings = Bind(test, mutation);
            Assert.NotNull(await Record.ExceptionAsync(() => ArtifactRootBoundaryCases.AssertContract(test, bindings)));
        }
    }
}
