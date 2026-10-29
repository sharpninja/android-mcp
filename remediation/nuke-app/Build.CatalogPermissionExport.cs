using System.Text.Json;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool;

public sealed partial class Build
{
    private async Task ExportCatalogPermissions()
    {
        var project=AdmitWorkflowRoot(Path.GetFullPath(ProjectRoot!),".");
        var source=AdmitWorkflowRoot(project,SourceCatalog!);var output=AdmitWorkflowRoot(project,OutputRoot!);
        var result=await new PermissionExportOperation(project,new CanonicalJson(),new NativeContentHash()).RunAsync(new(source,output));
        Console.WriteLine(JsonSerializer.Serialize(new{permission_names=result.Summary.PermissionNames,
            manifest_definition_records=result.Summary.ManifestDefinitionRecords,source_catalog=result.Summary.SourceCatalog,
            complete=result.Summary.Complete,artifacts=result.Artifacts}));
    }
}
