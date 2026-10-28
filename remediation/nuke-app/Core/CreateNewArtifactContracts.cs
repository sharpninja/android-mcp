namespace AndroidMcp.BuildTool.Core;

/// <summary>Strict creation in the same immutable namespace as ordinary artifact writes.</summary>
public interface ICreateNewArtifactStore : IArtifactStore
{
    // Success proves this call exclusively created the destination. Any existing
    // file, including identical bytes or a retained partial, must be rejected.
    // Failure/cancellation yields no success receipt; originals/partials remain.
    Task<StoredArtifact> WriteNewAsync(Artifact artifact, CancellationToken cancellationToken = default);
}
