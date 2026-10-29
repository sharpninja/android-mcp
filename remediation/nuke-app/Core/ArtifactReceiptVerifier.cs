namespace AndroidMcp.BuildTool.Core;

public interface IArtifactReceiptVerifier
{
    StoredArtifact Describe(Artifact artifact);
    bool Matches(Artifact artifact,StoredArtifact stored);
}

/// <summary>One metadata projection and equality rule; hashes come from the shared provider.</summary>
public sealed class ArtifactReceiptVerifier(IContentHash hashes):IArtifactReceiptVerifier
{
    public StoredArtifact Describe(Artifact artifact)=>new(artifact.RelativePath,hashes.Sha256(artifact.Bytes.Span),artifact.Bytes.Length,artifact.MediaType);
    public bool Matches(Artifact artifact,StoredArtifact stored)=>stored==Describe(artifact);
}
