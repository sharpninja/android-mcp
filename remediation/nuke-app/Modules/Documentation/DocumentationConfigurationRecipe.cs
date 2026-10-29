using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

// Existing caller configuration binding, shared by direct contract/runtime
// proofs. The immutable runtime owns reads, pin admission and commit rechecks.
public sealed class DocumentationConfigurationRecipe(IArtifactReceiptVerifier verifier)
{
    public ArtifactGenerationRecipe Bind(ArtifactGenerationRecipe inner,IReadOnlyList<Artifact> configuration,string? policyRelativePath=null)
    {
        var basePaths=inner.Sources.Select(spec=>spec.RelativePath).ToHashSet(StringComparer.Ordinal);
        if(policyRelativePath is not null&&basePaths.Contains(policyRelativePath))throw new InvalidOperationException("Package policy cannot replace a declared documentation source.");
        var identities=configuration.ToDictionary(artifact=>artifact.RelativePath,verifier.Describe,StringComparer.Ordinal);
        return new(inner.Sources.Select(spec=>identities.TryGetValue(spec.RelativePath,out var identity)?spec with{ExpectedIdentity=identity}:spec)
            .Concat(configuration.Where(artifact=>!basePaths.Contains(artifact.RelativePath)).Select(artifact=>new ArtifactSourceSpec(artifact.RelativePath,artifact.MediaType,identities[artifact.RelativePath]))).ToArray(),all=>
        {
            var prepared=inner.Prepare(all.Where(artifact=>basePaths.Contains(artifact.RelativePath)).ToArray());
            if(policyRelativePath is not null&&prepared.OutputPaths.Contains(policyRelativePath,StringComparer.OrdinalIgnoreCase))throw new InvalidOperationException("Package policy is configuration and cannot be a published output.");
            return prepared;
        });
    }
}
