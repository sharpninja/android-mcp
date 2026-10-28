using System.Text.Json;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;

namespace AndroidMcp.BuildTool.Publication;

// Data contract owned by Publication's sole recipe parser. Acquisition consumes
// these typed pins and serializes audit elements; it never reparses the recipe.
// All collections and JsonElements are detached immutable snapshots on output.
public sealed record RehydrationRowContext(
    string? Repository, string? Revision, string? SourceSurface, string? Scope,
    string? License, JsonElement? SourceReleaseContext,
    IReadOnlyList<JsonElement> SelectionOrigins, IReadOnlyList<string> SelectionReasons);

public sealed record RehydrationSourceRow(
    int RowIndex, string LogicalPath, string? Kind, int PhysicalSourceIndex,
    int? CacheAliasIndex, RehydrationRowContext Context, JsonElement OriginalRow);

public enum RehydrationPrerequisiteCode
{
    RequiredImmutableSnapshot,
    CanonicalArchiveETag,
    CanonicalArchivePlatformVersion,
    CanonicalArchiveRepositoryXml,
    CanonicalArchiveSize,
    SourceRevision,
    HistoricalCacheKind,
    HistoricalCacheRecordCount
}

// Missing proof does not make planning perform effects or invent facts.
// Acquisition preflights every selected requirement before any network/write,
// except exact existing-target SHA/size/Git reuse may avoid upstream proof.
public sealed record RehydrationPrerequisite(
    RehydrationPrerequisiteCode Code, IReadOnlyList<int> SelectionRowIndices,
    string Path, string Reason);

public sealed record RehydrationPhysicalSource(
    int PhysicalSourceIndex, SourcePin Pin, IReadOnlyList<int> SelectionRowIndices,
    IReadOnlyList<SnapshotRequirement> RequiredSnapshots,
    IReadOnlyList<RehydrationPrerequisite> Prerequisites);

public sealed record RehydrationCacheAlias(
    int CacheAliasIndex, string CachePath, HistoricalCachePin? Pin,
    IReadOnlyList<int> SelectionRowIndices,
    IReadOnlyList<RehydrationPrerequisite> Prerequisites);

public sealed record RehydrationSourceProjection(
    string Release, string RecipeSha256, AcquisitionCompatibilityProfile Profile,
    IReadOnlyList<RehydrationSourceRow> Rows,
    IReadOnlyList<RehydrationPhysicalSource> PhysicalSources,
    IReadOnlyList<RehydrationCacheAlias> CacheAliases,
    IReadOnlyList<int> SelectedPhysicalSourceIndices, bool Resume);
