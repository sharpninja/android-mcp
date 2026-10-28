using System.Text.Json;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;

namespace AndroidMcp.BuildTool;

public sealed partial class Build
{
    private async Task ValidateCatalog()
    {
        var project=AdmitWorkflowRoot(Path.GetFullPath(ProjectRoot!),".");var canonical=new CanonicalJson();
        var requests=new PinnedArtifactReader(new FileArtifactStore(project,bytes=>ContentHash.Sha256(bytes.Span)));
        var pin=new StoredArtifact(InputRelative(project,PipelineRequest!),PipelineRequestSha256!,
            PipelineRequestLength??throw new InvalidOperationException("Explicit validation request length required"),"application/json");
        var file=await new CatalogValidationWorkflowReader(requests,canonical).ReadAsync(pin);
        // Only compose existing path admission. No new path policy or report writer.
        string FilePath(string requested)
        {
            var full=Path.GetFullPath(requested,project);
            return ArtifactPaths.Resolve(AdmitWorkflowRoot(project,Path.GetDirectoryName(full)!),Path.GetFileName(full));
        }
        var report=FilePath(file.ReportPath!);
        var validator=new CatalogValidator(project,canonical,new AidlLexicalInventorySourceFacts());
        var result=file.Mode==CatalogValidationMode.Release
            ?await validator.ValidateAsync(AdmitWorkflowRoot(project,file.OutputRoot!),FilePath(file.SourceManifest!),report)
            :await validator.ValidateDatabaseAsync(FilePath(file.DatabasePath!),report);
        Console.WriteLine(JsonSerializer.Serialize(new{Mode=file.Mode!.Value.ToString(),result.Report,result.Artifact,result.DatabaseReadOnly}));
    }
}
