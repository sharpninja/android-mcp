using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;

namespace AndroidMcp.BuildTool.Documentation;

// Projection only. Original acquisition, lock reread and receipt publication
// timing are caller responsibilities over the existing services/runtime.
public sealed record DocumentationBootstrapReport(Artifact Receipt,string Summary);
public interface IDocumentationBootstrapReportProjector
{
    DocumentationBootstrapReport Project(DocumentationRendererProfile profile,Artifact finalLock,IReadOnlyList<ToolArtifactReceipt> successfulTools);
}
