using System.Text;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed record StreamingArtifactCase(string Id, string Scenario, string Path = "exports/result.bin");

public static class StreamingArtifactCases
{
    public const string Media = "application/octet-stream";
    public static readonly byte[] Abc = Encoding.ASCII.GetBytes("abc");
    public static IEnumerable<object[]> Cases => new[] {
        new StreamingArtifactCase("STR-01", "chunks"),
        new StreamingArtifactCase("STR-02", "empty"),
        new StreamingArtifactCase("STR-03", "identical"),
        new StreamingArtifactCase("STR-04", "conflict-equal"),
        new StreamingArtifactCase("STR-05", "conflict-short"),
        new StreamingArtifactCase("STR-06", "conflict-long"),
        new StreamingArtifactCase("STR-07", "producer-failure"),
        new StreamingArtifactCase("STR-08", "cancel-before"),
        new StreamingArtifactCase("STR-09", "cancel-during"),
        new StreamingArtifactCase("STR-10", "byte-then-stream"),
        new StreamingArtifactCase("STR-11", "stream-then-byte"),
        new StreamingArtifactCase("STR-12", "invalid", "../escape"),
        new StreamingArtifactCase("STR-13", "invalid", "a/../escape"),
        new StreamingArtifactCase("STR-14", "invalid", "/absolute"),
        new StreamingArtifactCase("STR-15", "invalid", "C:/drive"),
        new StreamingArtifactCase("STR-16", "invalid", "a\\escape"),
        new StreamingArtifactCase("STR-17", "invalid", "a//escape"),
        new StreamingArtifactCase("STR-18", "invalid", ""),
        new StreamingArtifactCase("STR-19", "sync-chunks"),
        new StreamingArtifactCase("STR-20", "identical-failure"),
        new StreamingArtifactCase("STR-21", "large-chunks")
    }.Select(x => new object[] { x });

    public static string[] Mutations(StreamingArtifactCase c) => c.Scenario switch {
        "invalid" => ["path-admission"],
        "cancel-before" => ["cancel-before"],
        "cancel-during" => ["cancel-during", "partial-retention"],
        "producer-failure" => ["producer-failure", "partial-retention"],
        "identical-failure" => ["producer-failure", "original-retention"],
        "conflict-equal" or "conflict-short" or "conflict-long" => ["conflict", "original-retention"],
        _ => ["path", "hash", "length", "media", "bytes", "producer-count", "seekable"]
    };

    // This exact contract body binds to the native store after the independent mock gate.
    public static async Task AssertContract(StreamingArtifactCase c, IStreamingArtifactStore store)
    {
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var expected = c.Scenario == "empty" ? Array.Empty<byte>() : c.Scenario == "large-chunks" ? Enumerable.Repeat((byte)'a', 1048576).ToArray() : Abc;
        var old = c.Scenario is "identical" or "byte-then-stream" or "identical-failure" || c.Scenario.StartsWith("conflict", StringComparison.Ordinal);
        if (old) await store.WriteAsync(new Artifact(c.Path, Abc, Media));
        if (c.Scenario == "cancel-before") cancellation.Cancel();
        async Task Producer(Stream output, CancellationToken token)
        {
            calls++;
            Assert.True(output.CanWrite);
            Assert.False(output.CanSeek);
            Assert.False(output.CanRead);
            Assert.Equal(cancellation.Token, token);
            if (c.Scenario == "empty") return;
            if (c.Scenario == "large-chunks")
            {
                var chunk = Enumerable.Repeat((byte)'a', 4096).ToArray();
                for (var i = 0; i < 256; i++) await output.WriteAsync(chunk, token);
                return;
            }
            if (c.Scenario == "sync-chunks") { output.Write(Abc, 0, 1); output.Write(Abc, 1, 2); return; }
            var bytes = c.Scenario switch {
                "conflict-equal" => Encoding.ASCII.GetBytes("xyz"),
                "conflict-short" => Encoding.ASCII.GetBytes("ab"),
                "conflict-long" => Encoding.ASCII.GetBytes("abcd"),
                _ => Abc
            };
            await output.WriteAsync(bytes.AsMemory(0, 1), token);
            if (c.Scenario is "producer-failure" or "identical-failure") throw new InvalidOperationException("producer-fixture");
            if (c.Scenario == "cancel-during") { cancellation.Cancel(); token.ThrowIfCancellationRequested(); }
            await output.WriteAsync(bytes.AsMemory(1), token);
        }
        StoredArtifact? receipt = null;
        var failure = await Record.ExceptionAsync(async () => receipt = await store.WriteStreamAsync(c.Path, Media, Producer, cancellation.Token));
        if (c.Scenario is "invalid" or "cancel-before")
        {
            Assert.NotNull(failure);
            if (c.Scenario == "invalid") Assert.IsType<ArgumentException>(failure);
            else Assert.IsAssignableFrom<OperationCanceledException>(failure);
            Assert.Null(receipt); Assert.Equal(0, calls); return;
        }
        Assert.Equal(1, calls);
        if (c.Scenario.StartsWith("conflict", StringComparison.Ordinal) || c.Scenario is "producer-failure" or "identical-failure" or "cancel-during")
        {
            Assert.NotNull(failure); Assert.Null(receipt);
            if (c.Scenario.StartsWith("conflict", StringComparison.Ordinal)) Assert.IsType<IOException>(failure);
            else if (c.Scenario == "cancel-during") Assert.IsAssignableFrom<OperationCanceledException>(failure);
            else { Assert.IsType<InvalidOperationException>(failure); Assert.Equal("producer-fixture", failure!.Message); }
            Assert.Equal(old ? Abc : new byte[] { (byte)'a' }, (await store.ReadAsync(c.Path)).ToArray());
            return;
        }
        Assert.Null(failure); Assert.NotNull(receipt);
        Assert.Equal(c.Path, receipt!.RelativePath);
        Assert.Equal(Media, receipt.MediaType);
        Assert.Equal(expected.LongLength, receipt.Length);
        Assert.Equal(expected.Length == 0 ? ArtifactStoreCases.EmptySha : c.Scenario == "large-chunks" ? "9bc1b2a288b26af7257a36277ae3816a7d4f16e89c1e7e77d0a5c48bad62b360" : ArtifactStoreCases.AbcSha, receipt.Sha256);
        Assert.Equal(expected, (await store.ReadAsync(c.Path)).ToArray());
        if (c.Scenario == "stream-then-byte")
            Assert.Equal(receipt, await store.WriteAsync(new Artifact(c.Path, expected, Media)));
    }
}

