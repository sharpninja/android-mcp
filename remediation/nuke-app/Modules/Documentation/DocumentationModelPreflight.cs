using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

/// <summary>Pure composition of the existing parsed model, module metadata, original clauses and renderer.</summary>
public sealed class DocumentationModelPreflight(IDocumentationModelReader models,IParsedDocumentationProjector ui,
    IModelContractValidator contracts,IModuleGraphValidator modules,IStrictJsonInspector json):IDocumentationModelPreflight
{
    public DocumentationModelPreflightResult Prepare(DocumentationModelPreflightInput input)
    {
        Artifact Read(string path)=>input.RequiredArtifacts.Single(artifact=>artifact.RelativePath==path);
        var code=Read("architecture/code-diagrams.md");var dsl=Read("architecture/workspace.dsl");
        var design=Read(DocumentationAssetPaths.Design);var blueprint=Read(DocumentationAssetPaths.Blueprint);
        var inspection=json.Inspect(Read("architecture/module-contract.json").Bytes);
        if(inspection.CanonicalDiagnostics.Count>0||inspection.Value is null||inspection.Value.Value.ValueKind!=JsonValueKind.Object)
            throw new ArchitectureAssetException("MODULE_CONTRACT","Strict module-contract inspection failed");
        var module=JsonSerializer.SerializeToNode(inspection.Value.Value)!.AsObject();
        var metadata=modules.Validate(new ModuleValidationInput(module,Encoding.UTF8.GetString(code.Bytes.Span)));
        if(input.EnforceModuleGraph&&metadata.Issues.Count>0)throw new ArchitectureAssetException("MODULE_CONTRACT","Architecture module contract failed");
        if(metadata.DeclaredClasses is null)throw new ModelContractException(["Module declaration authority missing"]);
        var model=models.Read(blueprint);
        var copies=input.Sources.Sources.Where(row=>row.Observation.Source.Role==DocumentationInputRole.ContractSnapshotRequired).Select(row=>
        {
            var source=row.Observation.Source;
            var canonical=input.Sources.Sources.Single(candidate=>candidate.Observation.Source.Role==DocumentationInputRole.CanonicalContractOptional&&candidate.Observation.Source.ContractId==source.ContractId).Observation.Content;
            return new ContractCopyObservation(Path.GetFileName(source.RelativePath),source.ExpectedSha256??throw new ModelContractException(["Contract source pin absent"]),Read(source.RelativePath),canonical);
        }).ToArray();
        var validated=contracts.Validate(new(code,dsl,design,model,module,metadata.DeclaredClasses,copies));
        var projection=ui.Project(new(blueprint,design),model);
        return new(model,projection,metadata,validated);
    }
}
