using AndroidMcp.BuildTool.Core;
using System.Text.Json.Serialization;

namespace AndroidMcp.BuildTool.Publication;

// Proposed typed service only. Root owns category/operation CLI admission after gates.
public sealed record ReleaseSplitRequest(string SourcePath, string GenerationPath,
    string ExpectedSha256, long PartBytes = 1073741824);
public sealed record ReleaseReassembleRequest(string ManifestPath, string GenerationPath,
    string DestinationPath, string ExpectedManifestSha256);
public sealed record ReleasePart([property:JsonPropertyName("name")]string Name,
    [property:JsonPropertyName("bytes")]long Bytes,[property:JsonPropertyName("sha256")]string Sha256);
public sealed record ReleasePartsManifest([property:JsonPropertyName("format")]string Format,
    [property:JsonPropertyName("artifact_name")]string ArtifactName,[property:JsonPropertyName("bytes")]long Bytes,
    [property:JsonPropertyName("sha256")]string Sha256,[property:JsonPropertyName("part_bytes")]long PartBytes,
    [property:JsonPropertyName("parts")]IReadOnlyList<ReleasePart> Parts);
public sealed record ReleaseSplitResult(ReleasePartsManifest Manifest, StoredArtifact ManifestArtifact,
    IReadOnlyList<StoredArtifact> Parts, StoredArtifact RetainedWhole);
public sealed record ReleaseReassembleResult(StoredArtifact Artifact, StoredArtifact SuccessMarker,
    StoredArtifact RetainedWhole);
public interface IReleasePartsCodec
{
    Task<ReleaseSplitResult> SplitAsync(ReleaseSplitRequest request, CancellationToken cancellationToken = default);
    Task<ReleaseReassembleResult> ReassembleAsync(ReleaseReassembleRequest request, CancellationToken cancellationToken = default);
}
public sealed class ReleasePartsException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
