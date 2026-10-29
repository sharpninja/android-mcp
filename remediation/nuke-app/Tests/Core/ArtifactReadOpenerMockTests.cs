using Microsoft.Win32.SafeHandles;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed record ReadOpenCase(string Id,NativeArtifactPlatform? Platform,string Mode="regular");
public static class ArtifactReadOpenerCases
{
    public static readonly NativeArtifactObservation Observation=new(NativeArtifactPlatform.Linux,
        NativeArtifactKind.RegularFile,7,11,13,3,123456789,987654300);
    public static readonly string AdmittedPath=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"read-opener-fixture","payload.bin"));
    public static IEnumerable<object[]> Cases=>new ReadOpenCase[]{
        new("OPEN-LINUX-REGULAR",NativeArtifactPlatform.Linux),new("OPEN-WINDOWS-REGULAR",NativeArtifactPlatform.Windows),
        new("OPEN-LINUX-DIRECTORY",NativeArtifactPlatform.Linux,"directory"),new("OPEN-WINDOWS-DIRECTORY",NativeArtifactPlatform.Windows,"directory"),
        new("OPEN-LINUX-OTHER",NativeArtifactPlatform.Linux,"other"),new("OPEN-WINDOWS-OTHER",NativeArtifactPlatform.Windows,"other"),
        new("OPEN-UNSUPPORTED",null,"unsupported"),new("OPEN-UNKNOWN-PLATFORM",(NativeArtifactPlatform)99,"unsupported"),
        new("OPEN-CANCEL-BEFORE",NativeArtifactPlatform.Linux,"cancel-before"),
        new("OPEN-CANCEL-AFTER-OPEN",NativeArtifactPlatform.Linux,"cancel-open"),
        new("OPEN-CANCEL-AFTER-IDENTITY",NativeArtifactPlatform.Windows,"cancel-identity"),
        new("OPEN-LINUX-SYSCALL-ERROR",NativeArtifactPlatform.Linux,"open-error"),
        new("OPEN-WINDOWS-SYSCALL-ERROR",NativeArtifactPlatform.Windows,"open-error"),
        new("OPEN-MISSING",NativeArtifactPlatform.Linux,"missing"),
        new("OPEN-INVALID-HANDLE",NativeArtifactPlatform.Linux,"invalid"),
        new("OPEN-CLOSED-HANDLE",NativeArtifactPlatform.Windows,"closed"),
        new("OPEN-LINUX-IDENTITY-ERROR",NativeArtifactPlatform.Linux,"identity-error"),
        new("OPEN-WINDOWS-IDENTITY-ERROR",NativeArtifactPlatform.Windows,"identity-error"),
        new("OPEN-IDENTITY-PLATFORM-MISMATCH",NativeArtifactPlatform.Linux,"platform-mismatch"),
        new("OPEN-IDENTITY-CLOSES-HANDLE",NativeArtifactPlatform.Windows,"identity-closes")
    }.Select(c=>new object[]{c});
    public static string[] Mutations(ReadOpenCase c)=>c.Mode switch{
        "regular"=>["missing-readonly","missing-nofollow","missing-closeonexec","extra-flags","wrong-route","wrong-path","skip-identity","different-identity-handle","different-returned-handle","wrong-observation","closed-success"],
        "unsupported" or "cancel-before"=>["admit","call-on-rejected","wrong-error"],
        "open-error" or "missing"=>["admit","wrong-error","wrong-error-message"],
        "cancel-open"=>["admit","identity-after-cancel","leak-failed-handle","wrong-error"],
        "identity-error"=>["admit","leak-failed-handle","wrong-error","wrong-error-message"],
        "closed" or "identity-closes"=>["admit","wrong-error"],
        _=>["admit","leak-failed-handle","wrong-error"]
    };
    public static async Task AssertCase(ReadOpenCase c,Func<INativeArtifactReadOpenCalls,INativeArtifactIdentityReader,IArtifactReadOpener> factory)
    {
        using var cancellation=new CancellationTokenSource();
        using var calls=new ScriptedReadOpenCalls(c,cancellation);
        var identity=new ScriptedReadOpenIdentity(c,calls,cancellation);
        var opener=factory(calls,identity);
        if(c.Mode=="cancel-before")cancellation.Cancel();
        AdmittedArtifactReadHandle? result=null;
        var error=Record.Exception(()=>result=opener.OpenRead(AdmittedPath,cancellation.Token));
        var rejectedBeforeOpen=c.Mode is "unsupported" or "cancel-before";
        Assert.Equal(rejectedBeforeOpen?0:1,calls.LinuxCalls+calls.WindowsCalls);
        Assert.Equal(!rejectedBeforeOpen&&c.Platform==NativeArtifactPlatform.Linux?1:0,calls.LinuxCalls);
        Assert.Equal(!rejectedBeforeOpen&&c.Platform==NativeArtifactPlatform.Windows?1:0,calls.WindowsCalls);
        if(!rejectedBeforeOpen){
            Assert.Equal(AdmittedPath,calls.Path);
            var flags=ArtifactReadOpenFlags.ReadOnly|ArtifactReadOpenFlags.NoFollow|ArtifactReadOpenFlags.CloseOnExec;
            if(c.Platform==NativeArtifactPlatform.Linux)flags|=ArtifactReadOpenFlags.NonBlocking;
            Assert.Equal(flags,calls.Flags);
        }
        var probesIdentity=!rejectedBeforeOpen&&c.Mode is not("open-error" or "missing" or "invalid" or "closed" or "cancel-open");
        Assert.Equal(probesIdentity?1:0,identity.Calls);
        if(probesIdentity){Assert.True(identity.SameHandle);Assert.False(identity.ClosedInside);}
        if(c.Mode=="regular"){
            Assert.Null(error);Assert.NotNull(result);Assert.Same(calls.Handle,result!.Handle);
            Assert.False(result.Handle.IsClosed);Assert.False(result.Handle.IsInvalid);
            Assert.Equal(Observation with{Platform=c.Platform!.Value},result.Observation);
            result.Handle.Dispose();Assert.True(calls.Handle!.IsClosed);
        }else{
            Assert.Null(result);Assert.NotNull(error);
            var type=c.Mode switch{
                "unsupported"=>typeof(PlatformNotSupportedException),
                "cancel-before" or "cancel-open" or "cancel-identity"=>typeof(OperationCanceledException),
                "missing"=>typeof(FileNotFoundException),_=>typeof(IOException)
            };
            Assert.IsType(type,error);
            if(c.Mode is "open-error" or "identity-error")Assert.Equal(c.Mode+"-fixture",error!.Message);
            if(calls.Handle is not null)Assert.True(calls.Handle.IsClosed);
        }
        await Task.CompletedTask;
    }
}

