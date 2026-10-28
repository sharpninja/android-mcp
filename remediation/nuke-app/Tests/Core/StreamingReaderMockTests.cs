using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed record StreamingReaderCase(string Id,string Mode,string Path="payload.bin");
public interface IStreamingReaderHarness
{
    IStreamingArtifactReader Reader { get; }
    void Prepare(StreamingReaderCase c);
    void WatchOwnedSession(IArtifactReadSession session);
    void SwapCurrentIdentity();
    bool OwnedSessionClosed { get; }
    bool NativeHandleExposed { get; }
    byte[] RetainedFixture();
}
public static class StreamingReaderCases
{
    public static readonly byte[] Abc=[97,98,99];
    // Reader tests use a declared identity-provider fixture. Actual OS mapping
    // and native Linux discriminator proof belong to their separate gate.
    public static readonly NativeArtifactObservation Identity=new(NativeArtifactPlatform.Windows,
        NativeArtifactKind.RegularFile,7,11,13,3,123456789,987654300);
    public static IEnumerable<object[]> Cases=>new StreamingReaderCase[]{
        new("READ-REGULAR","regular"),new("READ-PATH-IDENTITY-SWAP","swap"),new("READ-CANCEL-DURING","read-cancel"),
        new("READ-MISSING","missing"),new("READ-DIRECTORY","directory"),new("READ-OTHER","other"),
        new("READ-CANCEL-BEFORE","cancel-before"),new("READ-ROOT-LINK","root-link"),
        new("READ-ANCESTOR-LINK","ancestor-link","nested/payload.bin"),new("READ-LEAF-LINK","leaf-link"),new("READ-DANGLING-LINK","dangling-link"),
        new("READ-EMPTY-PATH","invalid",""),new("READ-PARENT","invalid","../escape"),
        new("READ-INTERNAL-PARENT","invalid","nested/../escape"),new("READ-ABSOLUTE","invalid","/absolute"),
        new("READ-DRIVE","invalid","C:/drive"),new("READ-BACKSLASH","invalid","a\\escape"),new("READ-DOUBLE-SLASH","invalid","a//escape")
    }.Select(c=>new object[]{c});
    public static string[] Mutations(StreamingReaderCase c)=>c.Mode switch {
        "missing"=>["missing-present","missing-open","fixture-overwrite"],
        "directory" or "other" or "invalid" or "root-link" or "ancestor-link" or "leaf-link" or "dangling-link"=>["admit","fixture-overwrite"],
        "cancel-before"=>["ignore-cancel","fixture-overwrite"],
        _=>new string[]{"snapshot-path","platform","kind","device","file-low","file-high","length","seconds","nanoseconds",
            "unreadable","writable","wrong-bytes","extra-bytes","close-on-open","close-on-recheck","never-close","handle-exposed","fixture-overwrite"}
            .Concat(c.Mode=="swap"?new[]{"swap-ignored"}:c.Mode=="read-cancel"?new[]{"ignore-read-cancel"}:Array.Empty<string>()).ToArray()
    };
    public static async Task AssertCase(StreamingReaderCase c,IStreamingReaderHarness harness)
    {
        harness.Prepare(c);var reader=harness.Reader;
        using var cancellation=new CancellationTokenSource();
        if(c.Mode=="cancel-before")cancellation.Cancel();
        if(c.Mode is "directory" or "other" or "invalid" or "root-link" or "ancestor-link" or "leaf-link" or "dangling-link" or "cancel-before"){
            var inspect=await Record.ExceptionAsync(async()=>{_ =await reader.InspectAsync(c.Path,cancellation.Token);});
            var open=await Record.ExceptionAsync(async()=>{await using var unexpected=await reader.OpenReadAsync(c.Path,cancellation.Token);});
            if(c.Mode=="cancel-before"){Assert.IsAssignableFrom<OperationCanceledException>(inspect);Assert.IsAssignableFrom<OperationCanceledException>(open);}
            else if(c.Mode is "directory" or "other"){Assert.IsAssignableFrom<IOException>(inspect);Assert.IsAssignableFrom<IOException>(open);}
            else {Assert.IsType<ArgumentException>(inspect);Assert.IsType<ArgumentException>(open);}
            Assert.Equal(Abc,harness.RetainedFixture());return;
        }
        var observed=await reader.InspectAsync(c.Path,cancellation.Token);
        if(c.Mode=="missing"){
            Assert.Null(observed);
            Assert.IsType<FileNotFoundException>(await Record.ExceptionAsync(async()=>{await using var unexpected=await reader.OpenReadAsync(c.Path);}));
            Assert.Equal(Abc,harness.RetainedFixture());return;
        }
        var expected=new ArtifactReadSnapshot(c.Path,Identity);Assert.Equal(expected,observed);
        var session=await reader.OpenReadAsync(c.Path);
        harness.WatchOwnedSession(session);
        try{
            Assert.Equal(expected,session.Opened);Assert.False(harness.OwnedSessionClosed);Assert.False(harness.NativeHandleExposed);
            Assert.True(session.Content.CanRead);Assert.False(session.Content.CanWrite);
            var bytes=new byte[3];await session.Content.ReadExactlyAsync(bytes);Assert.Equal(Abc,bytes);
            Assert.Equal(0,await session.Content.ReadAsync(new byte[1]));
            if(c.Mode=="read-cancel"){
                cancellation.Cancel();
                Assert.IsAssignableFrom<OperationCanceledException>(await Record.ExceptionAsync(async()=>{_ =await session.Content.ReadAsync(new byte[1],cancellation.Token);}));
            }
            if(c.Mode=="swap")harness.SwapCurrentIdentity();
            var current=await session.ObserveCurrentPathAsync();
            Assert.Equal(c.Mode=="swap"?expected with{Native=Identity with{FileIdLow=17}}:expected,current);
            Assert.Equal(expected,session.Opened);Assert.False(harness.OwnedSessionClosed);
        }finally{await session.DisposeAsync();}
        Assert.True(harness.OwnedSessionClosed);
        Assert.IsType<ObjectDisposedException>(await Record.ExceptionAsync(async()=>{_ =await session.Content.ReadAsync(new byte[1]);}));
        await session.DisposeAsync();Assert.True(harness.OwnedSessionClosed);Assert.Equal(Abc,harness.RetainedFixture());
    }
}

