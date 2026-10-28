using AndroidMcp.BuildTool.Modules.Sources.Acquisition;

namespace AndroidMcp.BuildTool.Documentation;

public interface IDocumentationToolBootstrap
{
    Task<IReadOnlyList<ToolArtifactReceipt>> AcquireAsync(DocumentationRendererProfile profile,ToolArtifactAccess access,
        string? offlineGradleChecksumProof,CancellationToken cancellationToken=default);
}
