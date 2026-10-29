using Nuke.Common.IO;
namespace AndroidMcp.BuildTool.Core;

/// <summary>Immutable generation store. Originals and failed partial writes are retained.</summary>
public sealed class FileArtifactStore : ICreateNewStreamingArtifactStore, IStreamingArtifactReader, ICreateNewArtifactDirectory
{
    private readonly AbsolutePath root;
    private readonly Func<ReadOnlyMemory<byte>, string> sha256;
    private readonly Func<Stream, string> streamSha256;
    private readonly IArtifactReadOpener? readOpener;
    public FileArtifactStore(string rootDirectory, Func<ReadOnlyMemory<byte>, string> sha256,
        Func<Stream, string>? streamSha256 = null) : this(rootDirectory,sha256,streamSha256,null) { }

    // Preserve the original three-argument constructor at both source and IL
    // level. The additive overload requires explicit reader composition.
    public FileArtifactStore(string rootDirectory, Func<ReadOnlyMemory<byte>, string> sha256,
        Func<Stream, string>? streamSha256, IArtifactReadOpener? readOpener)
    {
        root = (AbsolutePath)Path.GetFullPath(rootDirectory);
        this.sha256 = sha256;
        this.streamSha256 = streamSha256 ?? ContentHash.Sha256;
        this.readOpener = readOpener;
    }

