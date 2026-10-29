using AndroidMcp.BuildTool.Modules.Sources.Acquisition;

namespace AndroidMcp.BuildTool.Documentation;

// Typed original bootstrap phase boundaries. Concrete effects compose the
// one shared tree/permission service and the already admitted wrapper source.
public interface IDocumentationBootstrapFileEffects
{
    Task GradleExtractedAsync(DocumentationRendererProfile profile,ToolArtifactReceipt gradle,CancellationToken cancellationToken=default);
    Task WrappersAcquiredAsync(DocumentationRendererProfile profile,CancellationToken cancellationToken=default);
}
