using Microsoft.Win32.SafeHandles;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

// SAME reader cases and assertions with real paths/streams and controlled
// identity observations. This is not a native OS identity or FIFO-open proof.
public sealed class FileStreamingReaderHarness : IStreamingReaderHarness
{
    private readonly string retainedRoot=Path.Combine(Path.GetTempPath(),"android-mcp-reader-"+Guid.NewGuid().ToString("N"));
    private string fixturePath="";
    private IArtifactReadSession? watched;
    private readonly ControlledIdentity identity=new();
    public IStreamingArtifactReader Reader {get;private set;}=null!;
    public bool OwnedSessionClosed=>watched is not null&&!watched.Content.CanRead;
    public bool NativeHandleExposed=>watched is not null&&(ExposesHandle(watched)||ExposesHandle(watched.Content));
    private static bool ExposesHandle(object value)=>value is SafeFileHandle||value is FileStream||
        value.GetType().GetProperties().Any(p=>typeof(SafeFileHandle).IsAssignableFrom(p.PropertyType));
    public void WatchOwnedSession(IArtifactReadSession session)=>watched=session;
    public void SwapCurrentIdentity()=>identity.Swapped=true;
    public byte[] RetainedFixture()=>File.ReadAllBytes(fixturePath);
    public void Prepare(StreamingReaderCase c)
    {
        Directory.CreateDirectory(retainedRoot);
        var root=Path.Combine(retainedRoot,"root");Directory.CreateDirectory(root);
        var original=Path.Combine(retainedRoot,"original");Directory.CreateDirectory(original);
        fixturePath=Path.Combine(original,"payload.bin");File.WriteAllBytes(fixturePath,StreamingReaderCases.Abc);
        var payload=Path.Combine(root,"payload.bin");
        switch(c.Mode){
            case "missing": break;
            case "directory": Directory.CreateDirectory(payload);break;
            case "root-link":
                root=Path.Combine(retainedRoot,"root-link");Directory.CreateSymbolicLink(root,original);break;
            case "ancestor-link": Directory.CreateSymbolicLink(Path.Combine(root,"nested"),original);break;
            case "leaf-link": File.CreateSymbolicLink(payload,fixturePath);break;
            case "dangling-link": File.CreateSymbolicLink(payload,Path.Combine(retainedRoot,"absent.bin"));break;
            default: File.WriteAllBytes(payload,StreamingReaderCases.Abc);fixturePath=payload;break;
        }
        identity.Kind=c.Mode switch{"directory"=>NativeArtifactKind.Directory,"other"=>NativeArtifactKind.Other,_=>NativeArtifactKind.RegularFile};
        identity.Swapped=false;watched=null;
        Reader=new FileArtifactStore(root,bytes=>ContentHash.Sha256(bytes.Span),streamSha256:null,
            readOpener:new ArtifactReadOpener(new ControlledReadCalls(),identity));
    }
    // A controlled Windows-shaped opening fixture matches the existing declared
    // identity on every host. Only known regular fixture bytes use File.OpenHandle;
    // this is not a Windows syscall or FIFO-safe-opening runtime proof.
    private sealed class ControlledReadCalls : INativeArtifactReadOpenCalls
    {
        public NativeArtifactPlatform? Platform=>NativeArtifactPlatform.Windows;
        public SafeFileHandle OpenLinux(string path,ArtifactReadOpenFlags flags)=>
            throw new InvalidOperationException("The controlled reader fixture selected the wrong route.");
        public SafeFileHandle OpenWindows(string path,ArtifactReadOpenFlags flags)
        {
            if(flags!=(ArtifactReadOpenFlags.ReadOnly|ArtifactReadOpenFlags.NoFollow|ArtifactReadOpenFlags.CloseOnExec))
                throw new InvalidOperationException("The controlled reader fixture received wrong opening intents.");
            if(Directory.Exists(path))throw new IOException("Controlled directory rejection.");
            return File.OpenHandle(path,FileMode.Open,FileAccess.Read,FileShare.Read,FileOptions.None);
        }
    }
    private sealed class ControlledIdentity : INativeArtifactIdentityReader
    {
        public NativeArtifactKind Kind {get;set;}
        public bool Swapped {get;set;}
        public NativeArtifactObservation Observe(SafeFileHandle borrowedHandle)
        {
            if(borrowedHandle.IsClosed||borrowedHandle.IsInvalid)throw new IOException("Reader passed a closed or invalid borrowed handle.");
            return StreamingReaderCases.Identity with{Kind=Kind,FileIdLow=Swapped?17UL:11UL};
        }
    }
}

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-007")]
public sealed class StreamingReaderRealTests
{
    [Theory,MemberData(nameof(StreamingReaderCases.Cases),MemberType=typeof(StreamingReaderCases))]
    public Task SameCasesWithRealPathsAndControlledIdentity(StreamingReaderCase c)=>
        StreamingReaderCases.AssertCase(c,new FileStreamingReaderHarness());
}
