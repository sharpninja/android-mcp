using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;

namespace AndroidMcp.BuildTool.Publication;

public enum PublicationFileKind { Regular, SymbolicLink, Directory, Missing }
public sealed record PublicationFile(string Path, long Length, PublicationFileKind Kind = PublicationFileKind.Regular);
public sealed record PublicationIssue(string Code, string Path);
public sealed record PublicationCheckResult(int FileCount, long TotalBytes, IReadOnlyList<PublicationIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}
public interface IPublicationChecker
{
    PublicationCheckResult Check(ReadOnlyMemory<byte> policy, IReadOnlyList<PublicationFile> files);
}

public sealed record StageEntry(string SourcePath, string DestinationPath, long Length, string Sha256, string MediaType);
public sealed record StageRequest(string GenerationId, ReadOnlyMemory<byte> Policy, IReadOnlyList<StageEntry> Files);
public sealed record PublicationStageResult(string GenerationId, bool Committed, StoredArtifact? Manifest,
    IReadOnlyList<StoredArtifact> Outputs, IReadOnlyList<PublicationIssue> Issues, DateTimeOffset StartedAt, DateTimeOffset EndedAt);
public interface IPublicationStager
{
    Task<PublicationStageResult> StageAsync(StageRequest request, CancellationToken cancellationToken = default);
}

// The recipe and lock are data. Planning never reads arbitrary source files,
// starts a process, downloads, extracts, or claims a historical byte match.
public sealed record RehydrationPlanRequest(Artifact Recipe, Artifact Lock, string? ExpectedRelease = null,
    int? Limit = null, bool Resume = true, bool AllowFileFixtures = false,
    AcquisitionCompatibilityProfile Profile = AcquisitionCompatibilityProfile.CanonicalRestore);
public sealed record SnapshotRequirement(string SourcePath, string LocalPath, string? BundlePath, string Sha256, long? Length);
public sealed record ArchiveRequirement(string SourcePath,string LocalPath,string Url,string Member,string? ArchiveSha256,string MemberSha256);
public sealed record RehydrationPlan(string Release, string RecipeSha256, int SourceRows, int UniqueSourcePaths,
    long KnownSourceBytes, int UnknownSizePaths, IReadOnlyList<SnapshotRequirement> RequiredSnapshots,
    IReadOnlyList<OperationKey> Operations, Artifact Manifest, bool Network, bool Complete,
    bool HistoricalByteIdentityClaimed, IReadOnlyList<string> Caveats,IReadOnlyList<ArchiveRequirement>? ArchiveMembers=null,
    RehydrationSourceProjection? SourceProjection=null);
public sealed record RehydrationPlanResult(RehydrationPlan? Plan, IReadOnlyList<PublicationIssue> Issues)
{
    public bool IsValid => Plan is not null && Issues.Count == 0;
}
public interface IRehydrationPlanner
{
    RehydrationPlanResult Plan(RehydrationPlanRequest request);
}
