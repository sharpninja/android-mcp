using System.Text.Json;
using AndroidMcp.BuildTool.Catalog.Migration;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool;

public sealed partial class Build
{
    private async Task MigrateCatalog()
    {
        var project=AdmitWorkflowRoot(Path.GetFullPath(ProjectRoot!),".");var canonical=new CanonicalJson();
        var requests=new PinnedArtifactReader(new FileArtifactStore(project,bytes=>ContentHash.Sha256(bytes.Span)));
        var pin=new StoredArtifact(InputRelative(project,PipelineRequest!),PipelineRequestSha256!,
            PipelineRequestLength??throw new InvalidOperationException("Explicit migration request length required"),"application/json");
        var file=await new CatalogMigrationWorkflowReader(requests,canonical).ReadAsync(pin);
        string FilePath(string requested)
        {
            var full=Path.GetFullPath(requested,project);
            return ArtifactPaths.Resolve(AdmitWorkflowRoot(project,Path.GetDirectoryName(full)!),Path.GetFileName(full));
        }
        var catalog=AdmitWorkflowRoot(project,file.CatalogRoot!);var source=FilePath(file.SourceDatabase!);var destination=FilePath(file.DestinationDatabase!);
        var migration=new CatalogCopyMigration(catalog,CreateConfiguredArtifactStore);
        var result=await migration.MigrateAsync(source,destination);
        Console.WriteLine(JsonSerializer.Serialize(new{Operation="Migrate",result.Source,result.Destination,result.RetainedStage,result.TargetMigrations}));
    }
}
