using Microsoft.Win32.SafeHandles;

namespace AndroidMcp.BuildTool.Core;

// Deliberately bounded ABI set. Other Linux architectures do not silently use
// asm-generic open constants. Windows structures still need their ABI/runtime
// proof; injected Windows outcomes never establish execution on Windows.
public enum NativeArtifactAbi { LinuxX64, LinuxArm64, Windows }
public sealed record WindowsArtifactOpenRequest(string Path,uint DesiredAccess,uint ShareMode,
    uint CreationDisposition,uint FlagsAndAttributes,bool InheritHandle);

// Sole low-level syscall SPI beneath one adapter implementing the EXISTING
// INativeArtifactReadOpenCalls + INativeArtifactIdentityCalls. No filesystem,
// hash, path-admission or platform-field mapper is reimplemented here.
// Production kernel implementation/PInvoke must wait for this mock gate.
public interface INativeArtifactKernel
{
    NativeArtifactAbi? Abi {get;}
    SafeFileHandle OpenLinux(string path,int nativeFlags);
    SafeFileHandle OpenWindows(WindowsArtifactOpenRequest request);
    LinuxStatxIdentity Statx(SafeFileHandle borrowedHandle,string relativePath,int flags,uint mask);
    WindowsFileIdentity ReadWindowsIdentity(SafeFileHandle borrowedHandle);
}
