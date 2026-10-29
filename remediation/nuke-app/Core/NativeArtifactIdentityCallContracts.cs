using Microsoft.Win32.SafeHandles;

namespace AndroidMcp.BuildTool.Core;

// Raw result seams for ONE native adapter, not another filesystem service.
// Default implementation performs platform calls; injected records prove the
// SAME mapper on Linux without claiming Windows runtime execution.
public sealed record LinuxStatxIdentity(uint Mask,ushort Mode,uint DeviceMajor,uint DeviceMinor,
    ulong Inode,ulong Size,long ModifiedSeconds,uint ModifiedNanoseconds);
public sealed record WindowsFileIdentity(uint FileType,bool Directory,ulong VolumeSerial,
    ulong FileIdLow,ulong FileIdHigh,long EndOfFile,long LastWriteFileTime,uint Attributes);
public interface INativeArtifactIdentityCalls
{
    // Null denotes an unsupported host; unknown enum values also fail closed.
    NativeArtifactPlatform? Platform { get; }
    LinuxStatxIdentity ReadLinux(SafeFileHandle borrowedHandle);
    WindowsFileIdentity ReadWindows(SafeFileHandle borrowedHandle);
}
