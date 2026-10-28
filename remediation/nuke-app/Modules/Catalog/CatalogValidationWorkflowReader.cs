using System.Text.Json;
using System.Text.Json.Serialization;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Catalog;

/// <summary>Caller-pinned transport admission; validation and report I/O remain in CatalogValidator.</summary>
public sealed class CatalogValidationWorkflowReader(IPinnedArtifactReader requests,ICanonicalJson canonical):ICatalogValidationWorkflowReader
{
    public async Task<CatalogValidationWorkflowFile> ReadAsync(StoredArtifact callerPin,CancellationToken cancellationToken=default)
    {
        var bytes=await requests.ReadAsync(callerPin,cancellationToken);
        CatalogValidationWorkflowFile? file;
        try
        {
            var options=new JsonSerializerOptions();options.Converters.Add(new JsonStringEnumConverter<CatalogValidationMode>(allowIntegerValues:false));
            file=JsonSerializer.Deserialize<CatalogValidationWorkflowFile>(canonical.Serialize(bytes),options);
        }
        catch(Exception error)when(error is JsonException or CanonicalJsonException or NotSupportedException)
        {throw new CatalogBuildException(CatalogValidationWorkflowErrors.Request,CatalogValidationWorkflowErrors.Request+": malformed or ambiguous validation request",error);}
        if(file?.Mode is null || !Enum.IsDefined(file.Mode.Value) || string.IsNullOrWhiteSpace(file.ReportPath))
            throw new CatalogBuildException(CatalogValidationWorkflowErrors.Request,CatalogValidationWorkflowErrors.Request+": explicit mode and report path required");
        var valid=file.Mode==CatalogValidationMode.Release
            ? !string.IsNullOrWhiteSpace(file.OutputRoot) && !string.IsNullOrWhiteSpace(file.SourceManifest) && file.DatabasePath is null
            : !string.IsNullOrWhiteSpace(file.DatabasePath) && file.OutputRoot is null && file.SourceManifest is null;
        if(!valid)throw new CatalogBuildException(CatalogValidationWorkflowErrors.Request,CatalogValidationWorkflowErrors.Request+": missing or mixed mode-specific paths");
        return file;
    }
}
