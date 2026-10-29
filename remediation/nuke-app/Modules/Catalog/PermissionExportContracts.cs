using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Catalog;

public sealed record PermissionExportRequest(string SourceCatalogRoot,string OutputRoot);
public sealed record PermissionExportSummary(long PermissionNames,long ManifestDefinitionRecords,string SourceCatalog,bool Complete);
public sealed record PermissionExportResult(PermissionExportSummary Summary,IReadOnlyList<StoredArtifact> Artifacts);
public interface IPermissionExportOperation
{
    Task<PermissionExportResult> RunAsync(PermissionExportRequest request,CancellationToken cancellationToken=default);
}
public sealed class PermissionExportException(string code,string message):InvalidOperationException(message)
{
    public string Code {get;}=code;
}
