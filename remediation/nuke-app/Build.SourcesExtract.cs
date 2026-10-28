using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Execution;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Sources;
using AndroidMcp.BuildTool.Sources.Java;

namespace AndroidMcp.BuildTool;

public sealed partial class Build
{
    private async Task ExtractSources()
    {
        var project=Path.GetFullPath(ProjectRoot!);
        _=ArtifactPaths.Resolve(project,".sources-extract-admission");
        var canonical=new CanonicalJson();
        var inputs=new PinnedArtifactReader(new FileArtifactStore(project,bytes=>ContentHash.Sha256(bytes.Span)));
        var outerPin=new StoredArtifact(InputRelative(project,PipelineRequest!),PipelineRequestSha256!,
            PipelineRequestLength??throw new InvalidOperationException("Explicit workflow request length required."),"application/json");
        var wire=JsonSerializer.Deserialize<SourcesExtractWorkflowFile>(canonical.Serialize(await inputs.ReadAsync(outerPin)))
            ??throw new InvalidOperationException("Source extraction workflow request is required.");
        var recipe=await new SourceWorkflowInputsReader(inputs).ReadAsync(wire.Sources);
        var inventoryRoot=AdmitWorkflowRoot(project,wire.Stores.InventoryRoot);
        var rawRoot=AdmitWorkflowRoot(project,wire.Stores.AcquisitionRawRoot);
        var generationRoot=AdmitWorkflowRoot(project,wire.Stores.GenerationStoreRoot);
        var comparison=OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
        if(new[]{project,inventoryRoot,rawRoot}.Any(root=>string.Equals(Path.TrimEndingDirectorySeparator(root),generationRoot,comparison)))
            throw new InvalidOperationException("Source generations require a store separate from retained project, inventory and raw inputs.");
        var java=wire.Java is null?null:wire.Java with{ManifestPath=ArtifactPaths.Resolve(project,InputRelative(project,wire.Java.ManifestPath))};
        var raw=new FileArtifactStore(rawRoot,bytes=>ContentHash.Sha256(bytes.Span));
        var generations=new FileArtifactStore(generationRoot,bytes=>ContentHash.Sha256(bytes.Span));
        var leases=new FileArtifactLeaseProvider(Path.Combine(RootDirectory.ToString(),".nuke","source-extract-leases"));
        ISourceBatchReader acquisition=new SourceBatch(HttpByteTransport.Create(),raw,raw,leases,canonical);
        var inventory=new SourcesInventoryReader(new PinnedArtifactReader(new FileArtifactStore(inventoryRoot,bytes=>ContentHash.Sha256(bytes.Span))),acquisition,canonical);
        var javaLoader=new JavaSourceRuntimeLoader();
        var extraction=new SourcesExtractService(raw,generations,new SourcesRuntimeFactory(javaLoader),
            new SourceCache(generations,leases,canonical),canonical,leases);
        var workflow=new SourcesExtractWorkflowOperation(new RehydrationPlanner(canonical),inventory,
            new SourceProducerIdentityBuilder(javaLoader:javaLoader),extraction);
        var result=await workflow.RunAsync(new(recipe,wire.InventoryManifest,wire.GenerationRoot,wire.ProfileId,
            wire.TrustedPriorIndex,java,wire.CacheSourceLimit));
        Console.WriteLine(JsonSerializer.Serialize(new{result.Index,result.SelectedLogicalCacheRows,result.UniqueCacheAliases,
            result.CacheHits,result.RawExtractions,GenerationStoreRoot=generationRoot,wire.GenerationRoot}));
    }
}
