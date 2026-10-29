using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

// Finite actual-Linux runtime proofs. All fixtures are retained, including FIFO
// and links. These tests do not establish Windows or real path-replacement proof.
[Trait("Gate","Real"),Trait("Platform","Linux"),Trait("Requirement","TEST-BUILD-007")]
public sealed class NativeArtifactKernelLinuxRealTests
{
    private const int ReadFlags=0xa0800;
    private static readonly byte[] Payload=[0,1,2,127,128,254,255];

    [Fact]
    public void ExactStatxBufferSizeAndOffsetsMatchTheAdmittedUapi()
    {
        var type=typeof(NativeArtifactKernel).GetNestedType("LinuxStatxBuffer",BindingFlags.NonPublic)!;
        Assert.NotNull(type);Assert.Equal(256,Marshal.SizeOf(type));
        foreach(var field in new (string Name,int Offset)[]{("Mask",0),("Mode",28),("Inode",32),("Size",40),
            ("ModifiedSeconds",112),("ModifiedNanoseconds",120),("DeviceMajor",136),("DeviceMinor",140)})
            Assert.Equal(field.Offset,Marshal.OffsetOf(type,field.Name).ToInt32());
    }

    [Fact]
    public void RetainedEqualContentLengthAndMtimeFilesHaveDistinctNativeIds()
    {
        var kernel=LinuxKernel();var root=NewRoot();
        var first=Path.Combine(root,"first.bin");var second=Path.Combine(root,"second.bin");
        File.WriteAllBytes(first,Payload);File.WriteAllBytes(second,Payload);
        var stamp=DateTimeOffset.FromUnixTimeSeconds(1_650_000_123).AddTicks(1_234_567);
        File.SetLastWriteTimeUtc(first,stamp.UtcDateTime);File.SetLastWriteTimeUtc(second,stamp.UtcDateTime);
        var calls=new NativeArtifactCalls(kernel);var identity=new NativeArtifactIdentityReader(calls);
        using var one=kernel.OpenLinux(first,ReadFlags);using var two=kernel.OpenLinux(second,ReadFlags);
        var raw=kernel.Statx(one,"",0x1000,0x341);
        Assert.Equal(0x341U,raw.Mask&0x341U);Assert.Equal(0x8000,raw.Mode&0xf000);
        Assert.Equal((ulong)Payload.Length,raw.Size);Assert.NotEqual(0UL,raw.Inode);
        Assert.Equal(stamp.ToUnixTimeSeconds(),raw.ModifiedSeconds);Assert.Equal(123_456_700U,raw.ModifiedNanoseconds);
        var observed=identity.Observe(one);var other=identity.Observe(two);
        Assert.Equal(NativeArtifactPlatform.Linux,observed.Platform);Assert.Equal(NativeArtifactKind.RegularFile,observed.Kind);
        Assert.Equal(((ulong)raw.DeviceMajor<<32)|raw.DeviceMinor,observed.DeviceOrVolume);
        Assert.Equal(raw.Inode,observed.FileIdLow);Assert.Equal(0UL,observed.FileIdHigh);
        Assert.Equal(Payload.Length,observed.Length);Assert.Equal(raw.ModifiedSeconds,observed.ModifiedSeconds);
        Assert.Equal(raw.ModifiedNanoseconds,observed.ModifiedNanoseconds);
        Assert.Equal(observed,other with{FileIdLow=observed.FileIdLow});Assert.NotEqual(observed.FileIdLow,other.FileIdLow);
        Assert.False(one.IsClosed);Assert.False(two.IsClosed);
        one.Dispose();Assert.True(one.IsClosed);Assert.Throws<ObjectDisposedException>(()=>identity.Observe(one));
        Assert.Equal(Payload,File.ReadAllBytes(first));Assert.Equal(Payload,File.ReadAllBytes(second));
        Assert.Equal(File.GetLastWriteTimeUtc(first),File.GetLastWriteTimeUtc(second));
    }