public sealed class ScriptedStreamingReaderHarness(string? mutation=null) : IStreamingReaderHarness
{
    private StreamingReaderCase test=new("unprepared","regular");
    private byte[] fixture=StreamingReaderCases.Abc.ToArray();
    private Session? watched;
    private bool swapped;
    public IStreamingArtifactReader Reader=>new ReaderAdapter(this,mutation);
    public bool OwnedSessionClosed=>watched?.Closed??false;
    public bool NativeHandleExposed=>mutation=="handle-exposed";
    public void Prepare(StreamingReaderCase c){test=c;fixture=StreamingReaderCases.Abc.ToArray();swapped=false;watched=null;}
    public void WatchOwnedSession(IArtifactReadSession session)=>watched=(Session)session;
    public void SwapCurrentIdentity()=>swapped=true;
    public byte[] RetainedFixture()=>fixture.ToArray();
    private ArtifactReadSnapshot Snapshot(string path,bool current=false)
    {
        var value=StreamingReaderCases.Identity;
        if(current&&swapped&&mutation!="swap-ignored")value=value with{FileIdLow=17};
        value=mutation switch {
            "platform"=>value with{Platform=NativeArtifactPlatform.Linux},"kind"=>value with{Kind=NativeArtifactKind.Other},
            "device"=>value with{DeviceOrVolume=99},"file-low"=>value with{FileIdLow=99},"file-high"=>value with{FileIdHigh=99},
            "length"=>value with{Length=99},"seconds"=>value with{ModifiedSeconds=99},"nanoseconds"=>value with{ModifiedNanoseconds=99},_=>value
        };
        return new(mutation=="snapshot-path"?"wrong":path,value);
    }
    private void Admit(string path,CancellationToken token)
    {
        if(mutation=="fixture-overwrite")fixture=[255];
        if(mutation!="ignore-cancel")token.ThrowIfCancellationRequested();
        if(mutation=="admit")return;
        ArtifactPaths.Segments(path);
        if(test.Mode is "root-link" or "ancestor-link" or "leaf-link" or "dangling-link")throw new ArgumentException("reparse-fixture");
        if(test.Mode is "directory" or "other")throw new IOException("regular-file-required");
    }
    private sealed class ReaderAdapter(ScriptedStreamingReaderHarness owner,string? mutation) : IStreamingArtifactReader
    {
        public Task<ArtifactReadSnapshot?> InspectAsync(string path,CancellationToken token=default)
        {
            owner.Admit(path,token);
            return Task.FromResult(owner.test.Mode=="missing"&&mutation!="missing-present"?(ArtifactReadSnapshot?)null:owner.Snapshot(path));
        }
        public Task<IArtifactReadSession> OpenReadAsync(string path,CancellationToken token=default)
        {
            owner.Admit(path,token);
            if(owner.test.Mode=="missing"&&mutation!="missing-open")throw new FileNotFoundException("missing-fixture");
            return Task.FromResult<IArtifactReadSession>(new Session(owner,path,mutation));
        }
    }
    private sealed class FixtureInput(byte[] bytes,string? mutation) : MemoryStream(bytes,false)
    {
        public override bool CanRead=>mutation!="unreadable"&&base.CanRead;
        public override bool CanWrite=>mutation=="writable";
        public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token=default)=>
            base.ReadAsync(buffer,mutation=="ignore-read-cancel"?CancellationToken.None:token);
    }
    private sealed class Session : IArtifactReadSession
    {
        private readonly ScriptedStreamingReaderHarness owner;
        private readonly string path;
        private readonly string? mutation;
        private readonly FixtureInput content;
        public Session(ScriptedStreamingReaderHarness owner,string path,string? mutation)
        {
            this.owner=owner;this.path=path;this.mutation=mutation;
            Opened=owner.Snapshot(path);
            content=new(mutation=="wrong-bytes"?new byte[]{120,121,122}:mutation=="extra-bytes"?new byte[]{97,98,99,100}:StreamingReaderCases.Abc.ToArray(),mutation);
            if(mutation=="close-on-open"){content.Dispose();Closed=true;}
        }
        public bool Closed {get;private set;}
        public Stream Content=>content;
        public ArtifactReadSnapshot Opened {get;}
        public Task<ArtifactReadSnapshot> ObserveCurrentPathAsync(CancellationToken token=default)
        {
            owner.Admit(path,token);
            if(mutation=="close-on-recheck"){content.Dispose();Closed=true;}
            return Task.FromResult(owner.Snapshot(path,true));
        }
        public ValueTask DisposeAsync()
        {
            if(mutation!="never-close"){content.Dispose();Closed=true;}
            return ValueTask.CompletedTask;
        }
    }
}

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-007")]
public sealed class StreamingReaderMockTests
{
    [Theory,MemberData(nameof(StreamingReaderCases.Cases),MemberType=typeof(StreamingReaderCases))]
    public Task CorrectMocksPass(StreamingReaderCase c)=>StreamingReaderCases.AssertCase(c,new ScriptedStreamingReaderHarness());
    [Theory,MemberData(nameof(StreamingReaderCases.Cases),MemberType=typeof(StreamingReaderCases))]
    public async Task IndividualWrongResponsesAndEffectsAreRejected(StreamingReaderCase c)
    {
        foreach(var mutation in StreamingReaderCases.Mutations(c))
            Assert.NotNull(await Record.ExceptionAsync(()=>StreamingReaderCases.AssertCase(c,new ScriptedStreamingReaderHarness(mutation))));
    }
}
