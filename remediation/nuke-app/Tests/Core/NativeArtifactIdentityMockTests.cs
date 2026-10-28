using Microsoft.Win32.SafeHandles;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed record NativeIdentityCase(string Id,NativeArtifactPlatform? Platform,LinuxStatxIdentity Linux,
    WindowsFileIdentity Windows,NativeArtifactObservation? Expected,string? Error=null,bool DisposeDuringCall=false);
public static class NativeArtifactIdentityCases
{
    public const long WindowsEpoch=116444736000000000;
    private static readonly LinuxStatxIdentity Linux=new(0x341,0x81a4,7,9,0x1122334455667788,3,123456789,987654321);
    private static readonly WindowsFileIdentity Windows=new(1,false,0x1122334455667788,0x8877665544332211,0xfedcba9876543210,3,117679303899876543,0);
    private static readonly NativeArtifactObservation LinuxExpected=new(NativeArtifactPlatform.Linux,NativeArtifactKind.RegularFile,0x0000000700000009,0x1122334455667788,0,3,123456789,987654321);
    private static readonly NativeArtifactObservation WindowsExpected=new(NativeArtifactPlatform.Windows,NativeArtifactKind.RegularFile,0x1122334455667788,0x8877665544332211,0xfedcba9876543210,3,123456789,987654300);
    public static IEnumerable<object[]> Cases
    {
        get{
            var linux=new NativeIdentityCase("ID-LINUX-REGULAR",NativeArtifactPlatform.Linux,Linux,Windows,LinuxExpected);
            var windows=linux with{Id="ID-WINDOWS-REGULAR",Platform=NativeArtifactPlatform.Windows,Expected=WindowsExpected};
            return new[]{
                linux,
                linux with{Id="ID-LINUX-HIGH-BITS",Linux=Linux with{DeviceMajor=0xfffffffe,DeviceMinor=0xfedcba98,Inode=ulong.MaxValue-3,Size=long.MaxValue},Expected=LinuxExpected with{DeviceOrVolume=0xfffffffefedcba98,FileIdLow=ulong.MaxValue-3,Length=long.MaxValue}},
                linux with{Id="ID-LINUX-PREEPOCH",Linux=Linux with{ModifiedSeconds=-1,ModifiedNanoseconds=999999999},Expected=LinuxExpected with{ModifiedSeconds=-1,ModifiedNanoseconds=999999999}},
                linux with{Id="ID-LINUX-DIRECTORY",Linux=Linux with{Mode=0x41ed},Expected=LinuxExpected with{Kind=NativeArtifactKind.Directory}},
                linux with{Id="ID-LINUX-OTHER",Linux=Linux with{Mode=0x11b6},Expected=LinuxExpected with{Kind=NativeArtifactKind.Other}},
                linux with{Id="ID-LINUX-MISSING-TYPE",Linux=Linux with{Mask=0x340},Expected=null,Error="io"},
                linux with{Id="ID-LINUX-MISSING-MTIME",Linux=Linux with{Mask=0x301},Expected=null,Error="io"},
                linux with{Id="ID-LINUX-MISSING-INODE",Linux=Linux with{Mask=0x241},Expected=null,Error="io"},
                linux with{Id="ID-LINUX-MISSING-SIZE",Linux=Linux with{Mask=0x141},Expected=null,Error="io"},
                linux with{Id="ID-LINUX-SIZE-OVERFLOW",Linux=Linux with{Size=ulong.MaxValue},Expected=null,Error="io"},
                linux with{Id="ID-LINUX-BAD-NANOSECONDS",Linux=Linux with{ModifiedNanoseconds=1000000000},Expected=null,Error="io"},
                linux with{Id="ID-LINUX-BAD-MODE",Linux=Linux with{Mode=0},Expected=null,Error="io"},
                linux with{Id="ID-LINUX-SYSCALL-ERROR",Expected=null,Error="syscall"},
                windows,
                windows with{Id="ID-WINDOWS-DIRECTORY",Windows=Windows with{Directory=true,Attributes=0x10},Expected=WindowsExpected with{Kind=NativeArtifactKind.Directory}},
                windows with{Id="ID-WINDOWS-NONDISK",Windows=Windows with{FileType=2},Expected=null,Error="io"},
                windows with{Id="ID-WINDOWS-REPARSE",Windows=Windows with{Attributes=0x400},Expected=WindowsExpected with{Kind=NativeArtifactKind.Other}},
                windows with{Id="ID-WINDOWS-PREEPOCH",Windows=Windows with{LastWriteFileTime=WindowsEpoch-1},Expected=WindowsExpected with{ModifiedSeconds=-1,ModifiedNanoseconds=999999900}},
                windows with{Id="ID-WINDOWS-1601",Windows=Windows with{LastWriteFileTime=0},Expected=WindowsExpected with{ModifiedSeconds=-11644473600,ModifiedNanoseconds=0}},
                windows with{Id="ID-WINDOWS-NEGATIVE-LENGTH",Windows=Windows with{EndOfFile=-1},Expected=null,Error="io"},
                windows with{Id="ID-WINDOWS-NEGATIVE-TIME",Windows=Windows with{LastWriteFileTime=-1},Expected=null,Error="io"},
                windows with{Id="ID-WINDOWS-SYSCALL-ERROR",Expected=null,Error="syscall"},
                linux with{Id="ID-UNSUPPORTED",Platform=null,Expected=null,Error="unsupported"},
                linux with{Id="ID-UNKNOWN-PLATFORM",Platform=(NativeArtifactPlatform)99,Expected=null,Error="unsupported"},
                linux with{Id="ID-NULL-HANDLE",Expected=null,Error="null"},
                linux with{Id="ID-CLOSED-HANDLE",Expected=null,Error="closed"},
                linux with{Id="ID-INVALID-HANDLE",Expected=null,Error="invalid"},
                linux with{Id="ID-LINUX-CALLER-DISPOSES",DisposeDuringCall=true},
                windows with{Id="ID-WINDOWS-CALLER-DISPOSES",DisposeDuringCall=true}
            }.Select(c=>new object[]{c});
        }
    }
    public static string[] Mutations(NativeIdentityCase c)=>c.Error is not null
        ? new[]{"error-swallowed","error-type"}.Concat(c.Error is "null" or "closed" or "invalid" or "unsupported"?new[]{"call-on-rejected"}:c.Error=="syscall"?new[]{"error-message"}:Array.Empty<string>()).ToArray()
        : new[]{"platform","kind","device","file-low","file-high","length","seconds","nanoseconds","wrong-route","different-handle"}
            .Concat(c.DisposeDuringCall?new[]{"no-addref","leak-addref"}:new[]{"close-borrowed"}).ToArray();
    public static void AssertCase(NativeIdentityCase c,Func<INativeArtifactIdentityCalls,INativeArtifactIdentityReader> factory)
    {
        using var handle=c.Error=="null"?null:new SafeFileHandle(new IntPtr(c.Error=="invalid"?-1:1234),false);
        if(c.Error=="closed")handle!.Dispose();
        var calls=new ScriptedIdentityCalls(c,handle);var provider=factory(calls);
        NativeArtifactObservation? observed=null;
        var error=Record.Exception(()=>observed=provider.Observe(handle!));
        var expectedCalls=c.Error is "null" or "closed" or "invalid" or "unsupported"?0:1;
        Assert.Equal(expectedCalls,calls.LinuxCalls+calls.WindowsCalls);
        Assert.Equal(expectedCalls==1&&c.Platform==NativeArtifactPlatform.Linux?1:0,calls.LinuxCalls);
        Assert.Equal(expectedCalls==1&&c.Platform==NativeArtifactPlatform.Windows?1:0,calls.WindowsCalls);
        if(expectedCalls==1){Assert.True(calls.SameHandle);Assert.False(calls.ClosedInsideCall);}
        if(c.Error is not null){
            Assert.Null(observed);Assert.NotNull(error);
            var type=c.Error switch{"null"=>typeof(ArgumentNullException),"closed"=>typeof(ObjectDisposedException),"invalid"=>typeof(ArgumentException),"unsupported"=>typeof(PlatformNotSupportedException),_=>typeof(IOException)};
            Assert.IsType(type,error);if(c.Error=="syscall")Assert.Equal("syscall-fixture",error!.Message);
        }else{Assert.Null(error);Assert.Equal(c.Expected,observed);}
        if(handle is not null)Assert.Equal(c.Error=="closed"||c.DisposeDuringCall,handle.IsClosed);
    }
}

