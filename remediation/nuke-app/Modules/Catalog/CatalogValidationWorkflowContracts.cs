using System.Text.Json.Serialization;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Catalog;

public enum CatalogValidationMode { Release, Database }
public static class CatalogValidationWorkflowErrors
{
    public const string Request="CATALOG_VALIDATION_REQUEST";
}

// Mode is nullable so absent mode cannot silently select enum zero/Release.
// Reader uses native string-enum decoding with integer values disabled.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CatalogValidationWorkflowFile(CatalogValidationMode? Mode,string? ReportPath,
    string? OutputRoot=null,string? SourceManifest=null,string? DatabasePath=null);

public interface ICatalogValidationWorkflowReader
{
    Task<CatalogValidationWorkflowFile> ReadAsync(StoredArtifact callerPin,CancellationToken cancellationToken=default);
}