public sealed class ScriptedReadOpenCalls(ReadOpenCase c,CancellationTokenSource cancellation) : INativeArtifactReadOpenCalls,IDisposable
{
    public NativeArtifactPlatform? Platform=>c.Platform;
    public int LinuxCalls {get;private set;}
    public int WindowsCalls {get;private set;}
    public string? Path {get;private set;}
    public ArtifactReadOpenFlags Flags {get;private set;}
    public SafeFileHandle? Handle {get;private set;}
    private SafeFileHandle Open(string path,ArtifactReadOpenFlags flags)
    {
        Path=path;Flags=flags;
        if(c.Mode=="open-error")throw new IOException("open-error-fixture");
        if(c.Mode=="missing")throw new FileNotFoundException("missing-fixture");
        Handle=new SafeFileHandle(new IntPtr(c.Mode=="invalid"?-1:1234),false);
        if(c.Mode=="closed")Handle.Dispose();
        if(c.Mode=="cancel-open")cancellation.Cancel();
        return Handle;
    }
    public SafeFileHandle OpenLinux(string path,ArtifactReadOpenFlags flags){LinuxCalls++;return Open(path,flags);}
    public SafeFileHandle OpenWindows(string path,ArtifactReadOpenFlags flags){WindowsCalls++;return Open(path,flags);}
    public void Dispose()=>Handle?.Dispose();
}
public sealed class ScriptedReadOpenIdentity(ReadOpenCase c,ScriptedReadOpenCalls calls,CancellationTokenSource cancellation) : INativeArtifactIdentityReader
{
    public int Calls {get;private set;}
    public bool SameHandle {get;private set;}
    public bool ClosedInside {get;private set;}
    public NativeArtifactObservation Observe(SafeFileHandle borrowedHandle)
    {
        Calls++;SameHandle=ReferenceEquals(calls.Handle,borrowedHandle);ClosedInside=borrowedHandle.IsClosed;
        if(c.Mode=="identity-error")throw new IOException("identity-error-fixture");
        if(c.Mode=="cancel-identity")cancellation.Cancel();
        if(c.Mode=="identity-closes")borrowedHandle.Dispose();
        return ArtifactReadOpenerCases.Observation with{
            Platform=c.Mode=="platform-mismatch"?NativeArtifactPlatform.Windows:c.Platform!.Value,
            Kind=c.Mode switch{"directory"=>NativeArtifactKind.Directory,"other"=>NativeArtifactKind.Other,_=>NativeArtifactKind.RegularFile}
        };
    }
}

