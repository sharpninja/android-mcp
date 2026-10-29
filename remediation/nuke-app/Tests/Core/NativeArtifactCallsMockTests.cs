using Microsoft.Win32.SafeHandles;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed record NativeArtifactCallCase(string Id,NativeArtifactAbi? Abi,string Operation,string Mode="regular");
public static class NativeArtifactCallCases
{
    public static readonly string Path=System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"kernel-fixture","payload.bin"));
    public static readonly LinuxStatxIdentity Linux=new(0x341,0x81a4,0x87654321,0xabcdef01,0x123456789abcdef0,3,-12,987654321);
    public static readonly WindowsFileIdentity Windows=new(1,false,0x8765432101234567,0xabcdef0123456789,0x123456789abcdef0,3,116444736000000001,0);
    public static IEnumerable<object[]> Cases=>new NativeArtifactCallCase[]{
        new("KERNEL-OPEN-LX64",NativeArtifactAbi.LinuxX64,"open-linux"),
        new("KERNEL-OPEN-LA64",NativeArtifactAbi.LinuxArm64,"open-linux"),
        new("KERNEL-OPEN-WIN",NativeArtifactAbi.Windows,"open-windows"),
        new("KERNEL-NO-READ",NativeArtifactAbi.Windows,"open-windows","missing-read"),
        new("KERNEL-NO-NONBLOCK",NativeArtifactAbi.LinuxX64,"open-linux","missing-nonblock"),
        new("KERNEL-NO-NOFOLLOW",NativeArtifactAbi.Windows,"open-windows","missing-nofollow"),
        new("KERNEL-NO-CLOEXEC",NativeArtifactAbi.LinuxArm64,"open-linux","missing-cloexec"),
        new("KERNEL-EXTRA-FLAG",NativeArtifactAbi.LinuxX64,"open-linux","extra"),
        new("KERNEL-WIN-NONBLOCK",NativeArtifactAbi.Windows,"open-windows","extra-nonblock"),
        new("KERNEL-UNSUPPORTED",null,"open-linux","unsupported"),
        new("KERNEL-UNKNOWN-ABI",(NativeArtifactAbi)99,"open-linux","unsupported"),
        new("KERNEL-WRONG-LINUX",NativeArtifactAbi.Windows,"open-linux","wrong-route"),
        new("KERNEL-WRONG-WINDOWS",NativeArtifactAbi.LinuxX64,"open-windows","wrong-route"),
        new("KERNEL-OPEN-LINUX-ERROR",NativeArtifactAbi.LinuxX64,"open-linux","error"),
        new("KERNEL-OPEN-WIN-ERROR",NativeArtifactAbi.Windows,"open-windows","error"),
        new("KERNEL-OPEN-LINUX-MISSING",NativeArtifactAbi.LinuxX64,"open-linux","missing"),
        new("KERNEL-OPEN-WIN-MISSING",NativeArtifactAbi.Windows,"open-windows","missing"),
        new("KERNEL-STATX-LX64",NativeArtifactAbi.LinuxX64,"read-linux"),
        new("KERNEL-STATX-LA64",NativeArtifactAbi.LinuxArm64,"read-linux"),
        new("KERNEL-IDENTITY-WIN",NativeArtifactAbi.Windows,"read-windows"),
        new("KERNEL-READ-WRONG-LINUX",NativeArtifactAbi.Windows,"read-linux","wrong-route"),
        new("KERNEL-READ-WRONG-WIN",NativeArtifactAbi.LinuxX64,"read-windows","wrong-route"),
        new("KERNEL-READ-UNSUPPORTED",null,"read-linux","unsupported"),
        new("KERNEL-STATX-ERROR",NativeArtifactAbi.LinuxX64,"read-linux","error"),
        new("KERNEL-IDENTITY-ERROR",NativeArtifactAbi.Windows,"read-windows","error"),
        new("KERNEL-READ-INVALID",NativeArtifactAbi.LinuxX64,"read-linux","invalid"),
        new("KERNEL-READ-CLOSED",NativeArtifactAbi.Windows,"read-windows","closed"),
        new("KERNEL-READ-NULL",NativeArtifactAbi.LinuxX64,"read-linux","null"),
        new("KERNEL-STATX-DISPOSE",NativeArtifactAbi.LinuxArm64,"read-linux","dispose-during"),
        new("KERNEL-WIN-DISPOSE",NativeArtifactAbi.Windows,"read-windows","dispose-during")
    }.Select(test=>new object[]{test});
    public static NativeArtifactPlatform? Platform(NativeArtifactAbi? abi)=>abi switch{
        NativeArtifactAbi.LinuxX64 or NativeArtifactAbi.LinuxArm64=>NativeArtifactPlatform.Linux,
        NativeArtifactAbi.Windows=>NativeArtifactPlatform.Windows,_=>null};
    public static ArtifactReadOpenFlags Flags(NativeArtifactCallCase test)
    {
        var flags=ArtifactReadOpenFlags.ReadOnly|ArtifactReadOpenFlags.NoFollow|ArtifactReadOpenFlags.CloseOnExec;
        if(test.Operation=="open-linux")flags|=ArtifactReadOpenFlags.NonBlocking;
        return test.Mode switch{
            "missing-read"=>flags&~ArtifactReadOpenFlags.ReadOnly,"missing-nonblock"=>flags&~ArtifactReadOpenFlags.NonBlocking,
            "missing-nofollow"=>flags&~ArtifactReadOpenFlags.NoFollow,"missing-cloexec"=>flags&~ArtifactReadOpenFlags.CloseOnExec,
            "extra"=>flags|(ArtifactReadOpenFlags)16,"extra-nonblock"=>flags|ArtifactReadOpenFlags.NonBlocking,_=>flags};
    }
    public static bool RejectedBeforeKernel(NativeArtifactCallCase test)=>test.Mode is "unsupported" or "wrong-route" or "invalid" or "closed" or "null"
        or "missing-read" or "missing-nonblock" or "missing-nofollow" or "missing-cloexec" or "extra" or "extra-nonblock";
    public static string[] Mutations(NativeArtifactCallCase test)
    {
        var values=new List<string>{"platform","payload-write","process"};
        if(RejectedBeforeKernel(test))values.AddRange(["call-on-rejected","admit","wrong-error"]);
        else if(test.Mode is "error" or "missing")values.AddRange(["admit","replace-error","wrong-error"]);
        else if(test.Operation.StartsWith("open-",StringComparison.Ordinal))values.AddRange(["flags","path","wrong-route","returned-handle","close-success"]);
        else{
            values.AddRange(["different-borrowed","raw-field"]);
            if(test.Mode!="dispose-during")values.Add("dispose-borrowed");
        }
        if(!RejectedBeforeKernel(test)&&test.Operation=="read-linux")values.AddRange(["statx-path","statx-flags","statx-mask"]);
        if(!RejectedBeforeKernel(test)&&test.Operation.StartsWith("read-",StringComparison.Ordinal)){
            values.Add("borrow-pin-leak");
            if(test.Mode=="error")values.Add("dispose-borrowed");
        }
        if(test.Mode=="invalid")values.Add("close-rejected-borrowed");
        if(test.Mode=="dispose-during")values.Add("no-borrow-pin");
        return values.ToArray();
    }
    public static void AssertCase(NativeArtifactCallCase test,
        Func<INativeArtifactKernel,(INativeArtifactReadOpenCalls Open,INativeArtifactIdentityCalls Identity)> factory)
    {
        using var handle=new SafeFileHandle(new IntPtr(test.Mode=="invalid"?-1:1234),false);
        if(test.Mode=="closed")handle.Dispose();
        var kernel=new ScriptedArtifactKernel(test,handle);var adapter=factory(kernel);
        Assert.Equal(Platform(test.Abi),adapter.Open.Platform);Assert.Equal(Platform(test.Abi),adapter.Identity.Platform);
        SafeFileHandle? opened=null;object? raw=null;
        var error=Record.Exception(()=>{
            if(test.Operation=="open-linux")opened=adapter.Open.OpenLinux(Path,Flags(test));
            else if(test.Operation=="open-windows")opened=adapter.Open.OpenWindows(Path,Flags(test));
            else if(test.Operation=="read-linux")raw=adapter.Identity.ReadLinux(test.Mode=="null"?null!:handle);
            else raw=adapter.Identity.ReadWindows(handle);
        });
        Assert.Equal(0,kernel.Writes);Assert.Equal(0,kernel.Processes);
        Assert.Equal(RejectedBeforeKernel(test)?0:1,kernel.Calls);
        if(!RejectedBeforeKernel(test)){
            Assert.Equal(test.Operation,kernel.Route);
            if(test.Operation=="open-linux"){
                Assert.Equal(Path,kernel.Path);Assert.Equal(0x000a0800,kernel.LinuxFlags);
            }else if(test.Operation=="open-windows"){
                Assert.Equal(new WindowsArtifactOpenRequest(Path,0x80000000,1,3,0x02200000,false),kernel.WindowsRequest);
            }else{
                Assert.True(kernel.SameBorrowed);Assert.False(kernel.ClosedInside);
                if(test.Operation=="read-linux"){
                    Assert.Equal("",kernel.Path);Assert.Equal(0x1000,kernel.StatxFlags);Assert.Equal(0x341U,kernel.StatxMask);
                }
            }
        }
        if(RejectedBeforeKernel(test)){
            var expected=test.Mode switch{
                "unsupported" or "wrong-route"=>typeof(PlatformNotSupportedException),
                "closed"=>typeof(ObjectDisposedException),"null"=>typeof(ArgumentNullException),_=>typeof(ArgumentException)};
            Assert.IsType(expected,error);Assert.Null(opened);Assert.Null(raw);
            if(test.Mode=="invalid")Assert.False(handle.IsClosed);
        }else if(test.Mode is "error" or "missing"){
            Assert.Same(kernel.Error,error);Assert.Null(opened);Assert.Null(raw);
            if(test.Operation.StartsWith("read-",StringComparison.Ordinal)){
                Assert.False(handle.IsClosed);handle.Dispose();Assert.True(handle.IsClosed);
            }
        }else{
            Assert.Null(error);
            if(test.Operation.StartsWith("open-",StringComparison.Ordinal)){
                Assert.Same(kernel.Opened,opened);Assert.False(opened!.IsClosed);opened.Dispose();Assert.True(kernel.Opened!.IsClosed);
            }else{
                Assert.Same(test.Operation=="read-linux"?(object)Linux:Windows,raw);
                Assert.Equal(test.Mode=="dispose-during",handle.IsClosed);
                handle.Dispose();Assert.True(handle.IsClosed);
            }
        }
    }
}

