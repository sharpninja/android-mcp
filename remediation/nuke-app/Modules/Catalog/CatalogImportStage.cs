using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Catalog;

public sealed record CatalogImportObservation(string Event,string StagePath,string? SourceId=null,string? EntryId=null,int Count=0);

/// <summary>Retained private extraction evidence. Its tables are never part of the published schema.</summary>
internal sealed class CatalogImportStage:IDisposable
{
    private readonly CatalogDatabase database;
    private readonly ICanonicalJson canonical;
    internal string Path {get;}
    internal CatalogImportStage(string path,ICanonicalJson canonical)
    {
        Path=path;this.canonical=canonical;
        database=CatalogDatabase.CreatePrivateFile(path,["CREATE TABLE source_records(source_id TEXT NOT NULL,ordinal INTEGER NOT NULL,document_json TEXT NOT NULL CHECK(json_valid(document_json)),PRIMARY KEY(source_id,ordinal)); CREATE TABLE source_concerns(source_id TEXT NOT NULL PRIMARY KEY,issues_json TEXT NOT NULL,structural_status TEXT NOT NULL,metadata_json TEXT,record_count INTEGER NOT NULL);"]);
    }
    private string Text<T>(T value)=>Encoding.UTF8.GetString(canonical.Serialize(JsonSerializer.SerializeToElement(value)));
    internal void Capture(string sourceId,ExtractionResult facts)
    {
        using var transaction=database.Connection.BeginTransaction();var ordinal=0;
        foreach(var record in facts.Records)
        {
            using var insert=database.Command("INSERT INTO source_records VALUES($source,$ordinal,$document)",transaction,("$source",sourceId),("$ordinal",ordinal++),("$document",Text(record)));insert.ExecuteNonQuery();
        }
        using(var concern=database.Command("INSERT INTO source_concerns VALUES($source,$issues,$status,$metadata,$count)",transaction,("$source",sourceId),("$issues",Text(facts.Issues)),("$status",facts.StructuralStatus.ToString()),("$metadata",facts.Metadata is null ? null : Text(facts.Metadata)),("$count",ordinal))) concern.ExecuteNonQuery();
        transaction.Commit();
    }
    internal IEnumerable<JsonObject> Records(string sourceId)
    {
        using var command=database.Command("SELECT document_json FROM source_records WHERE source_id=$source ORDER BY ordinal",null,("$source",sourceId));using var reader=command.ExecuteReader();
        while(reader.Read()) yield return JsonNode.Parse(reader.GetString(0))!.AsObject();
    }
    internal ExtractionResult Facts(string sourceId)
    {
        using var command=database.Command("SELECT issues_json,structural_status,metadata_json FROM source_concerns WHERE source_id=$source",null,("$source",sourceId));using var reader=command.ExecuteReader();
        if(!reader.Read()) throw new InvalidDataException("Missing captured source concern");
        return new([],JsonSerializer.Deserialize<SourceIssue[]>(reader.GetString(0))!,Enum.Parse<StructuralParseStatus>(reader.GetString(1)),reader.IsDBNull(2) ? null : JsonNode.Parse(reader.GetString(2))!.AsObject());
    }
    internal int Count(string sourceId)
    {using var command=database.Command("SELECT record_count FROM source_concerns WHERE source_id=$source",null,("$source",sourceId));return checked((int)(long)command.ExecuteScalar()!);}
    public void Dispose()=>database.Dispose();
}
