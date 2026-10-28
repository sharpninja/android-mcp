using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed class NativeSourceSeedProbe:ISourceSeedProbe
{
    public async Task<SourceSeedObservation> RunAsync(SourceSeedFixture fixture,CancellationToken cancellationToken)
    {
        var store=new SeedStore(fixture.Existing,cancellationToken);var bundle=new SeedStore(fixture.Bundle,cancellationToken);
        var transport=new SeedTransport();ISourceBatchSeeder engine=new SourceBatch(transport,store,bundle,new ScriptedLeaseProvider(),SourceBatchCases.Canonical);
        SourceSeedResult? result=null;var error=await Record.ExceptionAsync(async()=>{result=await engine.SeedOnlyAsync(fixture.Request,cancellationToken);});
        return new(result,error,store.Reads,bundle.Reads,store.Writes,transport.Calls,store.OriginalsRetained,bundle.OriginalsRetained,
            store.Writes.Count==0||store.Writes[^1]==fixture.Request.Acquisition.ReceiptPath,store.TokenForwarded&&bundle.TokenForwarded);
    }
    private sealed class SeedTransport:IByteTransport
    {
        public int Calls{get;private set;}
        public Task<TransportResponse> SendAsync(PinnedRequest request,CancellationToken cancellationToken=default)
        {Calls++;throw new InvalidOperationException("Local Seed attempted HTTP");}
    }
    private sealed class SeedStore:IArtifactStore
    {
        private readonly MemoryAcquisitionStore inner=new();
        private readonly Dictionary<string,byte[]> originals;
        private readonly CancellationToken expectedToken;
        public List<string> Reads{get;}=[];
        public List<string> Writes{get;}=[];
        public bool TokenForwarded{get;private set;}=true;
        public bool OriginalsRetained=>originals.All(pair=>inner.Files.TryGetValue(pair.Key,out var artifact)&&artifact.Bytes.Span.SequenceEqual(pair.Value));
        public SeedStore(IReadOnlyDictionary<string,byte[]> files,CancellationToken token)
        {
            originals=files.ToDictionary(pair=>pair.Key,pair=>pair.Value.ToArray(),StringComparer.Ordinal);expectedToken=token;
            foreach(var file in files)inner.Files[file.Key]=new(file.Key,file.Value.ToArray(),"application/octet-stream");
        }
        public Task<ReadOnlyMemory<byte>> ReadAsync(string relativePath,CancellationToken cancellationToken=default)
        {Reads.Add(relativePath);TokenForwarded&=cancellationToken==expectedToken;cancellationToken.ThrowIfCancellationRequested();return inner.ReadAsync(relativePath,cancellationToken);}
        public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default)
        {Writes.Add(artifact.RelativePath);TokenForwarded&=cancellationToken==expectedToken;return inner.WriteAsync(artifact,cancellationToken);}
    }
}
[Trait("Gate","Real")]
public sealed class SourceSeedRealTests
{
    [Theory,MemberData(nameof(SourceSeedCases.All),MemberType=typeof(SourceSeedCases))]
    public Task SameLocalWinningAliasPinsCountersAndImmutableReplay(SourceSeedCase test)=>SourceSeedCases.AssertCase(test,new NativeSourceSeedProbe());
}
