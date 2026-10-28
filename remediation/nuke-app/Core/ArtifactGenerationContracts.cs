namespace AndroidMcp.BuildTool.Core;

// Shared immutable-generation contracts. Domains provide projection and optional observation checks.
public sealed record ArtifactSourceSpec(string RelativePath,string MediaType,StoredArtifact? ExpectedIdentity=null);
public sealed record ArtifactGenerationPreparation(IReadOnlyList<string> OutputPaths,
    Func<CancellationToken,Task<IReadOnlyList<Artifact>>> MaterializeAsync,
    Func<CancellationToken,Task>? VerifyBeforeCommitAsync=null);
public sealed record ArtifactGenerationRecipe(IReadOnlyList<ArtifactSourceSpec> Sources,
    Func<IReadOnlyList<Artifact>,ArtifactGenerationPreparation> Prepare);
public sealed record ArtifactGenerationResult(string GenerationId,IReadOnlyList<StoredArtifact> Inputs,
    IReadOnlyList<StoredArtifact> Outputs,StoredArtifact Manifest);
public interface IArtifactGenerationRuntime
{
    Task<ArtifactGenerationResult> ExecuteAsync(string generationId,ArtifactGenerationRecipe recipe,CancellationToken cancellationToken=default);
}
public interface IArtifactGenerationManifestFactory
{
    string RelativePath {get;}
    Artifact Create(string generationId,IReadOnlyList<StoredArtifact> inputs,IReadOnlyList<StoredArtifact> outputs);
}
