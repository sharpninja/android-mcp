using System.Text.Json;
using System.Text.Json.Serialization;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;

namespace AndroidMcp.BuildTool.Publication;

// Additive capability. Capture every logical row inside Publication's sole
// PlanCore walk, before physical alias selection. OriginalRow is audit-only.
// All returned collections are detached, read-only snapshots.
public sealed record RehydrationSnapshotRow(int RowIndex, string? Status, SourcePin Pin,
    IReadOnlyList<SnapshotRequirement> RequiredSnapshots,
    IReadOnlyList<RehydrationPrerequisite> Prerequisites, bool StatusPresent = true,
    JsonValueKind StatusKind = JsonValueKind.String,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    RehydrationCanonicalMetadataReference? MetadataReference = null)
{
    public bool DownloadedEligible => StatusPresent && StatusKind == JsonValueKind.String && Status == "downloaded";
}
public sealed record RehydrationSnapshotProjection(string Release, string RecipeSha256,
    AcquisitionCompatibilityProfile Profile, IReadOnlyList<RehydrationSnapshotRow> Rows, bool Resume);
public sealed record RehydrationSnapshotPlanResult(RehydrationPlanResult Baseline,
    RehydrationSnapshotProjection? SnapshotProjection)
{
    public bool IsValid => Baseline.IsValid && SnapshotProjection is not null;
}
public interface IRehydrationSnapshotPlanner : IRehydrationPlanner
{
    RehydrationSnapshotPlanResult PlanSnapshot(RehydrationPlanRequest request);
}
