using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;

namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;

/// <summary>Reconcile a caller-pinned acquisition seal through the sole read-only batch engine.</summary>
public sealed class SourcesInventoryOperation(IRehydrationPlanner planner,ISourceBatchReader reader,
    IArtifactStore acquisition,IArtifactStore output,ICanonicalJson canonical):ISourcesInventoryOperation
{
    public async Task<SourcesInventoryResult> RunAsync(SourcesInventoryRequest request,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _=ArtifactPaths.Segments(request.ManifestPath);_=ArtifactPaths.Segments(request.AcquisitionManifest.RelativePath);
        var result=planner.Plan(request.Recipe);
        if(!result.IsValid)throw new SourcesInventoryException("INVENTORY_PLAN_INVALID",result.Issues);
        var plan=result.Plan!;
        var projection=plan.SourceProjection??throw new SourcesInventoryException("INVENTORY_PROJECTION_REQUIRED",[]);
        if(plan.Network||plan.Complete||plan.HistoricalByteIdentityClaimed)throw new SourcesInventoryException("INVENTORY_PLAN_NOT_PURE",[]);
        cancellationToken.ThrowIfCancellationRequested();
        var pin=request.AcquisitionManifest;
        var bytes=await acquisition.ReadAsync(pin.RelativePath,cancellationToken);
        if(bytes.Length!=pin.Length||ContentHash.Sha256(bytes.Span)!=pin.Sha256||pin.MediaType!="application/json")
            throw new SourcesInventoryException("INVENTORY_ACQUISITION_PIN",[]);
        SourceBatchReceipt prior;
        try{prior=JsonSerializer.Deserialize<SourceBatchReceipt>(bytes.Span)??throw new JsonException("Acquisition receipt missing");}
        catch(Exception problem)when(problem is JsonException or NotSupportedException)
        {throw new SourcesInventoryException("INVENTORY_ACQUISITION_RECEIPT",[]);}
        // The engine verifies its own read against this same pin, closing admission/read races before effects.
        var resumed=await reader.ResumeOnlyAsync(new(new(prior.GenerationId,pin.RelativePath,projection,prior.CachePolicy),pin),cancellationToken);
        if(!resumed.Reused)throw new SourcesInventoryException("INVENTORY_SEALED",[]);
        if(resumed.Manifest!=pin)throw new SourcesInventoryException("INVENTORY_ACQUISITION_PIN",[]);
        var selected=projection.SelectedPhysicalSourceIndices.ToHashSet();
        var entries=projection.PhysicalSources.Select(source=>new SourceInventoryEntry(source.PhysicalSourceIndex,
            selected.Contains(source.PhysicalSourceIndex)?SourceInventoryDisposition.VerifiedSelected:SourceInventoryDisposition.NotSelected,
            selected.Contains(source.PhysicalSourceIndex)?resumed.Receipt.Sources.Single(item=>item.PhysicalSourceIndex==source.PhysicalSourceIndex).Source:null)).ToArray();
        var receipt=new SourcesInventoryReceipt(resumed,entries,selected.Count,projection.PhysicalSources.Count-selected.Count,resumed.Receipt.Complete,false,false);
        var manifest=new Artifact(request.ManifestPath,canonical.Serialize(JsonSerializer.SerializeToElement(receipt)),"application/json");
        return new(receipt,await output.WriteAsync(manifest,cancellationToken));
    }
}
