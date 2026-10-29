using System.Text.Json;
using System.Text.Json.Serialization;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Catalog.Migration;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CatalogMigrationWorkflowFile(string? CatalogRoot,string? SourceDatabase,string? DestinationDatabase);
public sealed class CatalogMigrationWorkflowReader(IPinnedArtifactReader requests,ICanonicalJson canonical)
{
    public const string RequestError="CATALOG_MIGRATION_REQUEST";
    public async Task<CatalogMigrationWorkflowFile> ReadAsync(StoredArtifact callerPin,CancellationToken cancellationToken=default)
    {
        var bytes=await requests.ReadAsync(callerPin,cancellationToken);CatalogMigrationWorkflowFile? file;
        try{file=JsonSerializer.Deserialize<CatalogMigrationWorkflowFile>(canonical.Serialize(bytes));}
        catch(Exception error)when(error is JsonException or CanonicalJsonException or NotSupportedException)
        {throw new InvalidDataException(RequestError+": malformed or ambiguous migration request",error);}
        if(file is null||string.IsNullOrWhiteSpace(file.CatalogRoot)||string.IsNullOrWhiteSpace(file.SourceDatabase)||string.IsNullOrWhiteSpace(file.DestinationDatabase))
            throw new InvalidDataException(RequestError+": explicit catalog root, source and destination required");
        return file;
    }
}
