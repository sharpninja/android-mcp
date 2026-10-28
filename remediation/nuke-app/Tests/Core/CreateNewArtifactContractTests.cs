using System.Text;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed record CreateNewCase(string Id, string Mode, string Path = "challenge.bin");
public interface ICreateNewHarness
{
    ICreateNewArtifactStore Store { get; }
    ICreateNewArtifactStore Peer { get; }
    void Configure(string mode, CancellationTokenSource cancellation);
    void PrepareFilesystem(string mode);
    bool Exists(string path);
    byte[] ReadFixture();
}
public static class CreateNewArtifactCases
{
    public static readonly byte[] Abc = Encoding.ASCII.GetBytes("abc");
    public static readonly byte[] Different = Encoding.ASCII.GetBytes("xyz");
    public const string Media = "application/octet-stream";
    public static IEnumerable<object[]> Cases => new[] {
        new CreateNewCase("NEW-01", "new"), new CreateNewCase("NEW-02", "empty"),
        new CreateNewCase("NEW-03", "existing-identical"), new CreateNewCase("NEW-04", "existing-different"),
        new CreateNewCase("NEW-05", "strict-retry"), new CreateNewCase("NEW-06", "ordinary-after-strict"),
        new CreateNewCase("NEW-07", "stream-existing"), new CreateNewCase("NEW-08", "retained-partial"),
        new CreateNewCase("NEW-09", "cancel-before"), new CreateNewCase("NEW-10", "cancel-hash"),
        new CreateNewCase("NEW-11", "hash-failure"), new CreateNewCase("NEW-12", "directory"),
        new CreateNewCase("NEW-13", "invalid", ""), new CreateNewCase("NEW-14", "invalid", "../escape"),
        new CreateNewCase("NEW-15", "invalid", "a/../escape"), new CreateNewCase("NEW-16", "invalid", "/absolute"),
        new CreateNewCase("NEW-17", "invalid", "C:/drive"), new CreateNewCase("NEW-18", "invalid", "a\\escape"),
        new CreateNewCase("NEW-19", "invalid", "a//escape"),
        new CreateNewCase("NEW-20", "root-link"), new CreateNewCase("NEW-21", "ancestor-link", "nested/challenge.bin"),
        new CreateNewCase("NEW-22", "leaf-link"), new CreateNewCase("NEW-23", "dangling-link"),
        new CreateNewCase("NEW-24", "strict-race"), new CreateNewCase("NEW-25", "stream-race")
    }.Select(c => new object[] { c });
    public static string[] Mutations(CreateNewCase c) => c.Mode switch {
        "existing-identical" or "existing-different" or "strict-retry" or "stream-existing" or "retained-partial" => ["existing-success", "overwrite-original"],
        "cancel-before" or "cancel-hash" => ["cancel", "failure-writes"],
        "hash-failure" => ["failure-success", "failure-writes"],
        "invalid" => ["path"],
        "directory" => ["boundary", "fixture-overwrite"],
        "root-link" or "ancestor-link" or "leaf-link" or "dangling-link" => ["boundary", "fixture-overwrite"],
        "strict-race" or "stream-race" => ["race-success", "race-overwrite"],
        _ => ["receipt-path", "receipt-hash", "receipt-length", "receipt-media", "bytes"]
    };
    public static async Task AssertContract(CreateNewCase c, ICreateNewHarness harness)
    {
        using var cancellation = new CancellationTokenSource();
        harness.Configure(c.Mode, cancellation);
        var store = harness.Store;
        var input = new Artifact(c.Path, c.Mode == "empty" ? Array.Empty<byte>() : Abc, Media);
        byte[]? original = null;
        if (c.Mode is "existing-identical" or "existing-different")
        {
            original = c.Mode == "existing-different" ? Different : Abc;
            await store.WriteAsync(new(c.Path, original, Media));
        }
        if (c.Mode is "strict-retry" or "ordinary-after-strict") await store.WriteNewAsync(input);
        if (c.Mode == "strict-retry") original = Abc;
        if (c.Mode == "stream-existing")
        {
            await ((IStreamingArtifactStore)store).WriteStreamAsync(c.Path, Media, (stream, token) => stream.WriteAsync(Abc, token).AsTask()); original = Abc;
        }
        if (c.Mode == "retained-partial")
        {
            var failure = await Record.ExceptionAsync(() => ((IStreamingArtifactStore)store).WriteStreamAsync(c.Path, Media, async (stream, token) =>
            { await stream.WriteAsync(new byte[] { (byte)'a' }, token); throw new IOException("seed-partial"); }));
            Assert.IsType<IOException>(failure); original = [(byte)'a']; input = input with { Bytes = original };
        }
        if (c.Mode is "directory" or "root-link" or "ancestor-link" or "leaf-link" or "dangling-link") harness.PrepareFilesystem(c.Mode);
        if (c.Mode == "cancel-before") cancellation.Cancel();
        if (c.Mode == "strict-race")
        {
            async Task<(StoredArtifact? Receipt, Exception? Failure)> Try(ICreateNewArtifactStore target)
            { StoredArtifact? receipt = null; var error = await Record.ExceptionAsync(async () => receipt = await target.WriteNewAsync(input)); return (receipt, error); }
            var attempts = await Task.WhenAll(Task.Run(() => Try(store)), Task.Run(() => Try(harness.Peer)));
            Assert.Single(attempts, a => a.Receipt is not null && a.Failure is null);
            var loser = Assert.Single(attempts, a => a.Failure is not null); Assert.IsType<IOException>(loser.Failure);
            Assert.Equal(Abc, (await store.ReadAsync(c.Path)).ToArray()); return;
        }
        if (c.Mode == "stream-race")
        {
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var first = ((IStreamingArtifactStore)store).WriteStreamAsync(c.Path, Media, async (stream, token) =>
            { await stream.WriteAsync(new byte[] { (byte)'a' }, token); entered.SetResult(); await release.Task; await stream.WriteAsync(new byte[] { (byte)'b', (byte)'c' }, token); });
            await Task.WhenAny(entered.Task, first);
            if (!entered.Task.IsCompletedSuccessfully) { await first; throw new InvalidOperationException("Stream writer did not enter fixture."); }
            Exception? rejection;
            try { rejection = await Record.ExceptionAsync(() => harness.Peer.WriteNewAsync(input)); }
            finally { release.SetResult(); }
            await first; Assert.IsType<IOException>(rejection); Assert.Equal(Abc, (await store.ReadAsync(c.Path)).ToArray()); return;
        }
        StoredArtifact? result = null;
        var error = await Record.ExceptionAsync(async () => result = c.Mode == "ordinary-after-strict"
            ? await store.WriteAsync(input, cancellation.Token) : await store.WriteNewAsync(input, cancellation.Token));
        if (original is not null)
        {
            Assert.IsType<IOException>(error); Assert.Null(result); Assert.Equal(original, (await store.ReadAsync(c.Path)).ToArray()); return;
        }
        if (c.Mode is "invalid" or "root-link" or "ancestor-link" or "leaf-link" or "dangling-link" or "directory")
        {
            Assert.NotNull(error); Assert.Null(result);
            if (c.Mode == "directory") Assert.True(error is IOException or UnauthorizedAccessException);
            else Assert.IsType<ArgumentException>(error);
            if (c.Mode != "invalid") Assert.Equal(Abc, harness.ReadFixture());
            return;
        }
        if (c.Mode is "cancel-before" or "cancel-hash" or "hash-failure")
        {
            Assert.NotNull(error); Assert.Null(result); Assert.False(harness.Exists(c.Path));
            if (c.Mode == "hash-failure") { Assert.IsType<InvalidOperationException>(error); Assert.Equal("hash-fixture", error!.Message); }
            else Assert.IsAssignableFrom<OperationCanceledException>(error);
            return;
        }
        Assert.Null(error); Assert.NotNull(result);
        Assert.Equal(c.Path, result!.RelativePath); Assert.Equal(Media, result.MediaType); Assert.Equal(input.Bytes.Length, result.Length);
        Assert.Equal(c.Mode == "empty" ? ArtifactStoreCases.EmptySha : ArtifactStoreCases.AbcSha, result.Sha256);
        Assert.Equal(input.Bytes.ToArray(), (await store.ReadAsync(c.Path)).ToArray());
    }
}

