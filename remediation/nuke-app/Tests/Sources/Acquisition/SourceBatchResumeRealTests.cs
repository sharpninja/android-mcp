using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed class NativeSourceBatchResumeProbe:ISourceBatchResumeProbe
{
    public async Task<SourceBatchResumeObservation> RunAsync(SourceBatchResumeFixture fixture,CancellationToken cancellationToken)
    {
        var store=new ObservedResumeStore(fixture.Initial,cancellationToken);var transport=new ForbiddenResumeTransport();
        var admitted=true;
        if(fixture.Case.AdmittedThenRaced)
        {
            var bytes=store.Files[fixture.Request.Acquisition.ReceiptPath];var pin=fixture.Request.RequiredManifest;
            admitted=bytes.Length==pin.Length&&ContentHash.Sha256(bytes)==pin.Sha256;
            if(fixture.Case.Id=="race-missing")store.Files.Remove(pin.RelativePath);else store.Files[pin.RelativePath]=fixture.RacedReplacement();
        }
        store.SealOriginals();
        ISourceBatchReader reader=new SourceBatch(transport,store,new MemoryAcquisitionStore(),new ScriptedLeaseProvider(),SourceBatchCases.Canonical);
        SourceBatchResult? result=null;
        var error=await Record.ExceptionAsync(async()=>{result=await reader.ResumeOnlyAsync(fixture.Request,cancellationToken);});
        return new(result,error,store.Reads,transport.Calls,store.Writes,store.OriginalsRetained,store.TokenForwarded,admitted);
    }
    private sealed class ForbiddenResumeTransport:IByteTransport
    {
        public int Calls{get;private set;}
        public Task<TransportResponse> SendAsync(PinnedRequest request,CancellationToken cancellationToken=default)
        {Calls++;throw new InvalidOperationException("Resume-only mode entered HTTP acquisition");}
    }
    private sealed class ObservedResumeStore(IReadOnlyDictionary<string,byte[]> initial,CancellationToken expectedToken):IArtifactStore
    {
        public Dictionary<string,byte[]> Files{get;}=initial.ToDictionary(pair=>pair.Key,pair=>pair.Value.ToArray(),StringComparer.Ordinal);
        private Dictionary<string,byte[]> originals=[];
        public List<string> Reads{get;}=[];
        public int Writes{get;private set;}
        public bool TokenForwarded{get;private set;}=true;
        public bool OriginalsRetained=>originals.All(pair=>Files.TryGetValue(pair.Key,out var bytes)&&bytes.AsSpan().SequenceEqual(pair.Value));
        public void SealOriginals()=>originals=Files.ToDictionary(pair=>pair.Key,pair=>pair.Value.ToArray(),StringComparer.Ordinal);
        public Task<ReadOnlyMemory<byte>> ReadAsync(string relativePath,CancellationToken cancellationToken=default)
        {
            Reads.Add(relativePath);TokenForwarded&=cancellationToken==expectedToken;cancellationToken.ThrowIfCancellationRequested();
            return Files.TryGetValue(relativePath,out var bytes)?Task.FromResult<ReadOnlyMemory<byte>>(bytes):throw new FileNotFoundException(relativePath);
        }
        public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default)
        {Writes++;throw new InvalidOperationException("Resume-only mode attempted a write");}
    }
}
[Trait("Gate","Real")]
public sealed class SourceBatchResumeRealTests
{
    [Theory,MemberData(nameof(SourceBatchResumeCases.All),MemberType=typeof(SourceBatchResumeCases))]
    public Task SameReadModePinsRacesAndNoEffects(SourceBatchResumeCase test)=>SourceBatchResumeCases.AssertCase(test,new NativeSourceBatchResumeProbe());
}