public sealed class ScriptedIdentityCalls(NativeIdentityCase c,SafeFileHandle? expectedHandle) : INativeArtifactIdentityCalls
{
    public NativeArtifactPlatform? Platform=>c.Platform;
    public int LinuxCalls {get;private set;}
    public int WindowsCalls {get;private set;}
    public bool SameHandle {get;private set;}
    public bool ClosedInsideCall {get;private set;}
    private void ObserveBorrow(SafeFileHandle handle)
    {
        SameHandle=ReferenceEquals(expectedHandle,handle);
        if(c.DisposeDuringCall)handle.Dispose();
        ClosedInsideCall=handle.IsClosed;
        if(c.Error=="syscall")throw new IOException("syscall-fixture");
    }
    public LinuxStatxIdentity ReadLinux(SafeFileHandle handle){LinuxCalls++;ObserveBorrow(handle);return c.Linux;}
    public WindowsFileIdentity ReadWindows(SafeFileHandle handle){WindowsCalls++;ObserveBorrow(handle);return c.Windows;}
}

// Independent scripted case responses. This is not the native field mapper or
// a syscall implementation; real mapper bindings follow only a green mock gate.
public sealed class ScriptedNativeArtifactIdentityReader(NativeIdentityCase c,INativeArtifactIdentityCalls calls,string? mutation=null) : INativeArtifactIdentityReader
{
    private static NativeArtifactObservation Fallback()=>new(NativeArtifactPlatform.Linux,NativeArtifactKind.RegularFile,0,0,0,0,0,0);
    private Exception Failure()=>mutation=="error-type"?new FormatException("wrong-type"):c.Error switch{
        "null"=>new ArgumentNullException("borrowedHandle"),"closed"=>new ObjectDisposedException("borrowedHandle"),
        "invalid"=>new ArgumentException("invalid-handle"),"unsupported"=>new PlatformNotSupportedException("unsupported-host"),
        "syscall"=>new IOException("syscall-fixture"),_=>new IOException("invalid-native-fields")
    };
    private void Invoke(SafeFileHandle handle,bool wrongRoute=false)
    {
        var linux=c.Platform==NativeArtifactPlatform.Linux;
        if(wrongRoute)linux=!linux;
        if(linux)_=calls.ReadLinux(handle);else _=calls.ReadWindows(handle);
    }
    public NativeArtifactObservation Observe(SafeFileHandle borrowedHandle)
    {
        if(c.Error is "null" or "closed" or "invalid" or "unsupported"){
            if(mutation=="call-on-rejected"){
                if(c.Platform==NativeArtifactPlatform.Windows)_=calls.ReadWindows(borrowedHandle);
                else _=calls.ReadLinux(borrowedHandle);
            }
            if(mutation=="error-swallowed")return Fallback();
            throw Failure();
        }
        var acquired=false;
        try{
            if(mutation!="no-addref")borrowedHandle.DangerousAddRef(ref acquired);
            try{
                if(mutation=="different-handle"){
                    using var other=new SafeFileHandle(new IntPtr(2345),false);Invoke(other);
                }else Invoke(borrowedHandle,mutation=="wrong-route");
            }catch(IOException){
                if(mutation=="error-swallowed")return Fallback();
                if(mutation=="error-message")throw new IOException("wrong-message");
                if(mutation=="error-type")throw new FormatException("wrong-type");
                throw;
            }
            if(c.Error is not null){if(mutation=="error-swallowed")return Fallback();throw Failure();}
            var value=c.Expected!;
            return mutation switch{
                "platform"=>value with{Platform=value.Platform==NativeArtifactPlatform.Linux?NativeArtifactPlatform.Windows:NativeArtifactPlatform.Linux},
                "kind"=>value with{Kind=value.Kind==NativeArtifactKind.Other?NativeArtifactKind.RegularFile:NativeArtifactKind.Other},
                "device"=>value with{DeviceOrVolume=value.DeviceOrVolume^1},"file-low"=>value with{FileIdLow=value.FileIdLow^1},
                "file-high"=>value with{FileIdHigh=value.FileIdHigh^1},"length"=>value with{Length=unchecked(value.Length+1)},
                "seconds"=>value with{ModifiedSeconds=value.ModifiedSeconds+1},"nanoseconds"=>value with{ModifiedNanoseconds=value.ModifiedNanoseconds^1},_=>value
            };
        }finally{
            if(acquired&&mutation!="leak-addref")borrowedHandle.DangerousRelease();
            if(mutation=="close-borrowed")borrowedHandle.Dispose();
        }
    }
}

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-007")]
public sealed class NativeArtifactIdentityMockTests
{
    [Theory,MemberData(nameof(NativeArtifactIdentityCases.Cases),MemberType=typeof(NativeArtifactIdentityCases))]
    public void CorrectScriptedNativeResultsPass(NativeIdentityCase c)=>NativeArtifactIdentityCases.AssertCase(c,calls=>new ScriptedNativeArtifactIdentityReader(c,calls));
    [Theory,MemberData(nameof(NativeArtifactIdentityCases.Cases),MemberType=typeof(NativeArtifactIdentityCases))]
    public void IndividualWrongFieldsCallsErrorsAndLifetimeAreRejected(NativeIdentityCase c)
    {
        foreach(var mutation in NativeArtifactIdentityCases.Mutations(c))
            Assert.NotNull(Record.Exception(()=>NativeArtifactIdentityCases.AssertCase(c,calls=>new ScriptedNativeArtifactIdentityReader(c,calls,mutation))));
    }
}
