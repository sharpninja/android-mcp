using System.Text.Json;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Sources;
using AndroidMcp.BuildTool.Sources.Java;

namespace AndroidMcp.BuildTool;

public sealed partial class Build
{
    private async Task BuildCatalogFromSources()
    {
        var project=AdmitWorkflowRoot(Path.GetFullPath(ProjectRoot!),".");
        var canonical=new CanonicalJson();
        var inputs=new PinnedArtifactReader(new FileArtifactStore(project,bytes=>ContentHash.Sha256(bytes.Span)));
        var pin=new StoredArtifact(InputRelative(project,PipelineRequest!),PipelineRequestSha256!,
            PipelineRequestLength??throw new InvalidOperationException("Explicit workflow request length required."),"application/json");
        var admitted=await new CatalogPipelineWorkflowReader(inputs,new SourceWorkflowInputsReader(inputs),canonical).ReadAsync(pin);
        var wire=admitted.File;
        var catalog=AdmitWorkflowRoot(project,wire.CatalogRoot);
        var inventoryRoot=AdmitWorkflowRoot(project,wire.Stores.InventoryRoot);
        var rawRoot=AdmitWorkflowRoot(project,wire.Stores.AcquisitionRawRoot);
        var generationRoot=AdmitWorkflowRoot(project,wire.Stores.GenerationStoreRoot);
        var output=AdmitWorkflowRoot(project,wire.OutputRoot);
        var stage=AdmitWorkflowRoot(project,wire.StageRoot);
        var comparer=OperatingSystem.IsWindows()?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal;
        var retained=new[]{project,catalog,inventoryRoot,rawRoot,generationRoot};
        if(comparer.Equals(output,stage)||retained.Contains(output,comparer)||retained.Contains(stage,comparer))
            throw new InvalidOperationException("Catalog output/private stage require separate roots from retained workflow inputs.");
        var java=wire.Java is null?null:wire.Java with{ManifestPath=ArtifactPaths.Resolve(project,InputRelative(project,wire.Java.ManifestPath))};
        var raw=new FileArtifactStore(rawRoot,bytes=>ContentHash.Sha256(bytes.Span));
        var generations=new FileArtifactStore(generationRoot,bytes=>ContentHash.Sha256(bytes.Span));
        var leases=new FileArtifactLeaseProvider(Path.Combine(RootDirectory.ToString(),".nuke","catalog-sources-leases"));
        ISourceBatchReader acquisition=new SourceBatch(HttpByteTransport.Create(),raw,raw,leases,canonical);
        var inventory=new SourcesInventoryReader(new PinnedArtifactReader(new FileArtifactStore(inventoryRoot,bytes=>ContentHash.Sha256(bytes.Span))),acquisition,canonical);
        var javaLoader=new JavaSourceRuntimeLoader();var factory=new SourcesRuntimeFactory(javaLoader);
        var source=new SourcesExtractService(raw,generations,factory,new SourceCache(generations,leases,canonical),canonical,leases);
        var workflow=new CatalogPipelineWorkflowOperation(catalog,rawRoot,new RehydrationPlanner(canonical),inventory,
            new SourceProducerIdentityBuilder(javaLoader:javaLoader),source,factory,canonical);
        var result=await workflow.RunAsync(admitted.Request with{OutputRoot=output,StageRoot=stage,Java=java});
        Console.WriteLine(JsonSerializer.Serialize(new{result.RegistryId,result.ProducerFingerprint,result.Artifacts,
            Counts=result.Validation["counts"],Complete=false,OutputRoot=output,wire.SourceGenerationCommit}));
    }
}
