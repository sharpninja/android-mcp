using Microsoft.Win32.SafeHandles;

namespace AndroidMcp.BuildTool.Core;

/// <summary>One native-field mapper. Platform calls are injected, never inferred
/// from the host when mapping an observation. Does not own borrowed handles.</summary>
public sealed class NativeArtifactIdentityReader : INativeArtifactIdentityReader
{
    private readonly INativeArtifactIdentityCalls calls;
    public NativeArtifactIdentityReader(INativeArtifactIdentityCalls calls)=>
        this.calls=calls??throw new ArgumentNullException(nameof(calls));

    public NativeArtifactObservation Observe(SafeFileHandle borrowedHandle)
    {
        ArgumentNullException.ThrowIfNull(borrowedHandle);
        if(borrowedHandle.IsClosed)throw new ObjectDisposedException(nameof(borrowedHandle));
        if(borrowedHandle.IsInvalid)throw new ArgumentException("A valid borrowed file handle is required.",nameof(borrowedHandle));
        var platform=calls.Platform;
        if(platform is not(NativeArtifactPlatform.Linux or NativeArtifactPlatform.Windows))
            throw new PlatformNotSupportedException("Native artifact identity is supported only for Linux and Windows.");
        var acquired=false;
        try{
            // Pin the SAME borrowed SafeHandle over the entire platform call.
            // Caller disposal during a call must not release its native handle.
            borrowedHandle.DangerousAddRef(ref acquired);
            return platform==NativeArtifactPlatform.Linux
                ? MapLinux(calls.ReadLinux(borrowedHandle))
                : MapWindows(calls.ReadWindows(borrowedHandle));
        }finally{if(acquired)borrowedHandle.DangerousRelease();}
    }

    private static NativeArtifactObservation MapLinux(LinuxStatxIdentity raw)
    {
        const uint required=0x341; // STATX_TYPE | STATX_MTIME | STATX_INO | STATX_SIZE
        if((raw.Mask&required)!=required||raw.Size>long.MaxValue||raw.ModifiedNanoseconds>=1_000_000_000)
            throw new IOException("Linux identity lacks required fields or contains invalid size/time.");
        var kind=(raw.Mode&0xf000) switch{
            0x8000=>NativeArtifactKind.RegularFile,
            0x4000=>NativeArtifactKind.Directory,
            0x1000 or 0x2000 or 0x6000 or 0xa000 or 0xc000=>NativeArtifactKind.Other,
            _=>throw new IOException("Linux identity contains an unknown file kind.")
        };
        return new(NativeArtifactPlatform.Linux,kind,((ulong)raw.DeviceMajor<<32)|raw.DeviceMinor,
            raw.Inode,0,(long)raw.Size,raw.ModifiedSeconds,raw.ModifiedNanoseconds);
    }

    private static NativeArtifactObservation MapWindows(WindowsFileIdentity raw)
    {
        if(raw.FileType!=1||raw.EndOfFile<0||raw.LastWriteFileTime<0)
            throw new IOException("Windows identity must be a disk handle with valid size/time.");
        // FILETIME is unsigned nonnegative ticks since 1601, with 100ns precision.
        // Floor division preserves pre-Unix-epoch seconds plus positive nanos.
        const long unixEpoch=116_444_736_000_000_000;
        var ticks=raw.LastWriteFileTime-unixEpoch;
        var seconds=Math.DivRem(ticks,10_000_000,out var remainder);
        if(remainder<0){seconds--;remainder+=10_000_000;}
        var kind=(raw.Attributes&0x400)!=0?NativeArtifactKind.Other:
            raw.Directory?NativeArtifactKind.Directory:NativeArtifactKind.RegularFile;
        return new(NativeArtifactPlatform.Windows,kind,raw.VolumeSerial,raw.FileIdLow,raw.FileIdHigh,
            raw.EndOfFile,seconds,(uint)(remainder*100));
    }
}
