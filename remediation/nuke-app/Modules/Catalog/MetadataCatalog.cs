using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;
using Json.Schema;
using Microsoft.Data.Sqlite;

namespace AndroidMcp.BuildTool.Catalog;

public sealed record MetadataSnapshot(string Kind,string Key,int Revision,string Origin,string State,
    string? BaseHash,string ContentHash,string Document,string Reason,string ETag);
public sealed record MetadataRevision(string Kind,string Key,int Revision,string Origin,string State,
    string? BaseHash,string ContentHash,string Document,string Reason);
public sealed record MetadataCursor(string Reader,long Generation);
public sealed record MetadataCatalogIdentity(string Kind,string ContractHash);

/// <summary>Protected offline metadata persistence. Capabilities come from the caller, never JSON.</summary>
public sealed partial class MetadataCatalog
{
    private readonly CatalogDatabase database;
    private readonly ICanonicalJson canonical;
    private readonly IContentHash hashes;
    private readonly CatalogSourceMetadata sourceMetadata;
    private readonly IReadOnlyDictionary<string,MetadataCatalogIdentity> catalogIndex;
    private readonly object sync=new();
    public MetadataCatalog(CatalogDatabase database,ICanonicalJson canonical,IContentHash hashes,string schemaPath,
        IReadOnlyDictionary<string,MetadataCatalogIdentity>? catalogIndex=null)
    {
        this.database=database;this.canonical=canonical;this.hashes=hashes;
        sourceMetadata=new CatalogSourceMetadata(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(schemaPath))!,"SourceContractMetadata.schema.json"),canonical);
        this.catalogIndex=catalogIndex ?? new Dictionary<string,MetadataCatalogIdentity>(StringComparer.Ordinal);
    }
    private SqliteCommand Command(string sql,SqliteTransaction? transaction=null,params (string Name,object? Value)[] parameters)=>
        database.Command(sql,transaction,parameters);
    public long Generation
    {
        get { lock(sync) { using var command=Command("SELECT generation FROM metadata_state WHERE singleton=1");return (long)command.ExecuteScalar()!; } }
    }
    private MetadataSnapshot? Row(string kind,string key,SqliteTransaction? transaction=null)
    {
        using var command=Command("SELECT r.revision,r.origin,r.state,r.base_hash,r.content_hash,r.document_json,r.reason FROM metadata_heads h JOIN metadata_revisions r USING(kind,resource_id,revision) WHERE h.kind=$kind AND h.resource_id=$key",
            transaction,("$kind",kind),("$key",key));
        using var reader=command.ExecuteReader();if(!reader.Read()) return null;
        var revision=reader.GetInt32(0);var origin=reader.GetString(1);var state=reader.GetString(2);
        var baseHash=reader.IsDBNull(3) ? null : reader.GetString(3);var contentHash=reader.GetString(4);
        var document=reader.GetString(5);var reason=reader.GetString(6);
        reader.Close();
        using var generationCommand=Command("SELECT generation FROM metadata_state WHERE singleton=1",transaction);
        var generation=(long)generationCommand.ExecuteScalar()!;
        var keyHash=hashes.Sha256(Encoding.UTF8.GetBytes(key));
        var etag=$"\"meta1:{generation}:{kind}:{keyHash}:{baseHash ?? "local"}:{revision}:{state}:{contentHash}\"";
        return new(kind,key,revision,origin,state,baseHash,contentHash,document,reason,etag);
    }
    public MetadataSnapshot Get(string kind,string key)
    { lock(sync) return Row(kind,key) ?? throw new CatalogException(404,"Metadata resource not found"); }
    private static void RequireOwner(bool owner)
    { if(!owner) throw new CatalogException(403,"Trusted owner capability required; request JSON cannot assert it"); }
    private MetadataSnapshot Precondition(string kind,string key,string? etag,SqliteTransaction transaction)
    {
        if(etag is null) throw new CatalogException(428,"If-Match required");
        if(!StrongMetadataTag().IsMatch(etag)) throw new CatalogException(400,"One exact strong metadata ETag required");
        var old=Row(kind,key,transaction) ?? throw new CatalogException(404,"Metadata resource not found");
        if(etag!=old.ETag) throw new CatalogException(412,"Metadata revision or generation changed");
        return old;
    }
    private string ValidateDocument(string kind,string key,JsonElement document,SqliteTransaction transaction)
    {
        try
        {
            var value=JsonNode.Parse(document.GetRawText()) as JsonObject ?? throw new FormatException("Metadata document must be an object");
            if(kind=="functional-group")
            {
                if(value["group_id"]?.GetValue<string>()!=key)throw new FormatException("Group identity mismatch");
                var groups=new Dictionary<string,CatalogMetadataGroupPin>(StringComparer.Ordinal);
                using(var command=Command("SELECT r.resource_id,r.revision,r.content_hash,r.document_json FROM metadata_heads h JOIN metadata_revisions r USING(kind,resource_id,revision) WHERE h.kind='functional-group' AND r.state='active'",transaction))
                using(var rows=command.ExecuteReader())while(rows.Read())groups.Add(rows.GetString(0),new(rows.GetInt32(1),rows.GetString(2),JsonNode.Parse(rows.GetString(3))!.AsObject()));
                sourceMetadata.ValidateGroup(value,groups);
            }
            else if(kind=="callback-descriptor")
            {
                if(value["callback_id"]?.GetValue<string>()!=key)throw new FormatException("Callback identity mismatch");
                sourceMetadata.ValidateDescriptor(value);
            }
            else throw new FormatException("Unknown metadata kind");
            void Pin(string identity,string? contractHash=null,bool typeRequired=false)
            {
                if(!catalogIndex.TryGetValue(identity,out var target) || contractHash is not null && target.ContractHash!=contractHash ||
                    typeRequired && target.Kind is not ("class" or "interface" or "aidl-interface" or "parcelable" or "enum" or "struct" or "union" or "typedef" or "annotation"))
                    throw new CatalogException(422,"Unknown or incompatible trusted catalog reference");
            }
            if(kind=="functional-group")
            {
                foreach(var member in value["members"]!.AsArray())Pin(member!["binding_id"]!.GetValue<string>(),member["contract_hash"]!.GetValue<string>());
                foreach(var identity in value["required_type_ids"]!.AsArray())Pin(identity!.GetValue<string>(),typeRequired:true);
            }
            else
            {
                Pin(value["type_id"]!.GetValue<string>(),value["type_contract_hash"]!.GetValue<string>(),true);
                foreach(var method in value["methods"]!.AsArray())
                {
                    Pin(method!["method_id"]!.GetValue<string>(),method["contract_hash"]!.GetValue<string>());
                    foreach(var identity in method["required_type_ids"]!.AsArray())Pin(identity!.GetValue<string>(),typeRequired:true);
                }
                foreach(var identity in value["registration_binding_ids"]!.AsArray())Pin(identity!.GetValue<string>());
                if(value["cleanup"]!["binding_id"] is JsonValue cleanup)Pin(cleanup.GetValue<string>(),value["cleanup"]!["contract_hash"]?.GetValue<string>());
            }
            return Encoding.UTF8.GetString(canonical.Serialize(document));
        }
        catch(Exception error)when(error is FormatException or JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException)
        {throw new CatalogException(422,error.Message);}
    }
    public JsonObject Representation(string kind,string key)
    {
        var row=Get(kind,key);return new(){["kind"]=row.Kind,["resource_id"]=row.Key,["revision"]=row.Revision,["origin"]=row.Origin,["state"]=row.State,
            ["base_hash"]=row.BaseHash,["content_hash"]=row.ContentHash,["document"]=JsonNode.Parse(row.Document),["reason"]=row.Reason,["etag"]=row.ETag};
    }
    private void Append(string kind,string key,string body,string origin,string state,string? baseHash,string reason,SqliteTransaction transaction)
    {
        // Preserve the original reason.strip() admission, including the four
        // C0 separators not classified as whitespace by the ordinary BCL test.
        // Accepted reason bytes remain untrimmed in the sole append path.
        if(string.IsNullOrEmpty(reason)||reason.All(character=>char.IsWhiteSpace(character)||character is >= '\u001c' and <= '\u001f'))
            throw new CatalogException(422,"Nonempty reason required");
        var old=Row(kind,key,transaction);var revision=(old?.Revision ?? 0)+1;
        using(var command=Command("INSERT INTO metadata_revisions VALUES($kind,$key,$revision,$origin,$state,$base,$hash,$body,$reason)",transaction,
            ("$kind",kind),("$key",key),("$revision",revision),("$origin",origin),("$state",state),("$base",baseHash),
            ("$hash",hashes.Sha256(Encoding.UTF8.GetBytes(body))),("$body",body),("$reason",reason))) command.ExecuteNonQuery();
        using var head=old is null ? Command("INSERT INTO metadata_heads VALUES($kind,$key,$revision)",transaction,("$kind",kind),("$key",key),("$revision",revision)) :
            Command("UPDATE metadata_heads SET revision=$revision WHERE kind=$kind AND resource_id=$key AND revision=$old",transaction,
                ("$revision",revision),("$kind",kind),("$key",key),("$old",old.Revision));
        if(head.ExecuteNonQuery()!=1) throw new CatalogException(412,"Metadata head CAS failed");
    }
    private MetadataSnapshot Mutate(string kind,string key,Action<SqliteTransaction> action)
    {
        lock(sync)
        {
            using var transaction=database.Connection.BeginTransaction(deferred:false);
            action(transaction);transaction.Commit();return Get(kind,key);
        }
    }
    public MetadataSnapshot Create(string kind,string key,JsonElement document,string reason,bool owner=false,string? ifNoneMatch=null)
    {
        RequireOwner(owner);
        if(ifNoneMatch is null) throw new CatalogException(428,"If-None-Match:* required");
        if(ifNoneMatch!="*") throw new CatalogException(400,"Creation requires If-None-Match:*");
        return Mutate(kind,key,transaction=>
        {
            if(Row(kind,key,transaction) is not null) throw new CatalogException(412,"Metadata identity already exists");
            var body=ValidateDocument(kind,key,document,transaction);Append(kind,key,body,"local","active",null,reason,transaction);
        });
    }
    public MetadataSnapshot Put(string kind,string key,JsonElement document,string? etag,string reason,bool owner=false,string? resolution=null,string? expectedBase=null)
    {
        RequireOwner(owner);
        return Mutate(kind,key,transaction=>
        {
            var old=Precondition(kind,key,etag,transaction);var body=ValidateDocument(kind,key,document,transaction);
            if(old.State=="conflict" && (resolution!=(old.BaseHash is null ? "retain-local" : "rebase") || expectedBase!=old.BaseHash))
                throw new CatalogException(409,"Explicit conflict resolution against selected base required");
            Append(kind,key,body,old.BaseHash is null ? "local" : "overlay","active",old.BaseHash,reason,transaction);
        });
    }
    public MetadataSnapshot Delete(string kind,string key,string? etag,string reason,bool owner=false)
    {
        RequireOwner(owner);
        return Mutate(kind,key,transaction=>
        {
            var old=Precondition(kind,key,etag,transaction);
            if(old.State=="conflict") throw new CatalogException(409,"Resolve conflict before deletion");
            if(old.State is "deleted" or "suppressed") return;
            Append(kind,key,old.Document,old.BaseHash is null ? "local" : "overlay",old.BaseHash is null ? "deleted" : "suppressed",old.BaseHash,reason,transaction);
        });
    }
    public MetadataSnapshot Import(string kind,string key,JsonElement document,string reason="trusted immutable fixture import",bool trustedImport=false)
    {
        if(!trustedImport) throw new CatalogException(403,"Only a trusted importer may add immutable metadata");
        return Mutate(kind,key,transaction=>
        {
            var body=ValidateDocument(kind,key,document,transaction);var hash=hashes.Sha256(Encoding.UTF8.GetBytes(body));var old=Row(kind,key,transaction);
            using(var command=Command("INSERT OR IGNORE INTO metadata_bases VALUES($kind,$key,$hash,$body)",transaction,
                ("$kind",kind),("$key",key),("$hash",hash),("$body",body))) command.ExecuteNonQuery();
            if(old?.BaseHash==hash) return;
            var clean=old is null || old.Origin=="imported" && old.State=="active" && old.ContentHash==old.BaseHash;
            Append(kind,key,clean ? body : old!.Document,clean ? "imported" : "overlay",clean ? "active" : "conflict",hash,reason,transaction);
        });
    }
    public IReadOnlyList<MetadataRevision> History(string kind,string key)
    {
        lock(sync)
        {
            using var command=Command("SELECT kind,resource_id,revision,origin,state,base_hash,content_hash,document_json,reason FROM metadata_revisions WHERE kind=$kind AND resource_id=$key ORDER BY revision",null,("$kind",kind),("$key",key));
            using var rows=command.ExecuteReader();var result=new List<MetadataRevision>();
            while(rows.Read())result.Add(new(rows.GetString(0),rows.GetString(1),rows.GetInt32(2),rows.GetString(3),rows.GetString(4),rows.IsDBNull(5)?null:rows.GetString(5),rows.GetString(6),rows.GetString(7),rows.GetString(8)));
            return result.ToArray();
        }
    }
    public MetadataCursor Cursor(string reader) { lock(sync) return new(reader,Generation); }
    public void ContinuePage(MetadataCursor cursor,string reader)
    { lock(sync) if(cursor!=new MetadataCursor(reader,Generation)) throw new CatalogException(410,"Reader or generation scoped cursor expired"); }

    [GeneratedRegex("^\"meta1:[0-9]+:(?:callback-descriptor|functional-group):[a-f0-9]{64}:(?:local|[a-f0-9]{64}):[1-9][0-9]*:(?:active|suppressed|deleted|conflict):[a-f0-9]{64}\"\\z",RegexOptions.CultureInvariant)]
    private static partial Regex StrongMetadataTag();
}
