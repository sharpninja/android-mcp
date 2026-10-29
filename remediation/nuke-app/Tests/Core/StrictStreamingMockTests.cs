using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

// Staged proposal only, not copied or executed. Native binds this SAME assertion body.
public sealed record StrictStreamingCase(string Id,string Mode);
public interface IStrictStreamingHarness
{
    ICreateNewStreamingArtifactStore Store { get; }
    ICreateNewStreamingArtifactStore Peer { get; }
    bool Exists(string path);
}
public static class StrictStreamingCases
{
    private static readonly byte[] Abc=[97,98,99],Other=[120,121,122];
    private const string Path="payload.bin",Media="application/octet-stream";
    public static IEnumerable<object[]> Cases=>new StrictStreamingCase[]{
        new StrictStreamingCase("STRICT-STREAM-NEW","new"),new("STRICT-STREAM-EMPTY","empty"),
        new("STRICT-STREAM-IDENTICAL","identical"),new("STRICT-STREAM-DIFFERENT","different"),
        new("STRICT-STREAM-RETAINED-PARTIAL","partial"),new("STRICT-STREAM-CANCEL-BEFORE","cancel-before"),
        new("STRICT-STREAM-PRODUCER-FAIL","producer-fail"),new("STRICT-STREAM-CANCEL-AFTER","cancel-after"),
        new("STRICT-STREAM-OPEN-WRITER","open-writer")
    }.Select(c=>new object[]{c});
    public static string[] Mutations(StrictStreamingCase c)=>c.Mode switch {
        "identical" or "different" or "partial"=>["existing-success","existing-overwrite","producer-on-existing"],
        "cancel-before"=>["cancel-ignored","producer-on-cancel"],
        "producer-fail"=>["failure-success","partial-drop"],
        "cancel-after"=>["cancel-ignored","partial-drop"],
        "open-writer"=>["existing-success","existing-overwrite"],
        _=>["receipt-path","receipt-sha","receipt-length","receipt-media","producer-zero","producer-twice","readable","seekable","wrong-bytes"]
    };
    public static async Task AssertCase(StrictStreamingCase c,IStrictStreamingHarness harness)
    {
        var store=harness.Store;byte[]? original=null;
        if(c.Mode is "identical" or "different") {
            original=c.Mode=="different"?Other:Abc;
            await store.WriteAsync(new(Path,original,Media));
        }
        if(c.Mode=="partial") {
            var error=await Record.ExceptionAsync(()=>store.WriteStreamAsync(Path,Media,async(s,t)=>{
                await s.WriteAsync(new byte[]{97},t);throw new IOException("seed-partial");}));
            Assert.IsType<IOException>(error);original=[97];
        }
        if(c.Mode=="open-writer") {
            var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var first=store.WriteNewStreamAsync(Path,Media,async(s,t)=>{
                await s.WriteAsync(new byte[]{97},t);entered.SetResult();await release.Task;
                await s.WriteAsync(new byte[]{98,99},t);});
            await Task.WhenAny(entered.Task,first);
            if(!entered.Task.IsCompletedSuccessfully){await first;throw new InvalidOperationException("Writer never entered.");}
            var peerCalls=0;Exception? rejection;
            try { rejection=await Record.ExceptionAsync(()=>harness.Peer.WriteNewStreamAsync(Path,Media,async(s,t)=>{
                peerCalls++;await s.WriteAsync(Other,t);})); }
            finally { release.SetResult(); }
            await first;Assert.IsType<IOException>(rejection);Assert.Equal(0,peerCalls);
            Assert.Equal(Abc,(await store.ReadAsync(Path)).ToArray());return;
        }
        using var cancellation=new CancellationTokenSource();
        if(c.Mode=="cancel-before")cancellation.Cancel();
        var calls=0;StoredArtifact? receipt=null;
        var failure=await Record.ExceptionAsync(async()=>receipt=await store.WriteNewStreamAsync(Path,Media,async(s,t)=>{
            calls++;Assert.True(s.CanWrite);Assert.False(s.CanRead);Assert.False(s.CanSeek);
            if(c.Mode!="empty")await s.WriteAsync(c.Mode is "producer-fail" or "cancel-after"?new byte[]{97}:Abc,t);
            if(c.Mode=="producer-fail")throw new IOException("producer-fixture");
            if(c.Mode=="cancel-after")cancellation.Cancel();
        },cancellation.Token));
        if(original is not null) {
            Assert.IsType<IOException>(failure);Assert.Null(receipt);Assert.Equal(0,calls);
            Assert.Equal(original,(await store.ReadAsync(Path)).ToArray());return;
        }
        if(c.Mode=="cancel-before") {
            Assert.IsAssignableFrom<OperationCanceledException>(failure);Assert.Null(receipt);
            Assert.Equal(0,calls);Assert.False(harness.Exists(Path));return;
        }
        if(c.Mode is "producer-fail" or "cancel-after") {
            Assert.Null(receipt);Assert.Equal(1,calls);Assert.True(harness.Exists(Path));
            if(c.Mode=="producer-fail"){Assert.IsType<IOException>(failure);Assert.Equal("producer-fixture",failure!.Message);}
            else Assert.IsAssignableFrom<OperationCanceledException>(failure);
            Assert.Equal(new byte[]{97},(await store.ReadAsync(Path)).ToArray());return;
        }
        Assert.Null(failure);Assert.NotNull(receipt);Assert.Equal(1,calls);
        Assert.Equal(Path,receipt!.RelativePath);Assert.Equal(Media,receipt.MediaType);
        Assert.Equal(c.Mode=="empty"?0:3,receipt.Length);
        Assert.Equal(c.Mode=="empty"?ArtifactStoreCases.EmptySha:ArtifactStoreCases.AbcSha,receipt.Sha256);
        Assert.Equal(c.Mode=="empty"?Array.Empty<byte>():Abc,(await store.ReadAsync(Path)).ToArray());
    }
}

