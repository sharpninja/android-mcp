namespace AndroidMcp.BuildTool.Core;

public enum ArtifactReferenceProfile { GradleMarkdown,RepositoryMarkdown,GradleOfflineAsset,RepositoryOfflineAsset }
public enum ArtifactReferenceDisposition { Ignored,Local,Remote,Invalid }
public sealed record ArtifactReferenceRequest(string SourcePath,string Reference,ArtifactReferenceProfile Profile);
public sealed record ArtifactReferenceResolution(ArtifactReferenceDisposition Disposition,string? RootRelativePath,
    bool DirectoryAllowed,string? Reason);
public interface IArtifactReferenceResolver
{
    ArtifactReferenceResolution Resolve(ArtifactReferenceRequest request);
}
