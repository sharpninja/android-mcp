using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;

namespace AndroidMcp.BuildTool.Catalog;

/// <summary>Pure association of already admitted source evidence with explicit Catalog inputs.</summary>
public sealed class CatalogInventoryBridge(ICanonicalJson canonical):ICatalogInventoryBridge
{
    private static CatalogBuildException Failure(string code,string message)=>new(code,message);
    public CatalogInventoryBridgeResult Adapt(CatalogInventoryBridgeInput input,string relativeManifestPath="catalog-pipeline-input.json")
    {
        ArgumentNullException.ThrowIfNull(input);var metadata=input.Metadata;
        if(metadata.RegistryVersion<1 || metadata.ApiLevel<1 || string.IsNullOrEmpty(metadata.AndroidVersion) || string.IsNullOrEmpty(metadata.Coverage))
            throw Failure("catalog-metadata","Explicit positive registry/API and nonempty Android version/coverage are required");
        if(string.IsNullOrWhiteSpace(input.AcquisitionRawRoot) || !Path.IsPathFullyQualified(input.AcquisitionRawRoot))
            throw Failure("catalog-raw-root","Acquisition raw root must be explicit and absolute");
        _=ArtifactPaths.Segments(relativeManifestPath);
        var receipt=input.VerifiedInventory;var projection=receipt.Acquisition.Receipt.Projection;
        if(canonical.Hash(JsonSerializer.SerializeToElement(projection))!=canonical.Hash(JsonSerializer.SerializeToElement(input.SourceExtraction.Request.Projection)))
            throw Failure("catalog-extraction-projection","Source extraction binding differs from the admitted acquisition projection");
        var selected=projection.SelectedPhysicalSourceIndices.ToHashSet();var entries=receipt.Entries.GroupBy(entry=>entry.PhysicalSourceIndex).ToDictionary(group=>group.Key,group=>group.ToArray());
        var physical=projection.PhysicalSources.ToDictionary(source=>source.PhysicalSourceIndex);var verified=new HashSet<int>();var statuses=new Dictionary<int,string>();var sources=new JsonArray();
        foreach(var row in projection.Rows.OrderBy(row=>row.RowIndex))
        {
            if(row.RowIndex!=sources.Count || !physical.TryGetValue(row.PhysicalSourceIndex,out var source) || !entries.TryGetValue(row.PhysicalSourceIndex,out var proofs) || proofs.Length!=1)
                throw Failure("catalog-inventory-proof","Every logical row needs its exact admitted physical acquisition association");
            if(selected.Contains(row.PhysicalSourceIndex))
            {
                var proof=proofs[0];var acquired=proof.VerifiedSource;
                if(proof.Disposition!=SourceInventoryDisposition.VerifiedSelected || acquired is null || acquired.Status is not ("downloaded" or "reused") ||
                    acquired.Artifact.RelativePath!=source.Pin.LocalPath || acquired.Artifact.Sha256!=source.Pin.Sha256 || source.Pin.SizeBytes is { } length && acquired.Artifact.Length!=length)
                    throw Failure("catalog-inventory-proof","Selected physical source lacks matching verified acquisition evidence");
                verified.Add(row.RowIndex);statuses.Add(row.RowIndex,acquired.Status);
            }
            else
            {
                if(proofs[0].Disposition!=SourceInventoryDisposition.NotSelected || proofs[0].VerifiedSource is not null)
                    throw Failure("catalog-inventory-proof","Unselected physical source cannot carry current verified availability");
                statuses.Add(row.RowIndex,"not-selected");
            }
            // This is an owned clone of the authoritative original element.
            // Downloaded/reused eligibility and root bindings stay outside it.
            sources.Add(JsonNode.Parse(row.OriginalRow.GetRawText()));
        }
        var manifest=new JsonObject
        {
            ["catalog"]=new JsonObject{["registry_version"]=metadata.RegistryVersion,["android_version"]=metadata.AndroidVersion,["api_level"]=metadata.ApiLevel,["coverage"]=metadata.Coverage},
            ["sources"]=sources,["x-native-catalog-input"]=new JsonObject{["contract"]="source-inventory-bridge-v1",["requires_typed_context"]=true}
        };
        return new(new(relativeManifestPath,canonical.Serialize(JsonSerializer.SerializeToElement(manifest)),"application/json"),projection,input.AcquisitionRawRoot,
            verified.ToFrozenSet(),new ReadOnlyDictionary<int,string>(statuses),input.SourceExtraction,true);
    }
}