// Scripted contract behavior, not a native opener or field mapper.
public sealed class ScriptedArtifactReadOpener(ReadOpenCase c,INativeArtifactReadOpenCalls calls,INativeArtifactIdentityReader identity,string? mutation=null) : IArtifactReadOpener
{
    public AdmittedArtifactReadHandle OpenRead(string path,CancellationToken token=default)
    {
        SafeFileHandle? owned=null;var transferred=false;
        try{
            if(c.Mode is "unsupported" or "cancel-before"){
                if(mutation=="call-on-rejected")owned=calls.OpenLinux(path,ArtifactReadOpenFlags.ReadOnly);
                if(mutation=="admit")return new(new SafeFileHandle(new IntPtr(3456),false),ArtifactReadOpenerCases.Observation);
                if(mutation=="wrong-error")throw new FormatException("wrong-error");
                if(c.Mode=="cancel-before")token.ThrowIfCancellationRequested();
                throw new PlatformNotSupportedException("unsupported-fixture");
            }
            var flags=ArtifactReadOpenFlags.ReadOnly|ArtifactReadOpenFlags.NoFollow|ArtifactReadOpenFlags.CloseOnExec;
            if(c.Platform==NativeArtifactPlatform.Linux)flags|=ArtifactReadOpenFlags.NonBlocking;
            flags=mutation switch{
                "missing-readonly"=>flags&~ArtifactReadOpenFlags.ReadOnly,"missing-nofollow"=>flags&~ArtifactReadOpenFlags.NoFollow,
                "missing-closeonexec"=>flags&~ArtifactReadOpenFlags.CloseOnExec,"missing-nonblock"=>flags&~ArtifactReadOpenFlags.NonBlocking,
                "extra-flags"=>flags|(ArtifactReadOpenFlags)16,_=>flags
            };
            var linux=c.Platform==NativeArtifactPlatform.Linux;if(mutation=="wrong-route")linux=!linux;
            var admitted=mutation=="wrong-path"?path+"-wrong":path;
            owned=linux?calls.OpenLinux(admitted,flags):calls.OpenWindows(admitted,flags);
            if(c.Mode=="cancel-open"&&mutation=="identity-after-cancel")_ =identity.Observe(owned);
            if(mutation!="admit")token.ThrowIfCancellationRequested();
            if(c.Mode is "invalid" or "closed"){
                if(mutation!="admit")throw new IOException("invalid-owned-handle");
            }
            NativeArtifactObservation observation;
            if(mutation=="skip-identity")observation=ArtifactReadOpenerCases.Observation with{Platform=c.Platform!.Value};
            else if(mutation=="different-identity-handle"){
                using var other=new SafeFileHandle(new IntPtr(2345),false);observation=identity.Observe(other);
            }else observation=identity.Observe(owned);
            if(mutation!="admit"){
                token.ThrowIfCancellationRequested();
                if(c.Mode is "directory" or "other" or "platform-mismatch" or "identity-closes")throw new IOException("admission-rejected");
            }
            if(mutation=="wrong-observation")observation=observation with{FileIdLow=99};
            if(mutation=="closed-success")owned.Dispose();
            var resultHandle=mutation=="different-returned-handle"?new SafeFileHandle(new IntPtr(3456),false):owned;
            transferred=true;return new(resultHandle,observation);
        }catch(Exception ex) when(ex is IOException or OperationCanceledException){
            if(mutation=="admit")return new(new SafeFileHandle(new IntPtr(3456),false),ArtifactReadOpenerCases.Observation);
            if(mutation=="wrong-error")throw new FormatException("wrong-error");
            if(mutation=="wrong-error-message")throw new IOException("wrong-message");
            throw;
        }finally{if(!transferred&&mutation!="leak-failed-handle")owned?.Dispose();}
    }
}

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-007")]
public sealed class ArtifactReadOpenerMockTests
{
    [Theory,MemberData(nameof(ArtifactReadOpenerCases.Cases),MemberType=typeof(ArtifactReadOpenerCases))]
    public Task CorrectContractResponsesPass(ReadOpenCase c)=>ArtifactReadOpenerCases.AssertCase(c,(calls,identity)=>new ScriptedArtifactReadOpener(c,calls,identity));
    [Theory,MemberData(nameof(ArtifactReadOpenerCases.Cases),MemberType=typeof(ArtifactReadOpenerCases))]
    public async Task IndividualWrongAdmissionAndOwnershipAreRejected(ReadOpenCase c)
    {
        var mutations=ArtifactReadOpenerCases.Mutations(c).AsEnumerable();
        if(c.Platform==NativeArtifactPlatform.Linux&&c.Mode=="regular")mutations=mutations.Append("missing-nonblock");
        foreach(var mutation in mutations)
            Assert.NotNull(await Record.ExceptionAsync(()=>ArtifactReadOpenerCases.AssertCase(c,(calls,identity)=>new ScriptedArtifactReadOpener(c,calls,identity,mutation))));
    }
}
