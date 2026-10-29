using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace AndroidMcp.BuildTool.Core;

/// <summary>The sole concrete syscall adapter. This increment admits only
/// little-endian Linux x64/arm64; every other runtime fails closed. Windows
/// portable requests remain testable through the existing injected kernel SPI.</summary>
public sealed class NativeArtifactKernel : INativeArtifactKernel
{
    public NativeArtifactAbi? Abi=>OperatingSystem.IsLinux()&&BitConverter.IsLittleEndian&&IntPtr.Size==8
        ? RuntimeInformation.ProcessArchitecture switch{
            Architecture.X64=>NativeArtifactAbi.LinuxX64,
            Architecture.Arm64=>NativeArtifactAbi.LinuxArm64,
            _=>null
        }:null;

    public SafeFileHandle OpenLinux(string path,int nativeFlags)
    {
        RequireLinux();
        ArgumentException.ThrowIfNullOrEmpty(path);
        if(path.Contains('\0'))throw new ArgumentException("Native paths cannot contain NUL.",nameof(path));
        if(nativeFlags!=0xa0800)throw new ArgumentException("Only the admitted read-only native flags are supported.",nameof(nativeFlags));
        try{
            var descriptor=Open(path,nativeFlags);
            if(descriptor<0)throw NativeError("open",path,Marshal.GetLastPInvokeError());
            return new SafeFileHandle(new IntPtr(descriptor),ownsHandle:true);
        }catch(EntryPointNotFoundException error){throw Unavailable(error);}
        catch(DllNotFoundException error){throw Unavailable(error);}
    }

    public LinuxStatxIdentity Statx(SafeFileHandle borrowedHandle,string relativePath,int flags,uint mask)
    {
        RequireLinux();
        ArgumentNullException.ThrowIfNull(borrowedHandle);
        if(borrowedHandle.IsClosed)throw new ObjectDisposedException(nameof(borrowedHandle));
        if(borrowedHandle.IsInvalid)throw new ArgumentException("A valid borrowed descriptor is required.",nameof(borrowedHandle));
        if(relativePath!=""||flags!=0x1000||mask!=0x341)
            throw new ArgumentException("Only the admitted descriptor-relative statx request is supported.");
        var acquired=false;
        try{
            // The P/Invoke signature is the exact C int descriptor ABI. Keep
            // its borrowed SafeHandle pinned during conversion and native use.
            borrowedHandle.DangerousAddRef(ref acquired);
            var descriptor=checked((int)borrowedHandle.DangerousGetHandle().ToInt64());
            if(ReadStatx(descriptor,relativePath,flags,mask,out var raw)<0)
                throw NativeError("statx",null,Marshal.GetLastPInvokeError());
            return new LinuxStatxIdentity(raw.Mask,raw.Mode,raw.DeviceMajor,raw.DeviceMinor,
                raw.Inode,raw.Size,raw.ModifiedSeconds,raw.ModifiedNanoseconds);
        }catch(EntryPointNotFoundException error){throw Unavailable(error);}
        catch(DllNotFoundException error){throw Unavailable(error);}
        finally{if(acquired)borrowedHandle.DangerousRelease();}
    }

    public SafeFileHandle OpenWindows(WindowsArtifactOpenRequest request)=>
        throw new PlatformNotSupportedException("The concrete Windows kernel is not admitted by this increment.");
    public WindowsFileIdentity ReadWindowsIdentity(SafeFileHandle borrowedHandle)=>
        throw new PlatformNotSupportedException("The concrete Windows kernel is not admitted by this increment.");

    private void RequireLinux()
    {
        if(Abi is not(NativeArtifactAbi.LinuxX64 or NativeArtifactAbi.LinuxArm64))
            throw new PlatformNotSupportedException("Native artifact syscalls require admitted little-endian Linux x64/arm64.");
    }

    private static PlatformNotSupportedException Unavailable(Exception inner)=>
        new("Required Linux artifact syscall library/symbol is unavailable; no fallback is used.",inner);

    private static Exception NativeError(string operation,string? path,int error)
    {
        var native=new Win32Exception(error);
        var message=$"Native artifact {operation} failed (errno {error}).";
        return error switch{
            2=>new FileNotFoundException(message,path,native),
            1 or 13=>new UnauthorizedAccessException(message,native),
            38 or 95=>new PlatformNotSupportedException(message,native),
            _=>new IOException(message,native)
        };
    }

    // Linux UAPI struct statx is 0x100 bytes on both admitted ABIs. Field
    // offsets below are the published fixed-width UAPI, not libc struct stat.
    // Only extraction occurs here; semantic field validation stays in the
    // already proven NativeArtifactIdentityReader.
#pragma warning disable CS0649 // Blittable out buffer is populated by statx.
    [StructLayout(LayoutKind.Explicit,Size=256)]
    private struct LinuxStatxBuffer
    {
        [FieldOffset(0)] public uint Mask;
        [FieldOffset(28)] public ushort Mode;
        [FieldOffset(32)] public ulong Inode;
        [FieldOffset(40)] public ulong Size;
        [FieldOffset(112)] public long ModifiedSeconds;
        [FieldOffset(120)] public uint ModifiedNanoseconds;
        [FieldOffset(136)] public uint DeviceMajor;
        [FieldOffset(140)] public uint DeviceMinor;
    }
#pragma warning restore CS0649

    [DllImport("libc",EntryPoint="open",SetLastError=true)]
    private static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)]string path,int flags);
    [DllImport("libc",EntryPoint="statx",SetLastError=true)]
    private static extern int ReadStatx(int descriptor,[MarshalAs(UnmanagedType.LPUTF8Str)]string path,
        int flags,uint mask,out LinuxStatxBuffer buffer);
}