// Scripted stateful mock. Its buffering is test-only, never the production streaming engine.
public sealed class ScriptedStreamingArtifactStore(string? mutation = null) : IStreamingArtifactStore
{
    private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);
    public Task<ReadOnlyMemory<byte>> ReadAsync(string path, CancellationToken token = default) =>
        Task.FromResult<ReadOnlyMemory<byte>>(mutation == "bytes" ? new byte[] { 255 } : files[path]);
    public Task<StoredArtifact> WriteAsync(Artifact artifact, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (files.TryGetValue(artifact.RelativePath, out var existing) && !existing.AsSpan().SequenceEqual(artifact.Bytes.Span))
            throw new IOException("Existing artifact retained.");
        files[artifact.RelativePath] = artifact.Bytes.ToArray();
        return Task.FromResult(new StoredArtifact(artifact.RelativePath,
            artifact.Bytes.Length == 0 ? ArtifactStoreCases.EmptySha : ArtifactStoreCases.AbcSha, artifact.Bytes.Length, artifact.MediaType));
    }
    public async Task<StoredArtifact> WriteStreamAsync(string path, string mediaType,
        Func<Stream, CancellationToken, Task> producer, CancellationToken token = default)
    {
        if (mutation != "path-admission") ArtifactPaths.Segments(path);
        if (mutation != "cancel-before") token.ThrowIfCancellationRequested();
        var original = files.TryGetValue(path, out var old) ? old : null;
        using var data = new MemoryStream();
        using var borrowed = new MockWriteOnlyStream(data, mutation == "seekable");
        try
        {
            await producer(borrowed, token);
            if (mutation == "producer-count") await producer(borrowed, token);
        }
        catch (OperationCanceledException) when (mutation == "cancel-during") { }
        catch (InvalidOperationException) when (mutation == "producer-failure") { }
        catch
        {
            if (original is null && mutation != "partial-retention") files[path] = data.ToArray();
            if (original is not null && mutation == "original-retention") files[path] = data.ToArray();
            throw;
        }
        var bytes = data.ToArray();
        if (original is not null && !original.AsSpan().SequenceEqual(bytes))
        {
            if (mutation == "original-retention") files[path] = bytes;
            if (mutation != "conflict") throw new IOException("Existing artifact retained.");
        }
        else files[path] = bytes;
        return new(mutation == "path" ? "wrong" : path,
            mutation == "hash" ? "wrong" : bytes.Length == 0 ? ArtifactStoreCases.EmptySha : bytes.Length == 1048576 ? "9bc1b2a288b26af7257a36277ae3816a7d4f16e89c1e7e77d0a5c48bad62b360" : ArtifactStoreCases.AbcSha,
            mutation == "length" ? -1 : bytes.LongLength, mutation == "media" ? "wrong" : mediaType);
    }
    private sealed class MockWriteOnlyStream(Stream destination, bool seekable) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => seekable;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => destination.Flush();
        public override Task FlushAsync(CancellationToken token) => destination.FlushAsync(token);
        public override void Write(byte[] buffer, int offset, int count) => destination.Write(buffer, offset, count);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default) => destination.WriteAsync(buffer, token);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}

[Trait("Gate", "Mock"), Trait("Requirement", "TEST-BUILD-007")]
public sealed class StreamingArtifactMockTests
{
    [Theory, MemberData(nameof(StreamingArtifactCases.Cases), MemberType = typeof(StreamingArtifactCases))]
    public Task CorrectMock(StreamingArtifactCase c) => StreamingArtifactCases.AssertContract(c, new ScriptedStreamingArtifactStore());

    [Theory, MemberData(nameof(StreamingArtifactCases.Cases), MemberType = typeof(StreamingArtifactCases))]
    public async Task WrongIndividualResponsesRejected(StreamingArtifactCase c)
    {
        foreach (var mutation in StreamingArtifactCases.Mutations(c))
            Assert.NotNull(await Record.ExceptionAsync(() => StreamingArtifactCases.AssertContract(c, new ScriptedStreamingArtifactStore(mutation))));
    }
}
