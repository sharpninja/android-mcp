using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;

namespace AndroidMcp.BuildTool.Catalog;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CatalogPipelineMetadata(long RegistryVersion,string AndroidVersion,long ApiLevel,string Coverage,
    IReadOnlyList<CatalogSourceNormalization>? SourceNormalization=null);

// Receipt/projection are admitted by Acquisition. Provided Catalog metadata is
// separate from these exact OriginalRows and their Source context.
public sealed record CatalogInventoryBridgeInput(SourcesInventoryReceipt VerifiedInventory,CatalogPipelineMetadata Metadata,
    string AcquisitionRawRoot,CatalogSourceIntake SourceExtraction);

public sealed record CatalogInventoryBridgeResult(Artifact LegacyManifest,RehydrationSourceProjection Projection,
    string AcquisitionRawRoot,IReadOnlySet<int> VerifiedLogicalRows,IReadOnlyDictionary<int,string> NativeStatuses,
    CatalogSourceIntake SourceExtraction,bool RequiresTypedContext=true,
    IReadOnlyDictionary<int,JsonObject>? NormalizedSources=null);

public interface ICatalogInventoryBridge
{
    CatalogInventoryBridgeResult Adapt(CatalogInventoryBridgeInput input,string relativeManifestPath="catalog-pipeline-input.json");
}