public sealed class ScriptedArtifactKernel(NativeArtifactCallCase test,SafeFileHandle caller):INativeArtifactKernel
{
    public NativeArtifactAbi? Abi=>test.Abi;
    public int Calls {get;set;}
    public string? Route {get;set;}
    public string? Path {get;set;}
    public int LinuxFlags {get;set;}
    public WindowsArtifactOpenRequest? WindowsRequest {get;set;}
    public int StatxFlags {get;set;}
    public uint StatxMask {get;set;}
    public bool SameBorrowed {get;set;}
    public bool ClosedInside {get;set;}
    public int Writes {get;set;}
    public int Processes {get;set;}
    public SafeFileHandle? Opened {get;set;}
    public Exception Error {get;}=test.Mode=="missing"?new FileNotFoundException("kernel-missing-fixture"):new IOException("kernel-error-fixture");
    private void Enter(string route){Calls++;Route=route;}
    private void FailIfRequested(){if(test.Mode is "error" or "missing")throw Error;}
    public SafeFileHandle OpenLinux(string path,int flags){Enter("open-linux");Path=path;LinuxFlags=flags;FailIfRequested();return Opened=new SafeFileHandle(new IntPtr(2345),false);}
    public SafeFileHandle OpenWindows(WindowsArtifactOpenRequest request){Enter("open-windows");WindowsRequest=request;FailIfRequested();return Opened=new SafeFileHandle(new IntPtr(2345),false);}
    private void Borrow(SafeFileHandle handle){SameBorrowed=ReferenceEquals(caller,handle);if(test.Mode=="dispose-during")handle.Dispose();ClosedInside=handle.IsClosed;}
    public LinuxStatxIdentity Statx(SafeFileHandle handle,string path,int flags,uint mask){Enter("read-linux");Path=path;StatxFlags=flags;StatxMask=mask;Borrow(handle);FailIfRequested();return NativeArtifactCallCases.Linux;}
    public WindowsFileIdentity ReadWindowsIdentity(SafeFileHandle handle){Enter("read-windows");Borrow(handle);FailIfRequested();return NativeArtifactCallCases.Windows;}
}

