using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

public sealed record ContractCopyObservation(string File,string ExpectedSha256,Artifact Snapshot,Artifact? Canonical);
public sealed record ModelContractInput(Artifact CodeMarkdown,Artifact WorkspaceDsl,Artifact DesignMarkdown,
    DocumentationModel Model,JsonObject? ModuleContract,IReadOnlyList<string>? DeclaredClasses,
    IReadOnlyList<ContractCopyObservation> ContractCopies);
public sealed record ModelContractValidationResult(bool Validated,string Scope,int Screens,int States);
public sealed class ModelContractException(IReadOnlyList<string> errors):IOException(string.Join("\n",errors))
{
    public string Code=>"MODEL_CONTRACT";
    public IReadOnlyList<string> Errors {get;}=errors.ToArray();
}
public interface IModelContractValidator
{
    ModelContractValidationResult Validate(ModelContractInput input);
}
