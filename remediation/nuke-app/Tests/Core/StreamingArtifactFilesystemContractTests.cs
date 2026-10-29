using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public interface IStreamingFilesystemHarness
{
    IStreamingArtifactStore Store { get; }
    IStreamingArtifactStore Peer { get; }
    void PrepareLink(string kind);
}

public static class StreamingFilesystemCases
{
    public static IEnumerable<object[]> Cases => new[] { "root-link", "ancestor-link", "leaf-link", "dangling-link", "create-race" }.Select(x => new object[] { x });
    public static async Task AssertContract(string scenario, IStreamingFilesystemHarness harness)
    {
        const string path = "exports/result.bin";
        if (scenario != "create-race")
        {
            harness.PrepareLink(scenario);
            var calls = 0;
            var error = await Record.ExceptionAsync(() => harness.Store.WriteStreamAsync(path, StreamingArtifactCases.Media,
                (_, _) => { calls++; return Task.CompletedTask; }));
            Assert.IsType<ArgumentException>(error); Assert.Equal(0, calls); return;
        }
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callsFirst = 0; var callsSecond = 0;
        var first = harness.Store.WriteStreamAsync(path, StreamingArtifactCases.Media, async (stream, token) =>
        {
            callsFirst++;
            await stream.WriteAsync(new byte[] { (byte)'a' }, token);
            entered.SetResult();
            await release.Task;
            await stream.WriteAsync(new byte[] { (byte)'b', (byte)'c' }, token);
        });
        await entered.Task;
        Exception? loser;
        try
        {
            loser = await Record.ExceptionAsync(() => harness.Peer.WriteStreamAsync(path, StreamingArtifactCases.Media,
                async (stream, token) => { callsSecond++; await stream.WriteAsync(new byte[] { (byte)'x' }, token); }));
        }
        finally { release.SetResult(); }
        var winner = await first;
        Assert.IsType<IOException>(loser);
        Assert.InRange(callsSecond, 0, 1); Assert.Equal(1, callsFirst);
        Assert.Equal(3, winner.Length); Assert.Equal(ArtifactStoreCases.AbcSha, winner.Sha256);
        Assert.Equal(StreamingArtifactCases.Abc, (await harness.Store.ReadAsync(path)).ToArray());
    }
}

public sealed class ScriptedStreamingFilesystemHarness(string? mutation = null) : IStreamingFilesystemHarness
{
    private readonly StoreScript script = new(mutation);
    public IStreamingArtifactStore Store => script;
    public IStreamingArtifactStore Peer => script;
    public void PrepareLink(string kind) => script.Linked = true;
    private sealed class StoreScript(string? mutation) : IStreamingArtifactStore
    {
        public bool Linked { get; set; }
        private bool reserved;
        private byte[] bytes = [];
        public Task<ReadOnlyMemory<byte>> ReadAsync(string path, CancellationToken token = default) => Task.FromResult<ReadOnlyMemory<byte>>(bytes);
        public Task<StoredArtifact> WriteAsync(Artifact artifact, CancellationToken token = default) => throw new NotSupportedException();
        public async Task<StoredArtifact> WriteStreamAsync(string path, string mediaType, Func<Stream, CancellationToken, Task> producer, CancellationToken token = default)
        {
            if (Linked && mutation != "link-admission") throw new ArgumentException("Link rejected before producer.");
            if (reserved && mutation != "exclusive-create") throw new IOException("Immutable generation already reserved.");
            reserved = true;
            using var output = new MemoryStream();
            await producer(output, token);
            bytes = output.ToArray();
            return new(path, ArtifactStoreCases.AbcSha, bytes.Length, mediaType);
        }
    }
}

[Trait("Gate", "Mock"), Trait("Requirement", "TEST-BUILD-007")]
public sealed class StreamingArtifactFilesystemMockTests
{
    [Theory, MemberData(nameof(StreamingFilesystemCases.Cases), MemberType = typeof(StreamingFilesystemCases))]
    public Task CorrectMock(string scenario) => StreamingFilesystemCases.AssertContract(scenario, new ScriptedStreamingFilesystemHarness());
    [Theory, MemberData(nameof(StreamingFilesystemCases.Cases), MemberType = typeof(StreamingFilesystemCases))]
    public async Task WrongIndividualResponseRejected(string scenario) => Assert.NotNull(await Record.ExceptionAsync(() =>
        StreamingFilesystemCases.AssertContract(scenario, new ScriptedStreamingFilesystemHarness(scenario == "create-race" ? "exclusive-create" : "link-admission"))));
}
