using Microsoft.Win32.SafeHandles;

namespace AndroidMcp.BuildTool.Core;

public sealed record ArtifactTreeEntry(string RelativePath, NativeArtifactKind Kind, long Length, NativeArtifactObservation? Native);
public interface IArtifactTreeReader
{
    // Selected scope directory is not emitted. Paths remain workspace-root-relative.
    // Retain .git entry metadata, never descend there. Never read file content.
    // Observed links are metadata-only; detected directory changes fail the read.
    // This preserves original traversal scope, not hostile rename/ABA immunity.
    Task<IReadOnlyList<ArtifactTreeEntry>> EnumerateAsync(string? relativeDirectory = null, bool recursive = true, CancellationToken cancellationToken = default);
}
public enum ArtifactPermissionOperation { AddExecuteBits, SetMode0755 }
public sealed record ArtifactPermissionRequest(ArtifactReadSnapshot Expected, ArtifactPermissionOperation Operation, StoredArtifact? ExpectedArtifact = null);
public sealed record ArtifactPermissionResult(ArtifactReadSnapshot Artifact, UnixFileMode PreviousMode, UnixFileMode AppliedMode);
public interface IArtifactPermissionEditor
{
    // Existing regular files/directories only. Byte pins are regular-file only.
    // Pin/hash and mode change address the SAME admitted owned native handle.
    Task<ArtifactPermissionResult> ApplyAsync(ArtifactPermissionRequest request, CancellationToken cancellationToken = default);
}

// Narrow BCL SPI for these two capabilities. Native open/identity/path/hash
// authorities remain the existing Core services; no new syscalls are introduced.
public interface IArtifactWorkspaceCalls
{
    IReadOnlyList<string> Enumerate(string admittedDirectory);
    FileAttributes Attributes(string admittedPath);
    UnixFileMode GetMode(SafeFileHandle borrowedHandle);
    void SetMode(SafeFileHandle borrowedHandle, UnixFileMode mode);
    string HashBorrowed(SafeFileHandle borrowedHandle);
}
