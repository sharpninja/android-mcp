using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;

public sealed record SnapshotMetadataProofEvidence(string SourcePath,string SourceSha256,StoredArtifact MetadataReceipt,StoredArtifact RepositoryXml,
    string IntegrityScope="local-input-integrity-only",bool OfficialSourceAuthority=false,
    string? DeclaredArchiveSha256=null,bool WholeArchiveHashVerified=false);
public sealed record SnapshotMetadataProofAdmission(SourcePin Pin,SnapshotMetadataProofEvidence? Evidence);
public sealed record SourceSnapshotMetadataProof(int RowIndex,string RecipeSha256,SnapshotMetadataProofEvidence Evidence);
public interface ISnapshotMetadataProofReader
{
    Task<SnapshotMetadataProofAdmission> BindAsync(SourcePin source,RehydrationCanonicalMetadataReference reference,
        CancellationToken cancellationToken=default);
}
