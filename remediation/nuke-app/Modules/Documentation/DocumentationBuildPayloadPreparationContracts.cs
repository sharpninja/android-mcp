using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

public interface IDocumentationBuildPayloadPreparationFactory
{
    Task<ArtifactGenerationPreparation> PrepareAsync(CancellationToken cancellationToken=default);
}