public sealed class ScriptedCreateNewHarness(string? mutation = null) : ICreateNewHarness
{
    private readonly State state = new();
    public ICreateNewArtifactStore Store => new ScriptedStore(state, mutation);
    public ICreateNewArtifactStore Peer => new ScriptedStore(state, mutation);
    public void Configure(string mode, CancellationTokenSource cancellation) { state.Mode = mode; state.Cancellation = cancellation; }
    public void PrepareFilesystem(string mode) { state.Boundary = mode; state.Fixture = CreateNewArtifactCases.Abc.ToArray(); }
    public bool Exists(string path) { lock (state) return state.Files.ContainsKey(path); }
    public byte[] ReadFixture() => state.Fixture;
    private sealed class State
    {
        public readonly Dictionary<string, byte[]> Files = new(StringComparer.Ordinal);
        public string Mode = ""; public string? Boundary; public CancellationTokenSource? Cancellation;
        public byte[] Fixture = CreateNewArtifactCases.Abc.ToArray();
    }
    private sealed class ScriptedStore(State state, string? mutation) : ICreateNewArtifactStore, IStreamingArtifactStore
    {
        public Task<ReadOnlyMemory<byte>> ReadAsync(string path, CancellationToken token = default)
        { lock (state) return Task.FromResult<ReadOnlyMemory<byte>>(mutation == "bytes" ? new byte[] { 255 } : state.Files[path]); }
        public Task<StoredArtifact> WriteAsync(Artifact artifact, CancellationToken token = default) => Write(artifact, token, false);
        public Task<StoredArtifact> WriteNewAsync(Artifact artifact, CancellationToken token = default) => Write(artifact, token, true);
        private Task<StoredArtifact> Write(Artifact artifact, CancellationToken token, bool strict)
        {
            if (mutation != "path") ArtifactPaths.Segments(artifact.RelativePath);
            if (strict && state.Boundary is not null)
            {
                if (mutation == "fixture-overwrite") state.Fixture = [255];
                if (mutation != "boundary") { if (state.Boundary == "directory") throw new IOException("Directory retained."); throw new ArgumentException("Link retained."); }
            }
            if (strict && state.Mode is "cancel-before" or "cancel-hash" or "hash-failure")
            {
                if (state.Mode == "cancel-hash") state.Cancellation!.Cancel();
                if (mutation == "failure-writes") { lock (state) state.Files[artifact.RelativePath] = artifact.Bytes.ToArray(); }
                if (state.Mode == "hash-failure") { if (mutation != "failure-success") throw new InvalidOperationException("hash-fixture"); }
                else if (mutation != "cancel") token.ThrowIfCancellationRequested();
            }
            else token.ThrowIfCancellationRequested();
            lock (state)
            {
                if (state.Files.TryGetValue(artifact.RelativePath, out var previous))
                {
                    if (strict)
                    {
                        if (mutation is "overwrite-original" or "race-overwrite") state.Files[artifact.RelativePath] = [255];
                        if (mutation is not ("existing-success" or "race-success")) throw new IOException("Existing destination retained.");
                    }
                    else if (!previous.AsSpan().SequenceEqual(artifact.Bytes.Span)) throw new IOException("Existing destination retained.");
                }
                else state.Files[artifact.RelativePath] = artifact.Bytes.ToArray();
            }
            return Task.FromResult(new StoredArtifact(mutation == "receipt-path" ? "wrong" : artifact.RelativePath,
                mutation == "receipt-hash" ? "wrong" : artifact.Bytes.Length == 0 ? ArtifactStoreCases.EmptySha : ArtifactStoreCases.AbcSha,
                mutation == "receipt-length" ? -1 : artifact.Bytes.Length, mutation == "receipt-media" ? "wrong" : artifact.MediaType));
        }
        public async Task<StoredArtifact> WriteStreamAsync(string path, string media, Func<Stream, CancellationToken, Task> producer, CancellationToken token = default)
        {
            lock (state) { if (state.Files.ContainsKey(path)) throw new IOException("Mock stream fixture must be fresh."); state.Files[path] = []; }
            using var bytes = new MemoryStream();
            try { await producer(bytes, token); }
            finally { lock (state) { if (mutation != "race-overwrite" || !state.Files[path].AsSpan().SequenceEqual(new byte[] { 255 })) state.Files[path] = bytes.ToArray(); } }
            return new(path, ArtifactStoreCases.AbcSha, bytes.Length, media);
        }
    }
}

[Trait("Gate", "Mock"), Trait("Requirement", "TEST-BUILD-007")]
public sealed class CreateNewArtifactMockTests
{
    [Theory, MemberData(nameof(CreateNewArtifactCases.Cases), MemberType = typeof(CreateNewArtifactCases))]
    public Task CorrectMock(CreateNewCase c) => CreateNewArtifactCases.AssertContract(c, new ScriptedCreateNewHarness());
    [Theory, MemberData(nameof(CreateNewArtifactCases.Cases), MemberType = typeof(CreateNewArtifactCases))]
    public async Task WrongIndividualResponsesRejected(CreateNewCase c)
    { foreach (var mutation in CreateNewArtifactCases.Mutations(c)) Assert.NotNull(await Record.ExceptionAsync(() => CreateNewArtifactCases.AssertContract(c, new ScriptedCreateNewHarness(mutation)))); }
}
