using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed class NativeSealedCacheProbe:ISealedCacheProbe
{
    public async Task<SealedCacheObservation> ExecuteAsync(SealedCacheFixture fixture,CancellationToken cancellationToken)
    {
        var store=new ObservedStore(fixture.Files,cancellationToken);
        var cache=new SourceCache(store,new ScriptedLeaseProvider(),new CanonicalJson());
        SourceCacheLookup? lookup=null;SourceCachePublication? publication=null;SourceCacheReceipt? legacy=null;
        var error=await Record.ExceptionAsync(async()=>
        {
            if(fixture.Case.Operation=="lookup")lookup=await cache.LookupAsync(fixture.Request,fixture.Seal!,cancellationToken);
            else if(fixture.Case.Operation=="publish")publication=await cache.PublishAsync(fixture.Request,cancellationToken);
            else legacy=await cache.WriteAsync(fixture.Request,cancellationToken);
        });
        return new(lookup,publication,legacy,error,store.Reads,store.Writes,store.OriginalsRetained,store.TokenForwarded);
    }
    private sealed class ObservedStore:IArtifactStore
    {
        private readonly Dictionary<string,Artifact> files;
        private readonly Dictionary<string,byte[]> originals;
        private readonly CancellationToken expectedToken;
        public List<string> Reads {get;}=[];
        public List<string> Writes {get;}=[];
        public bool TokenForwarded {get;private set;}=true;
        public bool OriginalsRetained=>originals.All(pair=>files.TryGetValue(pair.Key,out var item)&&item.Bytes.Span.SequenceEqual(pair.Value));
        public ObservedStore(Dictionary<string,Artifact> initial,CancellationToken token)
        {
            files=initial.ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
            originals=initial.ToDictionary(pair=>pair.Key,pair=>pair.Value.Bytes.ToArray(),StringComparer.Ordinal);expectedToken=token;
        }
        public Task<ReadOnlyMemory<byte>> ReadAsync(string relativePath,CancellationToken cancellationToken=default)
        {
            TokenForwarded&=cancellationToken==expectedToken;Reads.Add(relativePath);cancellationToken.ThrowIfCancellationRequested();
            return files.TryGetValue(relativePath,out var artifact)?Task.FromResult(artifact.Bytes):throw new FileNotFoundException(relativePath);
        }
        public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default)
        {
            TokenForwarded&=cancellationToken==expectedToken;Writes.Add(artifact.RelativePath);cancellationToken.ThrowIfCancellationRequested();
            if(files.TryGetValue(artifact.RelativePath,out var existing)&&!existing.Bytes.Span.SequenceEqual(artifact.Bytes.Span))throw new IOException("Preserve original");
            files[artifact.RelativePath]=artifact;
            return Task.FromResult(new StoredArtifact(artifact.RelativePath,ContentHash.Sha256(artifact.Bytes.Span),artifact.Bytes.Length,artifact.MediaType));
        }
    }
}

[Trait("Gate","Real")]
public sealed class SealedCacheRealTests
{
    [Theory,MemberData(nameof(SealedCacheCases.All),MemberType=typeof(SealedCacheCases))]
    public Task SameSealedAdmissionPublicationAndRetentionCases(SealedCacheCase test)=>SealedCacheCases.AssertCase(test,new NativeSealedCacheProbe());
}
