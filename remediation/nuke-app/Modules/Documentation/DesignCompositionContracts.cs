using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;

namespace AndroidMcp.BuildTool.Documentation;

public sealed record DesignArchitectureRequest(string GenerationId,ArchitectureRendererContext Renderer,
    ToolArtifactAccess ToolAccess,string? OfflineGradleChecksumProof);
public sealed record DesignPackageRequest(string GenerationId,IReadOnlyList<StoredArtifact> Sources,
    IReadOnlyList<string> RequiredPaths,ReadOnlyMemory<byte> Policy);
public sealed record ArchitectureCompositionPlan(IReadOnlyList<Artifact> Inputs,DocumentationRendererProfile Profile,
    IReadOnlyList<Artifact> CodeViews,IReadOnlyList<string> OutputPaths);
public interface IArchitectureProjectionComposer
{
    ArchitectureCompositionPlan Plan(IReadOnlyList<Artifact> inputs,DocumentationRendererProfile profile,IReadOnlyList<Artifact> codeViews);
    Task<IReadOnlyList<Artifact>> RenderAsync(ArchitectureCompositionPlan plan,ArchitectureRendererContext renderer,
        IReadOnlyList<ToolArtifactReceipt> admittedTools,CancellationToken cancellationToken=default);
}
public interface IDesignCompositionRecipeFactory
{
    ArtifactGenerationRecipe Architecture(DesignArchitectureRequest request);
    ArtifactGenerationRecipe Package(DesignPackageRequest request);
}