    [Fact]
    public void ActualOpenIsReadOnlyNonblockingCloseOnExecAndDoesNotFollowLinks()
    {
        var kernel=LinuxKernel();var root=NewRoot();var payload=Path.Combine(root,"payload.bin");
        File.WriteAllBytes(payload,Payload);
        using(var handle=kernel.OpenLinux(payload,ReadFlags))AssertReadFlags(handle);
        var link=Path.Combine(root,"link.bin");File.CreateSymbolicLink(link,payload);
        var linked=Assert.Throws<IOException>(()=>kernel.OpenLinux(link,ReadFlags));
        Assert.Equal(40,Assert.IsType<Win32Exception>(linked.InnerException).NativeErrorCode);
        var missing=Assert.Throws<FileNotFoundException>(()=>kernel.OpenLinux(Path.Combine(root,"absent.bin"),ReadFlags));
        Assert.Equal(2,Assert.IsType<Win32Exception>(missing.InnerException).NativeErrorCode);
        Assert.Equal(Payload,File.ReadAllBytes(payload));Assert.NotNull(new FileInfo(link).LinkTarget);
    }

    [Fact]
    public async Task FifoWithNoWriterOpensNonblockingAndIsRejectedBeforeFileStream()
    {
        var kernel=LinuxKernel();var root=NewRoot();var regular=Path.Combine(root,"regular.bin");
        File.WriteAllBytes(regular,Payload);
        // Check the SAME concrete open engine's nonblocking flags on a regular
        // fixture before entering the no-writer FIFO test. No timing threshold,
        // rescue writer or read from a FIFO is used to manufacture success.
        using(var preflight=kernel.OpenLinux(regular,ReadFlags))AssertReadFlags(preflight);
        var fifo=Path.Combine(root,"retained.fifo");
        if(MakeFifo(fifo,0x180)!=0)throw new Win32Exception(Marshal.GetLastPInvokeError());
        using(var opened=kernel.OpenLinux(fifo,ReadFlags)){
            AssertReadFlags(opened);var raw=kernel.Statx(opened,"",0x1000,0x341);
            Assert.Equal(0x1000,raw.Mode&0xf000);
        }
        var recording=new RecordingKernel(kernel);var calls=new NativeArtifactCalls(recording);
        var opener=new ArtifactReadOpener(calls,new NativeArtifactIdentityReader(calls));
        Assert.Throws<IOException>(()=>opener.OpenRead(fifo));
        Assert.Single(recording.Opened);Assert.All(recording.Opened,handle=>Assert.True(handle.IsClosed));
        var store=new FileArtifactStore(root,value=>ContentHash.Sha256(value.Span),null,opener);
        await Assert.ThrowsAsync<IOException>(async()=>{await using var unexpected=await store.OpenReadAsync("retained.fifo");});
        Assert.Equal(2,recording.Opened.Count);Assert.All(recording.Opened,handle=>Assert.True(handle.IsClosed));
        Assert.Contains(fifo,Directory.EnumerateFileSystemEntries(root));Assert.Equal(Payload,File.ReadAllBytes(regular));
    }

    [Fact]
    public async Task SameStoreReadsRetainedRegularBytesThroughActualKernelComposition()
    {
        var kernel=LinuxKernel();var root=NewRoot();var path=Path.Combine(root,"payload.bin");File.WriteAllBytes(path,Payload);
        var recording=new RecordingKernel(kernel);var calls=new NativeArtifactCalls(recording);
        var opener=new ArtifactReadOpener(calls,new NativeArtifactIdentityReader(calls));
        var store=new FileArtifactStore(root,value=>ContentHash.Sha256(value.Span),null,opener);
        var inspected=await store.InspectAsync("payload.bin");Assert.NotNull(inspected);
        Assert.Equal(NativeArtifactPlatform.Linux,inspected!.Native.Platform);Assert.Equal(NativeArtifactKind.RegularFile,inspected.Native.Kind);
        Assert.Equal(Payload.Length,inspected.Native.Length);
        var session=await store.OpenReadAsync("payload.bin");
        var bytes=new byte[Payload.Length];await session.Content.ReadExactlyAsync(bytes);Assert.Equal(Payload,bytes);
        Assert.Equal(0,await session.Content.ReadAsync(new byte[1]));Assert.False(session.Content.CanWrite);
        Assert.Equal(inspected,session.Opened);Assert.Equal(inspected,await session.ObserveCurrentPathAsync());
        Assert.False(recording.Opened[1].IsClosed);await session.DisposeAsync();
        Assert.All(recording.Opened,handle=>Assert.True(handle.IsClosed));
        await Assert.ThrowsAsync<ObjectDisposedException>(async()=>{_ =await session.Content.ReadAsync(new byte[1]);});
        Assert.Equal(Payload,File.ReadAllBytes(path));
    }

