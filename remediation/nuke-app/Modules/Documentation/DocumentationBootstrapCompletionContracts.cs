using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;

namespace AndroidMcp.BuildTool.Documentation;

public interface IDocumentationBootstrapCompletion
{
    Task<DocumentationBootstrapReport> CompleteAsync(string generation,DocumentationRendererProfile profile,
        IReadOnlyList<ToolArtifactReceipt> tools,Artifact capturedProperties,CancellationToken cancellationToken=default);
}
