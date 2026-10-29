using Microsoft.Win32.SafeHandles;

namespace AndroidMcp.BuildTool.Core;

[Flags]
public enum ArtifactReadOpenFlags
{
    ReadOnly=1, NoFollow=2, NonBlocking=4, CloseOnExec=8
}

// The sole Core native adapter implements this narrow raw-call seam alongside
// native identity calls. These are portable intents, NOT OS numeric constants.
// Linux: O_RDONLY|O_NOFOLLOW|O_NONBLOCK|O_CLOEXEC. Windows: OPEN_EXISTING,
// read-only access, no inheritance, open the reparse point rather than follow.
public interface INativeArtifactReadOpenCalls
{
    NativeArtifactPlatform? Platform {get;}
    // Ownership transfers to the opener on success. Errors throw; never return
    // an invalid/closed handle as successful admission. No create/write/delete.
    SafeFileHandle OpenLinux(string admittedAbsolutePath,ArtifactReadOpenFlags flags);
    SafeFileHandle OpenWindows(string admittedAbsolutePath,ArtifactReadOpenFlags flags);
}

public sealed record AdmittedArtifactReadHandle(SafeFileHandle Handle,NativeArtifactObservation Observation);
public interface IArtifactReadOpener
{
    // Input already passed ArtifactPaths.Resolve. No path-policy duplicate here.
    // The opener rejects missing/unsupported/nonregular and closes every owned
    // handle on failure/cancellation. Identity Observe borrows the opened handle.
    // Only a RegularFile observation transfers that SAME still-open handle to
    // the caller, who owns disposal. Construct FileStream only after this return.
    // Linux nonblocking/no-follow flags are required BEFORE descriptor probing.
    AdmittedArtifactReadHandle OpenRead(string admittedAbsolutePath,CancellationToken cancellationToken=default);
}
