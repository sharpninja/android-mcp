using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Catalog;

public sealed class CatalogException(int status,string message):Exception(message)
{
    public int Status { get; }=status;
}
public sealed record ImportedEntry(string Signature,JsonElement Document);
public sealed record CatalogCursor(string RegistryId,long RegistryGeneration,long OverlayGeneration);
public sealed record CatalogHistory(string Operation,int Revision,string State,string? Reason);
public sealed record CatalogEntrySnapshot(string Key,string Signature,JsonElement Document,string State,int Revision,
    string? BaseHash,bool Imported,string ETag,IReadOnlyList<CatalogHistory> History);

/// <summary>Offline state-machine reference. No administration HTTP listener or Android dispatch.</summary>
public sealed partial class CatalogReference(ICanonicalJson canonical,Func<ReadOnlyMemory<byte>,string> sha256)
{
    private sealed class Entry(string signature,JsonElement document,string state,int revision,string? baseHash,bool imported)
    {
        public string Signature=signature;
        public JsonElement Document=document.Clone();
        public string State=state;
        public int Revision=revision;
        public string? BaseHash=baseHash;
        public bool Imported=imported;
    }
    private readonly object sync=new();
    private readonly Dictionary<string,Entry> entries=new(StringComparer.Ordinal);
    private readonly Dictionary<string,List<CatalogHistory>> history=new(StringComparer.Ordinal);
    public long RegistryGeneration { get; private set; }=1;
    public long OverlayGeneration { get; private set; }
    public string RegistryId { get; private set; }="fixture-initial";

    public string Identity(string signature)=>sha256(Encoding.UTF8.GetBytes(signature));
    private string Tag(string key,Entry row)=>$"\"cat1:{RegistryGeneration}:{row.BaseHash ?? "local"}:{key}:{row.Revision}:{row.State}\"";
    private void Record(string key,string operation,string? reason)
    {
        OverlayGeneration++;
        if(!history.TryGetValue(key,out var records)) history.Add(key,records=[]);
        var row=entries[key];records.Add(new(operation,row.Revision,row.State,reason));
    }
    public CatalogEntrySnapshot Get(string key)
    {
        lock(sync)
        {
            if(!entries.TryGetValue(key,out var row)) throw new CatalogException(404,"Catalog identity not found");
            return new(key,row.Signature,row.Document.Clone(),row.State,row.Revision,row.BaseHash,row.Imported,Tag(key,row),
                history.TryGetValue(key,out var records) ? records.ToArray() : []);
        }
    }
    public CatalogEntrySnapshot Create(string signature,JsonElement document,string? reason=null)
    {
        lock(sync)
        {
            var key=Identity(signature);
            if(entries.ContainsKey(key)) throw new CatalogException(409,"Canonical identity already exists");
            _=canonical.Serialize(document);
            entries.Add(key,new(signature,document,"active",1,null,false));Record(key,"create",reason);return Get(key);
        }
    }
    private Entry Precondition(string key,string? etag)
    {
        if(etag is null) throw new CatalogException(428,"If-Match required");
        if(!StrongTag().IsMatch(etag)) throw new CatalogException(400,"One exact strong catalog ETag required");
        if(!entries.TryGetValue(key,out var row)) throw new CatalogException(404,"Catalog identity not found");
        if(etag!=Tag(key,row)) throw new CatalogException(412,"Catalog revision or selected registry changed");
        return row;
    }
    public CatalogEntrySnapshot Put(string key,string? etag,JsonElement document,string? resolution=null,string? expectedBase=null,string? reason=null)
    {
        lock(sync)
        {
            var row=Precondition(key,etag);
            if(row.State=="conflict" && (resolution!=(row.BaseHash is null ? "retain-local" : "rebase") || expectedBase!=row.BaseHash))
                throw new CatalogException(409,"Explicit resolution against the current imported base required");
            _=canonical.Serialize(document);
            var operation=row.State=="conflict" ? "rebase" : row.State is "deleted" or "suppressed" ? "restore" : "update";
            row.Document=document.Clone();row.State="active";row.Revision++;Record(key,operation,reason);return Get(key);
        }
    }
    public CatalogEntrySnapshot Delete(string key,string? etag,string? reason=null)
    {
        lock(sync)
        {
            var row=Precondition(key,etag);
            if(row.State=="conflict") throw new CatalogException(409,"Resolve conflict before deletion");
            if(row.State is not ("deleted" or "suppressed"))
            {
                row.State=row.Imported ? "suppressed" : "deleted";row.Revision++;
                Record(key,row.Imported ? "suppress" : "delete",reason);
            }
            return Get(key);
        }
    }
    public void PublishFixture(IReadOnlyDictionary<string,ImportedEntry> records)
    {
        lock(sync)
        {
            // Validate every identity and canonical document before changing selection.
            var hashes=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var (key,value) in records)
            {
                if(Identity(value.Signature)!=key) throw new CatalogException(422,"Imported canonical identity mismatch");
                try { hashes.Add(key,canonical.Hash(value.Document)); }
                catch(CanonicalJsonException error) { throw new CatalogException(422,error.Message); }
            }
            using var bytes=new MemoryStream();
            using(var writer=new Utf8JsonWriter(bytes))
            {
                writer.WriteStartObject();
                foreach(var (key,value) in records)
                {
                    writer.WritePropertyName(key);writer.WriteStartArray();writer.WriteStringValue(value.Signature);
                    value.Document.WriteTo(writer);writer.WriteEndArray();
                }
                writer.WriteEndObject();
            }
            var nextRegistryId=canonical.Hash(bytes.ToArray());
            foreach(var (key,row) in entries.ToArray())
            {
                if(!row.Imported || records.ContainsKey(key)) continue;
                if(row.Revision==0) { entries.Remove(key);continue; }
                row.BaseHash=null;row.Imported=false;row.State="conflict";row.Revision++;Record(key,"conflict",null);
            }
            foreach(var (key,value) in records)
            {
                var hash=hashes[key];
                if(!entries.TryGetValue(key,out var row)) entries.Add(key,new(value.Signature,value.Document,"active",0,hash,true));
                else
                {
                    if(!row.Imported || row.BaseHash!=hash && row.Revision>0)
                    { row.State="conflict";row.Revision++;Record(key,"conflict",null); }
                    else if(row.Revision==0) row.Document=value.Document.Clone();
                    row.BaseHash=hash;row.Imported=true;
                }
            }
            RegistryGeneration++;RegistryId=nextRegistryId;
        }
    }
    public CatalogCursor Cursor() { lock(sync) return new(RegistryId,RegistryGeneration,OverlayGeneration); }
    public void ContinuePage(CatalogCursor cursor)
    {
        lock(sync) if(cursor!=Cursor()) throw new CatalogException(410,"Selected registry or overlay cursor expired");
    }

    [GeneratedRegex("^\"cat1:[0-9]+:(?:local|[a-f0-9]{64}):[a-f0-9]{64}:[0-9]+:(?:active|suppressed|deleted|conflict)\"$",RegexOptions.CultureInvariant)]
    private static partial Regex StrongTag();
}
