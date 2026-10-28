using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using Microsoft.Data.Sqlite;

namespace AndroidMcp.BuildTool.Catalog;

public sealed record WireProofSnapshot(JsonElement Document,string RecordHash)
{
    public long Revision=>Document.GetProperty("revision").GetInt64();
    public string State=>Document.GetProperty("proof_state").GetString()!;
}

/// <summary>Offline append-only evidence selection. Evidence never supplies invocation authority.</summary>
public sealed class WireProofStore(CatalogDatabase database,ICanonicalJson canonical)
{
    private readonly object sync=new();
    private static JsonElement Record(Action<Utf8JsonWriter> write)
    {
        using var bytes=new MemoryStream();using(var writer=new Utf8JsonWriter(bytes)) write(writer);
        using var document=JsonDocument.Parse(bytes.ToArray());return document.RootElement.Clone();
    }
    private WireProofSnapshot Current(string contractId,SqliteTransaction? transaction)
    {
        using var command=database.Command("SELECT contract_id,binding_id,schema_hash,descriptor,interface_version,interface_hash,transaction_code,flags,oneway,proof_revision,proof_state,argument_codec_json,response_codec_json,proof_source_json,record_sha256 FROM current_wire_proofs WHERE contract_id=$contract",
            transaction,("$contract",contractId));
        using var reader=command.ExecuteReader();
        if(!reader.Read()) throw new ArgumentException("Unknown immutable wire identity");
        var body=Record(writer=>
        {
            writer.WriteStartObject();
            foreach(var (index,key) in new[]{(0,"contract_id"),(1,"binding_id"),(2,"schema_hash"),(3,"descriptor")})
                writer.WriteString(key,reader.GetString(index));
            if(reader.IsDBNull(4)) writer.WriteNull("interface_version");else writer.WriteNumber("interface_version",reader.GetInt64(4));
            if(reader.IsDBNull(5)) writer.WriteNull("interface_hash");else writer.WriteString("interface_hash",reader.GetString(5));
            foreach(var (index,key) in new[]{(6,"transaction_code"),(7,"flags"),(8,"oneway"),(9,"revision")})
                writer.WriteNumber(key,reader.GetInt64(index));
            writer.WriteString("proof_state",reader.GetString(10));
            foreach(var (index,key) in new[]{(11,"argument_codec"),(12,"response_codec"),(13,"proof_source")})
            {
                using var document=JsonDocument.Parse(reader.GetString(index));writer.WritePropertyName(key);document.RootElement.WriteTo(writer);
            }
            writer.WriteEndObject();
        });
        var hash=canonical.Hash(body);
        if(!reader.IsDBNull(14) && reader.GetString(14)!=hash) throw new InvalidDataException("Wire proof record hash mismatch");
        return new(body,hash);
    }
    public WireProofSnapshot Current(string contractId) { lock(sync) return Current(contractId,null); }
    public WireProofSnapshot Append(string contractId,object? expectedRevision,string proofState,JsonElement argumentCodec,
        JsonElement responseCodec,JsonElement proofSource)
    {
        lock(sync)
        {
            SqliteTransaction transaction;
            try { transaction=database.Connection.BeginTransaction(deferred:false); }
            catch(InvalidOperationException error) { throw new InvalidOperationException("Wire proof reference owns its transaction",error); }
            using(transaction)
            {
                // CLR integer widths represent the same counter domain; floating,
                // boolean, string and null values are never coerced into revisions.
                if(expectedRevision is not (int or long)) throw new ArgumentException("Invalid wire proof expected revision");
                var expected=Convert.ToInt64(expectedRevision,System.Globalization.CultureInfo.InvariantCulture);
                if(expected<0 || expected>=DeviceResolver.CounterLimit) throw new ArgumentException("Invalid wire proof expected revision");
                var old=Current(contractId,transaction);
                if(old.Revision!=expected) throw new InvalidOperationException("Wire proof revision changed");
                var body=Record(writer=>
                {
                    writer.WriteStartObject();
                    foreach(var property in old.Document.EnumerateObject())
                    {
                        writer.WritePropertyName(property.Name);
                        switch(property.Name)
                        {
                            case "revision":writer.WriteNumberValue(expected+1);break;
                            case "proof_state":writer.WriteStringValue(proofState);break;
                            case "argument_codec":argumentCodec.WriteTo(writer);break;
                            case "response_codec":responseCodec.WriteTo(writer);break;
                            case "proof_source":proofSource.WriteTo(writer);break;
                            default:property.Value.WriteTo(writer);break;
                        }
                    }
                    writer.WriteEndObject();
                });
                string Text(JsonElement value)=>Encoding.UTF8.GetString(canonical.Serialize(value));
                using(var revision=database.Command("INSERT INTO wire_proof_revisions VALUES($contract,$revision,$state,$arguments,$response,$source,$hash)",transaction,
                    ("$contract",contractId),("$revision",expected+1),("$state",proofState),("$arguments",Text(argumentCodec)),
                    ("$response",Text(responseCodec)),("$source",Text(proofSource)),("$hash",canonical.Hash(body)))) revision.ExecuteNonQuery();
                using(var head=expected==0 ? database.Command("INSERT INTO wire_proof_active VALUES($contract,$revision)",transaction,
                    ("$contract",contractId),("$revision",expected+1)) : database.Command("UPDATE wire_proof_active SET revision=$revision WHERE contract_id=$contract AND revision=$old",transaction,
                    ("$revision",expected+1),("$contract",contractId),("$old",expected)))
                    if(head.ExecuteNonQuery()!=1) throw new InvalidOperationException("Wire proof revision CAS failed");
                var selected=Current(contractId,transaction);transaction.Commit();return selected;
            }
        }
    }
    public bool SelectedProofUnchanged(string contractId,string recordHash)
    { lock(sync) { var selected=Current(contractId,null);return selected.State=="verified" && selected.RecordHash==recordHash; } }
}
