namespace AndroidMcp.BuildTool.Documentation;

// The original entrypoint remains available. Orchestration that already parsed the
// admitted blueprint passes that exact model to the same renderer through this overload.
public interface IParsedDocumentationProjector:IDocumentationProjector
{
    DocumentationProjection Project(DocumentationInput input,DocumentationModel model);
}
