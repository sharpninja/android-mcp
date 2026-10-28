using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

public interface IArchitectureStageProjectionComposer:IArchitectureProjectionComposer
{
    ArtifactGenerationPreparation PrepareSubset(DesignStagePlan selection,IReadOnlyList<Artifact> inputs,
        DocumentationRendererProfile? profile,IReadOnlyList<Artifact> codeViews,ArchitectureRendererContext? context,
        Func<CancellationToken,Task>? admitTools);
}
