using Microsoft.Win32.SafeHandles;

namespace AndroidMcp.BuildTool.Core;

// One admission/ownership adapter over the shared raw-open and identity seams.
// It has no syscall, path-policy, stream, hashing or platform-field mapper of
// its own. The owning Core store admits the absolute path before this call.
public sealed class ArtifactReadOpener : IArtifactReadOpener
{
    private readonly INativeArtifactReadOpenCalls calls;
    private readonly INativeArtifactIdentityReader identity;
    public ArtifactReadOpener(INativeArtifactReadOpenCalls calls,INativeArtifactIdentityReader identity)
    {
        this.calls=calls??throw new ArgumentNullException(nameof(calls));
        this.identity=identity??throw new ArgumentNullException(nameof(identity));
    }

    public AdmittedArtifactReadHandle OpenRead(string admittedAbsolutePath,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var platform=calls.Platform;
        if(platform is not(NativeArtifactPlatform.Linux or NativeArtifactPlatform.Windows))
            throw new PlatformNotSupportedException("Native artifact opening supports Linux and Windows only.");
        var flags=ArtifactReadOpenFlags.ReadOnly|ArtifactReadOpenFlags.NoFollow|ArtifactReadOpenFlags.CloseOnExec;
        if(platform==NativeArtifactPlatform.Linux)flags|=ArtifactReadOpenFlags.NonBlocking;
        SafeFileHandle? owned=null;
        var transferred=false;
        try{
            owned=platform==NativeArtifactPlatform.Linux
                ?calls.OpenLinux(admittedAbsolutePath,flags)
                :calls.OpenWindows(admittedAbsolutePath,flags);
            cancellationToken.ThrowIfCancellationRequested();
            if(owned is null||owned.IsInvalid||owned.IsClosed)
                throw new IOException("Native artifact opening did not return a valid owned handle.");
            var observed=identity.Observe(owned);
            cancellationToken.ThrowIfCancellationRequested();
            if(owned.IsInvalid||owned.IsClosed||observed.Platform!=platform||observed.Kind!=NativeArtifactKind.RegularFile)
                throw new IOException("Native artifact descriptor is not an admitted regular file.");
            var result=new AdmittedArtifactReadHandle(owned,observed);
            transferred=true;
            return result;
        }finally{if(!transferred)owned?.Dispose();}
    }
}
