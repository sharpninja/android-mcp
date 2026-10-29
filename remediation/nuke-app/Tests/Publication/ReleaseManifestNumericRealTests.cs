using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Publication;

// Production codec binding through the SAME9 mock-proven assertions. Controlled
// dependency boundaries prove admission only, not payload/OS/reassembly success.
public sealed class NativeReleaseNumericProbe:IReleaseNumericProbe
{
    public async Task<ReleaseNumericObservation> RunAsync(ReleaseNumericCase test)
    {
        var state=new BoundaryState(ReleaseNumericCases.Raw(test));var store=new BoundaryStore(state);
        var codec=new ReleasePartsCodec(new BoundaryReader(state),store,new PinnedArtifactReader(store),
            new StrictJsonInspector(),new CanonicalJson(),new BoundaryLeaseProvider());
        Exception? error=null;
        try{await codec.ReassembleAsync(new("release/manifest.json","generation","final.bin",ContentHash.Sha256(state.Raw)));}
        catch(Exception actual){error=actual;}
        return new(error!,state.Error,state.Boundary,state.ControlReads,state.PayloadOpens,state.StreamWriteAttempts,
            state.ByteWrites,0,state.Before.AsSpan().SequenceEqual(state.Raw));
    }
    private sealed class BoundaryState(byte[] raw)
    {
        public byte[] Raw{get;}=raw;
        public byte[] Before{get;}=raw.ToArray();
        public Exception Error{get;}=new NotSupportedException("numeric-admission-dependency-boundary");
        public string? Boundary{get;set;}
        public int ControlReads{get;set;}
        public int PayloadOpens{get;set;}
        public int StreamWriteAttempts{get;set;}
        public int ByteWrites{get;set;}
    }
    private sealed class BoundaryReader(BoundaryState state):IStreamingArtifactReader
    {
        public Task<ArtifactReadSnapshot?> InspectAsync(string path,CancellationToken token=default)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult<ArtifactReadSnapshot?>(path=="release/manifest.json"
                ?new(path,new(NativeArtifactPlatform.Linux,NativeArtifactKind.RegularFile,7,11,0,state.Raw.Length,123,0))
                :null);
        }
        public Task<IArtifactReadSession> OpenReadAsync(string path,CancellationToken token=default)
        {
            token.ThrowIfCancellationRequested();Assert.Equal("release/artifact.part-0001",path);
            state.PayloadOpens++;state.Boundary="part-open";throw state.Error;
        }
    }
    private sealed class BoundaryStore(BoundaryState state):ICreateNewStreamingArtifactStore
    {
        public Task<ReadOnlyMemory<byte>> ReadAsync(string path,CancellationToken token=default)
        {
            token.ThrowIfCancellationRequested();Assert.Equal("release/manifest.json",path);state.ControlReads++;
            return Task.FromResult<ReadOnlyMemory<byte>>(state.Raw.ToArray());
        }
        private Task<StoredArtifact> UnexpectedWrite(){state.ByteWrites++;throw new InvalidOperationException("Unexpected output before numeric boundary");}
        public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken token=default)=>UnexpectedWrite();
        public Task<StoredArtifact> WriteNewAsync(Artifact artifact,CancellationToken token=default)=>UnexpectedWrite();
        public Task<StoredArtifact> WriteStreamAsync(string path,string type,Func<Stream,CancellationToken,Task> producer,CancellationToken token=default)=>UnexpectedWrite();
        public Task<StoredArtifact> WriteNewStreamAsync(string path,string type,Func<Stream,CancellationToken,Task> producer,CancellationToken token=default)
        {
            token.ThrowIfCancellationRequested();Assert.Equal("generation/retained-whole.unverified",path);
            Assert.Equal("application/octet-stream",type);
            state.StreamWriteAttempts++;state.Boundary="empty-whole-write";throw state.Error;
        }
    }
    private sealed class BoundaryLeaseProvider:IArtifactLeaseProvider
    {
        public ValueTask<IAsyncDisposable> AcquireAsync(string scope,CancellationToken token=default)
        {
            token.ThrowIfCancellationRequested();Assert.Equal("release-codec:generation",scope);
            return ValueTask.FromResult<IAsyncDisposable>(new BoundaryLease());
        }
        private sealed class BoundaryLease:IAsyncDisposable{public ValueTask DisposeAsync()=>ValueTask.CompletedTask;}
    }
}

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-007")]
public sealed class ReleaseManifestNumericRealTests
{
    [Theory,MemberData(nameof(ReleaseNumericCases.Cases),MemberType=typeof(ReleaseNumericCases))]
    public Task SameOriginalNumericAdmissionCasesBindProductionCodec(ReleaseNumericCase test)=>
        ReleaseNumericCases.AssertCase(test,new NativeReleaseNumericProbe());
}
