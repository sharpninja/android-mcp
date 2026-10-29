using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationBootstrapCompletionRealTests
{
    [Theory,MemberData(nameof(BootstrapCompletionCases.Cases),MemberType=typeof(BootstrapCompletionCases))]
    public Task SameEarlyCommitFailureAndRetentionCasesBindNativeStoresAndRuntime(string id)=>BootstrapCompletionCases.AssertContract(id,Probe);
    private static async Task<BootstrapCompletionObservation> Probe(string id)
    {
        var root=Path.Combine(Path.GetTempPath(),"bootstrap-completion",Guid.NewGuid().ToString("N"));
        var sourceRoot=Path.Combine(root,"source");var outputRoot=Path.Combine(root,"bootstrap");
        var source=new FileArtifactStore(sourceRoot,bytes=>ContentHash.Sha256(bytes.Span));var output=new FileArtifactStore(outputRoot,bytes=>ContentHash.Sha256(bytes.Span));
        var e=new BootstrapReportEnvironment("GOOD");var properties=new Artifact(BootstrapCliCases.PropertiesPath,"captured properties\n"u8.ToArray(),"application/octet-stream");
        if(id!="INPUT-MISSING")await source.WriteAsync(e.Lock);
        await source.WriteAsync(id=="PROPERTY-CHANGED"?properties with{Bytes="changed properties\n"u8.ToArray()}:properties);
        if(id=="OUTPUT-EXISTS")await output.WriteAsync(new("generation-manifest.json","occupied"u8.ToArray(),"application/json"));
        var reads=0;var committed=false;var trace=new List<string>{"complete"};
        var observedSource=new ObservedStore(source,read:path=>{if(path=="toolchain-lock.json")reads++;});
        var observedOutput=new ObservedStore(output,write:async artifact=>{
            if(artifact.RelativePath=="toolchain-bootstrap-receipt.json"&&id=="SOURCE-CHANGED")await File.WriteAllBytesAsync(ArtifactPaths.Resolve(sourceRoot,e.Lock.RelativePath),"changed lock"u8.ToArray());
            if(artifact.RelativePath=="generation-manifest.json"){
                committed=true;trace.Add("commit");
                if(id=="RECEIPT-CHANGED"){
                    var path=ArtifactPaths.Resolve(outputRoot,"toolchain-bootstrap-receipt.json");var bytes=await File.ReadAllBytesAsync(path);bytes[0]^=1;await File.WriteAllBytesAsync(path,bytes);
                }
            }
        });
        var verifier=new ArtifactReceiptVerifier(new NativeContentHash());
        var runtime=new ObservedRuntime(new ArtifactGenerationRuntime(observedSource,observedOutput,new FileArtifactLeaseProvider(Path.Combine(root,"leases")),verifier,new DocumentationGenerationManifestFactory(),"bootstrap"));
        var completion=new DocumentationBootstrapCompletion(runtime,new PinnedArtifactReader(output),new DocumentationBootstrapReportProjector(e,verifier),verifier);
        string? error=null;var printed="";var downstream=false;var same=false;
        try{
            var result=await completion.CompleteAsync("bootstrap",e.Original.Profile,e.Tools,properties);
            trace.Add("print");printed=result.Summary;
            Assert.True(committed);trace.Add("downstream");downstream=true;
            if(id=="RENDER-FAIL")throw new ArchitectureAssetException("render","Downstream renderer failed after bootstrap commit");
            var retained=await output.ReadAsync(result.Receipt.RelativePath);same=result.Receipt.Bytes.Span.SequenceEqual(retained.Span);
        }
        catch(ArtifactGenerationException failure){error=failure.Code;}
        catch(PinnedArtifactException failure){error=failure.Code;}
        catch(ArchitectureAssetException failure){error=failure.Code;}
        return new(error,committed,File.Exists(ArtifactPaths.Resolve(outputRoot,"toolchain-bootstrap-receipt.json")),printed,downstream,same,
            runtime.Recipe!.Sources.Select(spec=>spec.RelativePath).ToArray(),runtime.Recipe.Sources[1].ExpectedIdentity==verifier.Describe(properties),reads,trace);
    }
    private sealed class ObservedRuntime(IArtifactGenerationRuntime inner):IArtifactGenerationRuntime
    {
        public ArtifactGenerationRecipe? Recipe {get;private set;}
        public Task<ArtifactGenerationResult> ExecuteAsync(string generation,ArtifactGenerationRecipe recipe,CancellationToken token=default)
        {Recipe=recipe;return inner.ExecuteAsync(generation,recipe,token);}
    }
    private sealed class ObservedStore(IArtifactStore inner,Action<string>? read=null,Func<Artifact,Task>? write=null):IArtifactStore
    {
        public Task<ReadOnlyMemory<byte>> ReadAsync(string path,CancellationToken token=default){read?.Invoke(path);return inner.ReadAsync(path,token);}
        public async Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken token=default){var result=await inner.WriteAsync(artifact,token);if(write is not null)await write(artifact);return result;}
    }
}
