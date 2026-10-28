using Nuke.Common.IO;
namespace AndroidMcp.BuildTool.Core;

/// <summary>Immutable generation store. Originals and failed partial writes are retained.</summary>
public sealed class FileArtifactStore : IStreamingArtifactStore
{
    private readonly AbsolutePath root;
    private readonly Func<ReadOnlyMemory<byte>, string> sha256;
    private readonly Func<Stream, string> streamSha256;
    public FileArtifactStore(string rootDirectory, Func<ReadOnlyMemory<byte>, string> sha256,
        Func<Stream, string>? streamSha256 = null)
    {
        root = (AbsolutePath)Path.GetFullPath(rootDirectory);
        this.sha256 = sha256;
        this.streamSha256 = streamSha256 ?? ContentHash.Sha256;
    }

    public Task<StoredArtifact> WriteAsync(Artifact artifact, CancellationToken cancellationToken = default)
    {
        Resolve(artifact.RelativePath);
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = artifact.Bytes.ToArray();
        var digest = sha256(bytes); // Preserve the existing byte-hash constructor seam.
        return WriteCoreAsync(artifact.RelativePath, artifact.MediaType,
            (output, token) => output.WriteAsync(bytes, token).AsTask(), cancellationToken, digest);
    }

    public Task<StoredArtifact> WriteStreamAsync(string relativePath, string mediaType,
        Func<Stream, CancellationToken, Task> producer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(producer);
        return WriteCoreAsync(relativePath, mediaType, producer, cancellationToken, null);
    }

    public async Task<ReadOnlyMemory<byte>> ReadAsync(string relativePath, CancellationToken cancellationToken = default) =>
        await File.ReadAllBytesAsync(Resolve(relativePath), cancellationToken);

    private async Task<StoredArtifact> WriteCoreAsync(string relativePath, string mediaType,
        Func<Stream, CancellationToken, Task> producer, CancellationToken token, string? byteDigest)
    {
        var path = Resolve(relativePath);
        token.ThrowIfCancellationRequested();
        FileStream destination;
        var compare = File.Exists(path);
        if (compare) destination = OpenExisting(path);
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            path = Resolve(relativePath); // Recheck ancestors after creating directories.
            try
            {
                destination = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite,
                    FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough);
            }
            catch (IOException) when (File.Exists(path))
            {
                // A competing creator owns the existing bytes. Opening/verification may
                // fail while its exclusive write is active; never truncate or replace it.
                path = Resolve(relativePath);
                destination = OpenExisting(path);
                compare = true;
            }
        }
        await using (destination)
        {
            using var output = new BorrowedWriteStream(destination, compare, relativePath);
            await producer(output, token);
            token.ThrowIfCancellationRequested();
            if (compare)
            {
                if (destination.ReadByte() != -1) throw Conflict(relativePath);
            }
            else
            {
                await destination.FlushAsync(token);
                destination.Flush(true);
            }
            token.ThrowIfCancellationRequested();
            destination.Position = 0;
            var digest = byteDigest ?? streamSha256(destination);
            token.ThrowIfCancellationRequested();
            return new(relativePath, digest, destination.Length, mediaType);
        }
    }

    private static FileStream OpenExisting(string path) => new(path, FileMode.Open, FileAccess.Read,
        FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
    private static IOException Conflict(string path) => new($"Existing artifact differs and was retained: {path}");
    private string Resolve(string relativePath) => ArtifactPaths.Resolve(root, relativePath);

    // Bounded comparison and writes share the same borrowed write-only producer facade.
    // It cannot expose or dispose the underlying immutable file and never buffers payloads.
    private sealed class BorrowedWriteStream(Stream target, bool compare, string relativePath) : Stream
    {
        private readonly byte[]? scratch = compare ? new byte[81920] : null;
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { if (!compare) target.Flush(); }
        public override Task FlushAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return compare ? Task.CompletedTask : target.FlushAsync(token);
        }
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (!compare) { target.Write(buffer); return; }
            while (!buffer.IsEmpty)
            {
                var comparison = scratch!;
                var read = target.Read(comparison.AsSpan(0, Math.Min(comparison.Length, buffer.Length)));
                if (read == 0 || !buffer[..read].SequenceEqual(comparison.AsSpan(0, read))) throw Conflict(relativePath);
                buffer = buffer[read..];
            }
        }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token) =>
            WriteAsync(buffer.AsMemory(offset, count), token).AsTask();
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (!compare) { await target.WriteAsync(buffer, token); return; }
            while (!buffer.IsEmpty)
            {
                var comparison = scratch!;
                var read = await target.ReadAsync(comparison.AsMemory(0, Math.Min(comparison.Length, buffer.Length)), token);
                if (read == 0 || !buffer.Span[..read].SequenceEqual(comparison.AsSpan(0, read))) throw Conflict(relativePath);
                buffer = buffer[read..];
            }
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
