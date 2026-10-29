using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;

namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;

public sealed record SourcesInventoryRequest(RehydrationPlanRequest Recipe,StoredArtifact AcquisitionManifest,
    string ManifestPath="sources/inventory.json");
public enum SourceInventoryDisposition { VerifiedSelected,NotSelected }
public sealed record SourceInventoryEntry(int PhysicalSourceIndex,SourceInventoryDisposition Disposition,AcquiredSource? VerifiedSource);
// Acquisition retains the sole Publication projection, original logical rows and exact provenance/pins.
// NotSelected says nothing about existence or validity of an unexamined path.
public sealed record SourcesInventoryReceipt(SourceBatchResult Acquisition,IReadOnlyList<SourceInventoryEntry> Entries,
    int VerifiedPhysicalSources,int UnselectedPhysicalSources,bool RawComplete,bool ExtractionComplete,
    bool HistoricalByteIdentityClaimed);
public sealed record SourcesInventoryResult(SourcesInventoryReceipt Receipt,StoredArtifact Manifest);
public interface ISourcesInventoryOperation
{
    Task<SourcesInventoryResult> RunAsync(SourcesInventoryRequest request,CancellationToken cancellationToken=default);
}
public sealed class SourcesInventoryException(string code,IReadOnlyList<PublicationIssue> issues)
    :InvalidOperationException(code+": "+string.Join("; ",issues.Select(issue=>issue.Code+": "+issue.Path)))
{
    public string Code{get;}=code;
    public IReadOnlyList<PublicationIssue> Issues{get;}=Array.AsReadOnly(issues.ToArray());
}
