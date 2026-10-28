using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

public sealed record DocumentationModelPreflightInput(IReadOnlyList<Artifact> RequiredArtifacts,
    DocumentationModelSourceSnapshot Sources,bool EnforceModuleGraph=true);
public sealed record DocumentationModelPreflightResult(DocumentationModel Model,DocumentationProjection Ui,
    ModuleValidationResult Modules,ModelContractValidationResult Contracts);
public interface IDocumentationModelPreflight
{
    DocumentationModelPreflightResult Prepare(DocumentationModelPreflightInput input);
}
