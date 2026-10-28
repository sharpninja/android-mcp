using Nuke.Common.IO;
namespace AndroidMcp.BuildTool.Core;

/// <summary>Immutable generation store. Originals and failed partial writes are retained.</summary>
public sealed class FileArtifactStore : IArtifactStore
{
    private readonly AbsolutePath root;
    private readonly Func<ReadOnlyMemory<byte>,string> sha256;
    public FileArtifactStore(string rootDirectory,Func<ReadOnlyMemory<byte>,string> sha256)
    {
        root=(AbsolutePath)Path.GetFullPath(rootDirectory);
        this.sha256=sha256;
    }
    public async Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default)
    {
        var path=Resolve(artifact.RelativePath);
        cancellationToken.ThrowIfCancellationRequested();
        var bytes=artifact.Bytes.ToArray();
        var digest=sha256(bytes);
        if(File.Exists(path))return await VerifyExisting(path,artifact,bytes,digest,cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        path=Resolve(artifact.RelativePath); // reject newly introduced reparse components
        FileStream stream;
        try { stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,FileOptions.Asynchronous|FileOptions.WriteThrough); }
        catch(IOException) when(File.Exists(path)) { return await VerifyExisting(path,artifact,bytes,digest,cancellationToken); }
        await using(stream)
        {
            await stream.WriteAsync(bytes,cancellationToken);
            await stream.FlushAsync(cancellationToken);
            stream.Flush(true);
        }
        return new(artifact.RelativePath,digest,bytes.LongLength,artifact.MediaType);
    }
    public async Task<ReadOnlyMemory<byte>> ReadAsync(string relativePath,CancellationToken cancellationToken=default) =>
        await File.ReadAllBytesAsync(Resolve(relativePath),cancellationToken);

    private async Task<StoredArtifact> VerifyExisting(string path,Artifact artifact,byte[] bytes,string digest,CancellationToken token)
    {
        var existing=await File.ReadAllBytesAsync(path,token);
        if(!existing.AsSpan().SequenceEqual(bytes))throw new IOException($"Existing artifact differs and was retained: {artifact.RelativePath}");
        return new(artifact.RelativePath,digest,existing.LongLength,artifact.MediaType);
    }
    private string Resolve(string relativePath)
    {
        return ArtifactPaths.Resolve(root,relativePath);
    }
}