    [Fact]
    public void ConcreteKernelRejectsUnadmittedRequestsWithoutChangingOriginals()
    {
        var kernel=LinuxKernel();var root=NewRoot();var path=Path.Combine(root,"payload.bin");File.WriteAllBytes(path,Payload);
        Assert.Throws<ArgumentException>(()=>kernel.OpenLinux(path,0));
        Assert.Throws<ArgumentException>(()=>kernel.OpenLinux(path+"\0suffix",ReadFlags));
        Assert.Throws<PlatformNotSupportedException>(()=>kernel.OpenWindows(new(path,0x80000000,1,3,0x02200000,false)));
        using var opened=kernel.OpenLinux(path,ReadFlags);
        Assert.Throws<ArgumentException>(()=>kernel.Statx(opened,"wrong",0x1000,0x341));
        Assert.Throws<ArgumentException>(()=>kernel.Statx(opened,"",0,0x341));
        Assert.Throws<ArgumentException>(()=>kernel.Statx(opened,"",0x1000,0));
        Assert.Throws<PlatformNotSupportedException>(()=>kernel.ReadWindowsIdentity(opened));
        Assert.False(opened.IsClosed);Assert.Equal(Payload,File.ReadAllBytes(path));
    }

    private static NativeArtifactKernel LinuxKernel()
    {
        Assert.True(OperatingSystem.IsLinux(),"This exact runtime proof requires Linux; it is not a Windows proof.");
        var kernel=new NativeArtifactKernel();Assert.True(kernel.Abi is NativeArtifactAbi.LinuxX64 or NativeArtifactAbi.LinuxArm64);
        return kernel;
    }
    private static string NewRoot()=>Directory.CreateDirectory(Path.Combine(Path.GetTempPath(),"android-mcp-native-kernel-"+Guid.NewGuid().ToString("N"))).FullName;
    private static void AssertReadFlags(SafeFileHandle borrowed)
    {
        var acquired=false;
        try{
            borrowed.DangerousAddRef(ref acquired);var descriptor=checked((int)borrowed.DangerousGetHandle().ToInt64());
            var flags=GetFlags(descriptor,3);Assert.True(flags>=0);Assert.Equal(0,flags&3);Assert.Equal(0x800,flags&0x800);
            var fdFlags=GetFlags(descriptor,1);Assert.True(fdFlags>=0);Assert.Equal(1,fdFlags&1);
        }finally{if(acquired)borrowed.DangerousRelease();}
    }
    // Recording only: all native calls still use the single concrete kernel.
    private sealed class RecordingKernel(INativeArtifactKernel inner):INativeArtifactKernel
    {
        public List<SafeFileHandle> Opened {get;}=[];
        public NativeArtifactAbi? Abi=>inner.Abi;
        public SafeFileHandle OpenLinux(string path,int flags){var handle=inner.OpenLinux(path,flags);Opened.Add(handle);return handle;}
        public SafeFileHandle OpenWindows(WindowsArtifactOpenRequest request)=>inner.OpenWindows(request);
        public LinuxStatxIdentity Statx(SafeFileHandle borrowed,string path,int flags,uint mask)=>inner.Statx(borrowed,path,flags,mask);
        public WindowsFileIdentity ReadWindowsIdentity(SafeFileHandle borrowed)=>inner.ReadWindowsIdentity(borrowed);
    }
    [DllImport("libc",EntryPoint="fcntl",SetLastError=true)]
    private static extern int GetFlags(int descriptor,int command);
    [DllImport("libc",EntryPoint="mkfifo",SetLastError=true)]
    private static extern int MakeFifo([MarshalAs(UnmanagedType.LPUTF8Str)]string path,uint mode);
}
