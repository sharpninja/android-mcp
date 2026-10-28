using AndroidMcp.BuildTool.Modules.Sources.Acquisition;

namespace AndroidMcp.BuildTool.Publication;

// Planned additive capability. It does not add fields to the existing Plan,
// SourcePin, SourceProjection or SourceBatch request serialization.
public interface IRehydrationSeedPlanner : IRehydrationPlanner
{
    RehydrationSeedPlanResult PlanSeed(RehydrationPlanRequest request);
}
public enum RehydrationSeedCandidateKind { LocalPath, RequiredLocalSnapshot }
public sealed record RehydrationSeedCandidate(int CandidateOrdinal,
    RehydrationSeedCandidateKind Kind, string Path);

// All typed values come from the winning last logical row in the SAME recipe
// walk, before ordinary Plan's existing first-physical-pin deduplication.
public sealed record RehydrationSeedSource(int PhysicalSourceIndex,
    int WinningSelectionRowIndex, SourcePin Pin, SnapshotRequirement? RequiredSnapshot,
    IReadOnlyList<RehydrationSeedCandidate> BundleCandidates);
public sealed record RehydrationSeedProjection(string Release, string RecipeSha256,
    IReadOnlyList<RehydrationSeedSource> Sources);
public sealed record RehydrationSeedPlanResult(RehydrationPlanResult BaseResult,
    RehydrationSeedProjection? SeedProjection)
{
    public bool IsValid => BaseResult.IsValid && SeedProjection is not null;
}
