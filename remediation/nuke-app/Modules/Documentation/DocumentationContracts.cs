using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

// Contract only. No production projection implementation exists before the
// independent mock assertion gate is accepted.
public sealed record DocumentationInput(Artifact BlueprintMarkdown, Artifact DesignMarkdown);
public sealed record DocumentationProjection(IReadOnlyList<Artifact> Artifacts);

public interface IDocumentationProjector
{
    DocumentationProjection Project(DocumentationInput input);
}
