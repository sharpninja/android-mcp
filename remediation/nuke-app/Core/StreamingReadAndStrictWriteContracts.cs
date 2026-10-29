using Microsoft.Win32.SafeHandles;

namespace AndroidMcp.BuildTool.Core;

// Proposed additive declarations only. Native implementations require a mock gate.
// The SAME FileArtifactStore implements both capabilities and retains old APIs.
public interface ICreateNewStreamingArtifactStore : ICreateNewArtifactStore, IStreamingArtifactStore
{
    // Producer at most once; reject every existing destination, even identical.
    // Failure/cancel returns no receipt and retains partial bytes. No Move/delete.
    Task<StoredArtifact> WriteNewStreamAsync(string relativePath, string mediaType,
        Func<Stream, CancellationToken, Task> producer, CancellationToken cancellationToken = default);
}

public enum NativeArtifactPlatform { Linux, Windows }
public enum NativeArtifactKind { RegularFile, Directory, Other }
public sealed record NativeArtifactObservation(NativeArtifactPlatform Platform, NativeArtifactKind Kind,
    ulong DeviceOrVolume, ulong FileIdLow, ulong FileIdHigh, long Length,
    long ModifiedSeconds, uint ModifiedNanoseconds);
public interface INativeArtifactIdentityReader
{
    // Borrowed handle: use safe lifetime pinning, never close/dispose/take ownership.
    // Exact statx / WindowsFileId fields; unsupported hosts and syscall errors throw.
    NativeArtifactObservation Observe(SafeFileHandle borrowedHandle);
}
public sealed record ArtifactReadSnapshot(string RelativePath, NativeArtifactObservation Native);
public interface IStreamingArtifactReader
{
    // Null means verified missing only. Reparse/nonregular/admission/IO errors throw.
    Task<ArtifactReadSnapshot?> InspectAsync(string relativePath, CancellationToken cancellationToken = default);
    Task<IArtifactReadSession> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);
}
public interface IArtifactReadSession : IAsyncDisposable
{
    // Session owns its admitted stream. Content is borrowed and read-only;
    // consumers do not dispose it or gain access to the native file handle.
    Stream Content { get; }
    ArtifactReadSnapshot Opened { get; }
    // Re-admit/open current path through the same Core store and identity adapter.
    // Callers compare complete observations; a missing/nonregular path throws.
    Task<ArtifactReadSnapshot> ObserveCurrentPathAsync(CancellationToken cancellationToken = default);
}
