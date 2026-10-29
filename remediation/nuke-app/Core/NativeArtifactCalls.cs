using Microsoft.Win32.SafeHandles;

namespace AndroidMcp.BuildTool.Core;

/// <summary>One portable-intent adapter over the admitted kernel ABI. Native
/// field interpretation remains owned by NativeArtifactIdentityReader.</summary>
public sealed class NativeArtifactCalls : INativeArtifactReadOpenCalls, INativeArtifactIdentityCalls
{
    private readonly INativeArtifactKernel kernel;
    public NativeArtifactCalls(INativeArtifactKernel kernel)=>
        this.kernel=kernel??throw new ArgumentNullException(nameof(kernel));

    public NativeArtifactPlatform? Platform=>kernel.Abi switch{
        NativeArtifactAbi.LinuxX64 or NativeArtifactAbi.LinuxArm64=>NativeArtifactPlatform.Linux,
        NativeArtifactAbi.Windows=>NativeArtifactPlatform.Windows,
        _=>null
    };

    public SafeFileHandle OpenLinux(string admittedAbsolutePath,ArtifactReadOpenFlags flags)
    {
        RequirePlatform(NativeArtifactPlatform.Linux);
        const ArtifactReadOpenFlags required=ArtifactReadOpenFlags.ReadOnly|ArtifactReadOpenFlags.NoFollow|
            ArtifactReadOpenFlags.NonBlocking|ArtifactReadOpenFlags.CloseOnExec;
        RequireFlags(flags,required);
        ArgumentNullException.ThrowIfNull(admittedAbsolutePath);
        // The admitted x64/arm64 ABIs share these UAPI constants: O_RDONLY=0,
        // O_NOFOLLOW=0x20000, O_NONBLOCK=0x800, O_CLOEXEC=0x80000.
        return kernel.OpenLinux(admittedAbsolutePath,0xa0800);
    }

    public SafeFileHandle OpenWindows(string admittedAbsolutePath,ArtifactReadOpenFlags flags)
    {
        RequirePlatform(NativeArtifactPlatform.Windows);
        const ArtifactReadOpenFlags required=ArtifactReadOpenFlags.ReadOnly|ArtifactReadOpenFlags.NoFollow|
            ArtifactReadOpenFlags.CloseOnExec;
        RequireFlags(flags,required);
        ArgumentNullException.ThrowIfNull(admittedAbsolutePath);
        // GENERIC_READ, FILE_SHARE_READ, OPEN_EXISTING, OPEN_REPARSE_POINT |
        // BACKUP_SEMANTICS. No inheritance and no overlapped I/O request.
        return kernel.OpenWindows(new WindowsArtifactOpenRequest(admittedAbsolutePath,
            0x80000000,1,3,0x02200000,false));
    }

    public LinuxStatxIdentity ReadLinux(SafeFileHandle borrowedHandle)
    {
        RequirePlatform(NativeArtifactPlatform.Linux);
        // AT_EMPTY_PATH addresses the SAME descriptor. Required raw fields are
        // TYPE|MTIME|INO|SIZE; semantic validation stays in the existing mapper.
        return Borrow(borrowedHandle,handle=>kernel.Statx(handle,"",0x1000,0x341));
    }

    public WindowsFileIdentity ReadWindows(SafeFileHandle borrowedHandle)
    {
        RequirePlatform(NativeArtifactPlatform.Windows);
        return Borrow(borrowedHandle,kernel.ReadWindowsIdentity);
    }

    private void RequirePlatform(NativeArtifactPlatform required)
    {
        if(Platform!=required)throw new PlatformNotSupportedException("Artifact call does not match an admitted kernel ABI.");
    }

    private static void RequireFlags(ArtifactReadOpenFlags actual,ArtifactReadOpenFlags required)
    {
        if(actual!=required)throw new ArgumentException("The exact read-only admission intents are required.",nameof(actual));
    }

    private static T Borrow<T>(SafeFileHandle borrowedHandle,Func<SafeFileHandle,T> call)
    {
        ArgumentNullException.ThrowIfNull(borrowedHandle);
        if(borrowedHandle.IsClosed)throw new ObjectDisposedException(nameof(borrowedHandle));
        if(borrowedHandle.IsInvalid)throw new ArgumentException("A valid borrowed file handle is required.",nameof(borrowedHandle));
        var acquired=false;
        try{
            borrowedHandle.DangerousAddRef(ref acquired);
            return call(borrowedHandle);
        }finally{if(acquired)borrowedHandle.DangerousRelease();}
    }
}