    public Task CreateNewDirectoryAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var path = Resolve(relativePath);
        cancellationToken.ThrowIfCancellationRequested();
        var exists = true;
        try { _ = File.GetAttributes(path); }
        catch (FileNotFoundException) { exists = false; }
        catch (DirectoryNotFoundException) { exists = false; }
        if (exists) throw AlreadyExists(relativePath);
        // Absence plus creation is protected by the caller's cooperating
        // generation lease; it is not a hostile atomic-mkdir guarantee.
        Directory.CreateDirectory(path);
        _ = Resolve(relativePath);
        return Task.CompletedTask;
    }

    public Task<StoredArtifact> WriteAsync(Artifact artifact, CancellationToken cancellationToken = default) =>
        WriteBytesAsync(artifact, cancellationToken, false);

    public Task<StoredArtifact> WriteNewAsync(Artifact artifact, CancellationToken cancellationToken = default) =>
        WriteBytesAsync(artifact, cancellationToken, true);

    private Task<StoredArtifact> WriteBytesAsync(Artifact artifact, CancellationToken cancellationToken, bool requireNew)
    {
        Resolve(artifact.RelativePath);
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = artifact.Bytes.ToArray();
        var digest = sha256(bytes); // Preserve the existing byte-hash constructor seam.
        return WriteCoreAsync(artifact.RelativePath, artifact.MediaType,
            (output, token) => output.WriteAsync(bytes, token).AsTask(), cancellationToken, digest, requireNew);
    }

    public Task<StoredArtifact> WriteStreamAsync(string relativePath, string mediaType,
        Func<Stream, CancellationToken, Task> producer, CancellationToken cancellationToken = default) =>
        WriteProducedAsync(relativePath, mediaType, producer, cancellationToken, false);

    public Task<StoredArtifact> WriteNewStreamAsync(string relativePath, string mediaType,
        Func<Stream, CancellationToken, Task> producer, CancellationToken cancellationToken = default) =>
        WriteProducedAsync(relativePath, mediaType, producer, cancellationToken, true);

    private Task<StoredArtifact> WriteProducedAsync(string relativePath, string mediaType,
        Func<Stream, CancellationToken, Task> producer, CancellationToken cancellationToken, bool requireNew)
    {
        ArgumentNullException.ThrowIfNull(producer);
        return WriteCoreAsync(relativePath, mediaType, producer, cancellationToken, null, requireNew);
    }

    public async Task<ReadOnlyMemory<byte>> ReadAsync(string relativePath, CancellationToken cancellationToken = default) =>
        await File.ReadAllBytesAsync(Resolve(relativePath), cancellationToken);

    public Task<ArtifactReadSnapshot?> InspectAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path=Resolve(relativePath);
        try{
            var admitted=RequiredReadOpener().OpenRead(path,cancellationToken);
            using(admitted.Handle)return Task.FromResult<ArtifactReadSnapshot?>(new(relativePath,admitted.Observation));
        }catch(FileNotFoundException){return Task.FromResult<ArtifactReadSnapshot?>(null);}
    }

    public Task<IArtifactReadSession> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path=Resolve(relativePath);
        var admitted=RequiredReadOpener().OpenRead(path,cancellationToken);
        try{
            // Descriptor kind/identity has already been admitted by the one
            // shared opener. Do not construct FileStream before that boundary.
            var stream=new FileStream(admitted.Handle,FileAccess.Read,65536,isAsync:false);
            return Task.FromResult<IArtifactReadSession>(new ReadSession(this,relativePath,admitted.Observation,stream));
        }catch{admitted.Handle.Dispose();throw;}
    }

    private IArtifactReadOpener RequiredReadOpener()=>readOpener??throw new PlatformNotSupportedException(
        "Streaming reads require an explicitly admitted native read opener; no fallback is used.");

    private sealed class ReadSession : IArtifactReadSession
    {
        private readonly FileArtifactStore owner;
        private readonly string path;
        private readonly FileStream stream;
        private bool disposed;
        public ArtifactReadSnapshot Opened {get;}
        public Stream Content {get;}
        public ReadSession(FileArtifactStore owner,string path,NativeArtifactObservation observed,FileStream stream)
        {
            this.owner=owner;this.path=path;this.stream=stream;
            Opened=new(path,observed);Content=new BorrowedReadStream(this,stream);
        }
        public async Task<ArtifactReadSnapshot> ObserveCurrentPathAsync(CancellationToken cancellationToken=default)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            return await owner.InspectAsync(path,cancellationToken)
                ??throw new FileNotFoundException("The current artifact path is missing.",path);
        }
        public async ValueTask DisposeAsync()
        {
            if(disposed)return;
            disposed=true;
            await stream.DisposeAsync();
        }
        private sealed class BorrowedReadStream(ReadSession session,Stream target):Stream
        {
            private void Guard()=>ObjectDisposedException.ThrowIf(session.disposed,this);
            public override bool CanRead=>!session.disposed&&target.CanRead;
            public override bool CanSeek=>!session.disposed&&target.CanSeek;
            public override bool CanWrite=>false;
            public override long Length {get{Guard();return target.Length;}}
            public override long Position {get{Guard();return target.Position;}set{Guard();target.Position=value;}}
            public override int Read(byte[] buffer,int offset,int count){Guard();return target.Read(buffer,offset,count);}
            public override int Read(Span<byte> buffer){Guard();return target.Read(buffer);}
            public override Task<int> ReadAsync(byte[] buffer,int offset,int count,CancellationToken token)
            {Guard();token.ThrowIfCancellationRequested();return target.ReadAsync(buffer,offset,count,token);}
            public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token=default)
            {Guard();token.ThrowIfCancellationRequested();return target.ReadAsync(buffer,token);}
            public override long Seek(long offset,SeekOrigin origin){Guard();return target.Seek(offset,origin);}
            public override void Flush()=>Guard();
            public override Task FlushAsync(CancellationToken token){Guard();token.ThrowIfCancellationRequested();return Task.CompletedTask;}
            public override void SetLength(long value)=>throw new NotSupportedException();
            public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
            public override void Write(ReadOnlySpan<byte> buffer)=>throw new NotSupportedException();
            public override Task WriteAsync(byte[] buffer,int offset,int count,CancellationToken token)=>throw new NotSupportedException();
            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken token=default)=>throw new NotSupportedException();
            // Only the owning session closes its stream. Borrowed consumers
            // cannot dispose the descriptor or obtain its native handle.
            protected override void Dispose(bool disposing) { }
        }
    }

    private async Task<StoredArtifact> WriteCoreAsync(string relativePath, string mediaType,
        Func<Stream, CancellationToken, Task> producer, CancellationToken token, string? byteDigest, bool requireNew = false)
    {
        var path = Resolve(relativePath);
        token.ThrowIfCancellationRequested();
        FileStream destination;
        var compare = File.Exists(path);
        if (compare)
        {
            if (requireNew) throw AlreadyExists(relativePath);
            destination = OpenExisting(path);
        }
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
                if (requireNew) throw AlreadyExists(relativePath);
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
    private static IOException AlreadyExists(string path) => new($"Artifact already exists and was retained: {path}");
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
