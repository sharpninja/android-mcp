using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

// Pure source/payload composition only. Existing source/runtime/tool/manifest
// authorities still own acquisition, pin checks, precommit rechecks and writes.
public interface IDocumentationPackageSupport
{
    IReadOnlyList<ArtifactSourceSpec> Describe(DocumentationRendererProfile profile);
    ArtifactGenerationPreparation Append(DocumentationRendererProfile profile,ArtifactGenerationPreparation child,
        IReadOnlyList<Artifact> captured);
}