// Scripted behavioral response model only, no syscall/PInvoke implementation.
public sealed class ScriptedNativeArtifactCalls(NativeArtifactCallCase test,ScriptedArtifactKernel kernel,string? mutation=null):INativeArtifactReadOpenCalls,INativeArtifactIdentityCalls
{
    public NativeArtifactPlatform? Platform=>mutation=="platform"?(NativeArtifactPlatform)99:NativeArtifactCallCases.Platform(kernel.Abi);
    public SafeFileHandle OpenLinux(string path,ArtifactReadOpenFlags flags)=>(SafeFileHandle)Run("open-linux",path,flags,null);
    public SafeFileHandle OpenWindows(string path,ArtifactReadOpenFlags flags)=>(SafeFileHandle)Run("open-windows",path,flags,null);
    public LinuxStatxIdentity ReadLinux(SafeFileHandle handle)=>(LinuxStatxIdentity)Run("read-linux","",0,handle);
    public WindowsFileIdentity ReadWindows(SafeFileHandle handle)=>(WindowsFileIdentity)Run("read-windows","",0,handle);
    private object Run(string operation,string path,ArtifactReadOpenFlags flags,SafeFileHandle? handle)
    {
        var acquired=false;
        try{
            if(mutation=="payload-write")kernel.Writes++;
            if(mutation=="process")kernel.Processes++;
            if(NativeArtifactCallCases.RejectedBeforeKernel(test)){
                if(mutation=="close-rejected-borrowed")handle?.Dispose();
                if(mutation=="call-on-rejected")kernel.Calls++;
                if(mutation=="admit")return operation.StartsWith("open-",StringComparison.Ordinal)?new SafeFileHandle(new IntPtr(3456),false):operation=="read-linux"?(object)NativeArtifactCallCases.Linux:NativeArtifactCallCases.Windows;
                if(mutation=="wrong-error")throw new FormatException("wrong-error");
                if(test.Mode is "unsupported" or "wrong-route")throw new PlatformNotSupportedException();
                if(test.Mode=="null")throw new ArgumentNullException(nameof(handle));
                if(test.Mode=="closed")throw new ObjectDisposedException(nameof(handle));
                throw new ArgumentException("rejected-fixture");
            }
            var route=mutation=="wrong-route"?(operation=="open-linux"?"open-windows":"open-linux"):operation;
            object result;
            if(operation=="open-linux")result=route=="open-linux"?kernel.OpenLinux(mutation=="path"?path+"-wrong":path,mutation=="flags"?0:0xa0800):kernel.OpenWindows(new(path,0x80000000,1,3,0x02200000,false));
            else if(operation=="open-windows")result=route=="open-windows"?kernel.OpenWindows(new(mutation=="path"?path+"-wrong":path,mutation=="flags"?0U:0x80000000,1,3,0x02200000,false)):kernel.OpenLinux(path,0xa0800);
            else{
                if(mutation!="no-borrow-pin")handle!.DangerousAddRef(ref acquired);
                using var other=new SafeFileHandle(new IntPtr(3456),false);var borrowed=mutation=="different-borrowed"?other:handle!;
                result=operation=="read-linux"?kernel.Statx(borrowed,mutation=="statx-path"?"wrong":"",mutation=="statx-flags"?0:0x1000,mutation=="statx-mask"?0U:0x341):kernel.ReadWindowsIdentity(borrowed);
                if(mutation=="raw-field")result=operation=="read-linux"?NativeArtifactCallCases.Linux with{Inode=99}:(object)(NativeArtifactCallCases.Windows with{FileIdHigh=99});
                if(mutation=="dispose-borrowed")handle!.Dispose();
            }
            if(mutation=="returned-handle")result=new SafeFileHandle(new IntPtr(3456),false);
            if(mutation=="close-success")((SafeFileHandle)result).Dispose();
            return result;
        }catch(Exception error) when(error is IOException){
            if(mutation=="dispose-borrowed")handle?.Dispose();
            if(mutation=="admit")return operation.StartsWith("open-",StringComparison.Ordinal)?new SafeFileHandle(new IntPtr(3456),false):operation=="read-linux"?(object)NativeArtifactCallCases.Linux:NativeArtifactCallCases.Windows;
            if(mutation=="replace-error")throw new IOException(error.Message);
            if(mutation=="wrong-error")throw new FormatException("wrong-error");
            throw;
        }finally{if(acquired&&mutation!="borrow-pin-leak")handle!.DangerousRelease();}
    }
}

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-007")]
public sealed class NativeArtifactCallsMockTests
{
    [Theory,MemberData(nameof(NativeArtifactCallCases.Cases),MemberType=typeof(NativeArtifactCallCases))]
    public void CorrectKernelIntentAndOwnershipMocksPass(NativeArtifactCallCase test)=>NativeArtifactCallCases.AssertCase(test,kernel=>{var calls=new ScriptedNativeArtifactCalls(test,(ScriptedArtifactKernel)kernel);return(calls,calls);});
    [Theory,MemberData(nameof(NativeArtifactCallCases.Cases),MemberType=typeof(NativeArtifactCallCases))]
    public void EveryWrongKernelRequestAndOwnershipIsRejected(NativeArtifactCallCase test)
    {
        foreach(var mutation in NativeArtifactCallCases.Mutations(test))
            Assert.NotNull(Record.Exception(()=>NativeArtifactCallCases.AssertCase(test,kernel=>{var calls=new ScriptedNativeArtifactCalls(test,(ScriptedArtifactKernel)kernel,mutation);return(calls,calls);})));
    }
}