public sealed class ScriptedStrictStreamingHarness(string? mutation=null) : IStrictStreamingHarness
{
    private readonly State state=new();
    public ICreateNewStreamingArtifactStore Store=>new StoreAdapter(state,mutation);
    public ICreateNewStreamingArtifactStore Peer=>new StoreAdapter(state,mutation);
    public bool Exists(string path){lock(state)return state.Files.ContainsKey(path);}
    private sealed class State
    {
        public readonly Dictionary<string,byte[]> Files=new(StringComparer.Ordinal);
        public readonly HashSet<string> Corrupted=new(StringComparer.Ordinal);
    }
    private sealed class FixtureOutput(bool readable=false,bool seekable=false) : MemoryStream
    {
        public override bool CanRead=>readable;
        public override bool CanSeek=>seekable;
    }
    private sealed class StoreAdapter(State state,string? mutation) : ICreateNewStreamingArtifactStore
    {
        public Task<ReadOnlyMemory<byte>> ReadAsync(string path,CancellationToken token=default)
        {token.ThrowIfCancellationRequested();lock(state)return Task.FromResult<ReadOnlyMemory<byte>>(state.Files[path].ToArray());}
        public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken token=default)
        {
            token.ThrowIfCancellationRequested();ArtifactPaths.Segments(artifact.RelativePath);
            var bytes=artifact.Bytes.ToArray();
            lock(state){
                if(state.Files.TryGetValue(artifact.RelativePath,out var old)&&!old.SequenceEqual(bytes))throw new IOException("existing-conflict");
                state.Files[artifact.RelativePath]=bytes;
            }
            return Task.FromResult(Receipt(artifact.RelativePath,artifact.MediaType,bytes,false));
        }
        public Task<StoredArtifact> WriteNewAsync(Artifact artifact,CancellationToken token=default)=>
            WriteNewStreamAsync(artifact.RelativePath,artifact.MediaType,(stream,cancellation)=>stream.WriteAsync(artifact.Bytes,cancellation).AsTask(),token);
        public Task<StoredArtifact> WriteStreamAsync(string path,string media,Func<Stream,CancellationToken,Task> producer,CancellationToken token=default)=>
            StreamWrite(path,media,producer,token,false);
        public Task<StoredArtifact> WriteNewStreamAsync(string path,string media,Func<Stream,CancellationToken,Task> producer,CancellationToken token=default)=>
            StreamWrite(path,media,producer,token,true);
        private async Task<StoredArtifact> StreamWrite(string path,string media,Func<Stream,CancellationToken,Task> producer,CancellationToken token,bool strict)
        {
            ArtifactPaths.Segments(path);ArgumentNullException.ThrowIfNull(producer);
            if(strict&&token.IsCancellationRequested&&mutation=="producer-on-cancel"){
                using var scratch=new FixtureOutput();await producer(scratch,CancellationToken.None);
            }
            var activeToken=strict&&mutation=="cancel-ignored"?CancellationToken.None:token;
            activeToken.ThrowIfCancellationRequested();
            byte[]? old;lock(state)state.Files.TryGetValue(path,out old);
            if(strict&&old is not null){
                if(mutation=="existing-success")return Receipt(path,media,old,false);
                if(mutation=="existing-overwrite"){
                    lock(state){state.Files[path]=[255];state.Corrupted.Add(path);}
                }
                if(mutation=="producer-on-existing"){
                    using var scratch=new FixtureOutput();await producer(scratch,activeToken);
                }
                throw new IOException("existing-retained");
            }
            lock(state){
                // Correct fake models CreateNew atomically; ordinary setup writes
                // retain the old bytes and compare after its producer completes.
                if(strict&&state.Files.ContainsKey(path))throw new IOException("competing-creator");
                if(old is null)state.Files[path]=[];
            }
            using var output=new FixtureOutput(strict&&mutation=="readable",strict&&mutation=="seekable");
            var failed=false;
            try{
                if(!strict||mutation!="producer-zero")await producer(output,activeToken);
                if(strict&&mutation=="producer-twice")await producer(output,activeToken);
                activeToken.ThrowIfCancellationRequested();
                var bytes=output.ToArray();
                if(old is not null&&!old.SequenceEqual(bytes))throw new IOException("existing-conflict");
                return Receipt(path,media,bytes,strict);
            }catch when(strict&&mutation=="failure-success"){
                failed=true;return Receipt(path,media,output.ToArray(),true);
            }catch{failed=true;throw;}
            finally{
                lock(state){
                    if(old is null&&!state.Corrupted.Contains(path)&&!(strict&&failed&&mutation=="partial-drop")){
                        var bytes=output.ToArray();
                        if(strict&&mutation=="wrong-bytes")bytes=[255];
                        state.Files[path]=bytes;
                    }
                }
            }
        }
        private StoredArtifact Receipt(string path,string media,byte[] bytes,bool strict)=>new(
            strict&&mutation=="receipt-path"?"wrong":path,
            strict&&mutation=="receipt-sha"?"wrong":ContentHash.Sha256(bytes),
            strict&&mutation=="receipt-length"?bytes.Length+1:bytes.Length,
            strict&&mutation=="receipt-media"?"wrong":media);
    }
}

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-007")]
public sealed class StrictStreamingMockTests
{
    [Theory,MemberData(nameof(StrictStreamingCases.Cases),MemberType=typeof(StrictStreamingCases))]
    public Task CorrectMocksPass(StrictStreamingCase c)=>StrictStreamingCases.AssertCase(c,new ScriptedStrictStreamingHarness());
    [Theory,MemberData(nameof(StrictStreamingCases.Cases),MemberType=typeof(StrictStreamingCases))]
    public async Task IndividualWrongOutcomesAreRejected(StrictStreamingCase c)
    {
        foreach(var mutation in StrictStreamingCases.Mutations(c))
            Assert.NotNull(await Record.ExceptionAsync(()=>StrictStreamingCases.AssertCase(c,new ScriptedStrictStreamingHarness(mutation))));
    }
}
