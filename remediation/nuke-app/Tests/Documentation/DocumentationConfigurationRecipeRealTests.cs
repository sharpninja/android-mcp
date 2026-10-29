using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationConfigurationRecipeRealTests
{
    [Fact]
    public Task SamePropertiesPinsInnerScopeOutputsAndRecheckBindNativeComposition()
    {var e=new ConfigurationRecipeEnvironment();return ConfigurationRecipeCases.AssertContract(e,new DocumentationConfigurationRecipe(e.Receipts).Bind);}
    [Theory,InlineData("source-policy"),InlineData("output-policy")]
    public void SameOriginalPolicyCollisionFailuresBindNativeComposition(string scenario)
        =>ConfigurationRecipeCases.AssertCollision(scenario,new DocumentationConfigurationRecipe(new ArtifactReceiptVerifier(new NativeContentHash())).Bind);
    [Theory,MemberData(nameof(DocumentationConfigurationRecipeMockTests.RuntimeCases),MemberType=typeof(DocumentationConfigurationRecipeMockTests))]
    public Task SamePropertiesAdmissionAndCommitCasesBindActualStoresAndImmutableRuntime(string scenario)=>ConfigurationRuntimeCases.AssertContract(scenario,Probe);
    private static async Task<ConfigurationRuntimeObservation> Probe(string scenario)
    {
        var e=new ConfigurationRecipeEnvironment();var root=Path.Combine(Path.GetTempPath(),"documentation-configuration",Guid.NewGuid().ToString("N"));
        var sourceRoot=Path.Combine(root,"source");var source=new FileArtifactStore(sourceRoot,value=>ContentHash.Sha256(value.Span));
        var outputRoot=Path.Combine(root,"output");var output=new FileArtifactStore(outputRoot,value=>ContentHash.Sha256(value.Span));
        foreach(var artifact in e.Configuration)await source.WriteAsync(artifact);
        var reads=0;var recording=new RecordingStore(source,path=>{if(path==e.Properties.RelativePath)reads++;});
        async Task Corrupt()
        {
            // Fault injection into this isolated test's copy only. Keep the
            // original fixture bytes and their admitted artifact unchanged.
            await source.WriteAsync(e.Properties with{RelativePath="retained-original-properties.bin"});
            await File.WriteAllBytesAsync(ArtifactPaths.Resolve(sourceRoot,e.Properties.RelativePath),"changed configuration"u8.ToArray());
        }
        var inner=new ArtifactGenerationRecipe([new(e.Existing.RelativePath,e.Existing.MediaType)],_=>new([e.Output.RelativePath],async token=>{
            token.ThrowIfCancellationRequested();if(scenario=="BEFORE-COMMIT")await Corrupt();return [e.Output];}));
        var recipe=new DocumentationConfigurationRecipe(e.Receipts).Bind(inner,e.Configuration);
        if(scenario=="BEFORE-ADMISSION")await Corrupt();
        string? code=null;
        try{await new ArtifactGenerationRuntime(recording,output,new FileArtifactLeaseProvider(Path.Combine(root,"leases")),e.Receipts,new DocumentationGenerationManifestFactory(),"properties-caller").ExecuteAsync("properties-caller",recipe);}
        catch(ArtifactGenerationException failure){code=failure.Code;}
        return new(code,File.Exists(ArtifactPaths.Resolve(outputRoot,"generation-manifest.json")),File.Exists(ArtifactPaths.Resolve(outputRoot,e.Output.RelativePath)),reads,e.Properties.Bytes.Span.SequenceEqual("admitted wrapper input\n"u8));
    }
    private sealed class RecordingStore(IArtifactStore inner,Action<string> read):IArtifactStore
    {
        public Task<ReadOnlyMemory<byte>> ReadAsync(string path,CancellationToken cancellationToken=default){read(path);return inner.ReadAsync(path,cancellationToken);}
        public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default)=>throw new InvalidOperationException("Configuration caller must never write its source store");
    }
}
