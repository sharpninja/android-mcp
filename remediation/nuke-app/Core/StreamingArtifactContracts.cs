namespace AndroidMcp.BuildTool.Core;

/// <summary>Additive streaming writes using the same immutable artifact namespace as byte writes.</summary>
public interface IStreamingArtifactStore : IArtifactStore
{
    // Producer is called at most once, with a writable non-seekable borrowed stream.
    // The producer must not dispose that stream. It may write synchronously or asynchronously.
    // Success means all bytes were flushed and matched a new or identical immutable artifact.
    // Exceptions/cancellation never yield a StoredArtifact; originals and partial new files remain.
    Task<StoredArtifact> WriteStreamAsync(string relativePath, string mediaType,
        Func<Stream, CancellationToken, Task> producer, CancellationToken cancellationToken = default);
}
