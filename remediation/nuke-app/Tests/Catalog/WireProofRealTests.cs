using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeWireProvider:IWireCaseProvider
{
    private readonly CatalogDatabase database;
    private readonly CanonicalJson canonical=new();
    private readonly WireProofStore store;
    private SqliteTransaction? fixtureTransaction;
    public NativeWireProvider()
    {
        database=CatalogDatabase.CreateInMemory(NativeSchemaProvider.Root(),CatalogSchemaProfile.Bridge);
        foreach(var (sql,parameters) in new (string,(string Name,object? Value)[])[]
        {
            ("INSERT INTO binding_definitions VALUES($binding,'fixture-binding','1.1.0')",[("$binding",WireProofCases.Binding)]),
            ("INSERT INTO binding_mappings VALUES($binding,$schema,1,'unknown','fixture','binder','a.I',0,'{}','{}')",[("$binding",WireProofCases.Binding),("$schema",WireProofCases.Schema)]),
            ("INSERT INTO binding_wire_contracts VALUES($contract,$binding,$schema,'a.I',NULL,NULL,0,0,0,'{}','{}','{}','unknown')",
                [("$contract",WireProofCases.Contract),("$binding",WireProofCases.Binding),("$schema",WireProofCases.Schema)])
        })
        { using var command=database.Command(sql,null,parameters);command.ExecuteNonQuery(); }
        store=new(database,canonical);
    }
    private WireReply Reply(string outcome,WireProofSnapshot? row=null,bool? unchanged=null)
    {
        using var count=database.Command("SELECT count(*) FROM wire_proof_revisions",fixtureTransaction);
        var rows=checked((int)(long)count.ExecuteScalar()!);
        using var basis=database.Command("SELECT proof_state FROM binding_wire_contracts WHERE contract_id=$contract",fixtureTransaction,("$contract",WireProofCases.Contract));
        var baseState=(string)basis.ExecuteScalar()!;
        using var check=database.Command("PRAGMA foreign_key_check",fixtureTransaction);using var reader=check.ExecuteReader();
        var clean=!reader.Read();
        return new(outcome,row?.Revision ?? 0,row?.State,row is null ? null : Encoding.UTF8.GetString(canonical.Serialize(row.Document)),
            row?.RecordHash,unchanged,rows,baseState,clean);
    }
    public WireReply Execute(WireCall call)
    {
        try
        {
            if(call.Operation=="begin-fixture-transaction")
            { fixtureTransaction=database.Connection.BeginTransaction(deferred:false);return new("ok"); }
            if(call.Operation=="rollback-fixture-transaction")
            { fixtureTransaction!.Rollback();fixtureTransaction.Dispose();fixtureTransaction=null;return new("ok"); }
            if(call.Operation=="current") return Reply("ok",store.Current(call.ContractId));
            if(call.Operation=="recheck") return Reply("ok",store.Current(call.ContractId),store.SelectedProofUnchanged(call.ContractId,call.PinnedHash!));
            if(call.Operation!="append") throw new ArgumentException("Unknown fixture wire operation",nameof(call));
            JsonElement Element(JsonObject? value)=>JsonSerializer.SerializeToElement(value ?? new JsonObject());
            return Reply("ok",store.Append(call.ContractId,call.ExpectedRevision,call.State,Element(call.ArgumentCodec),Element(call.ResponseCodec),Element(call.Source)));
        }
        catch(SqliteException error) when(error.SqliteErrorCode==19) { return Reply("constraint"); }
        catch(ArgumentException error) when(error.Message=="Unknown immutable wire identity") { return Reply("unknown-identity"); }
        catch(ArgumentException error) when(error.Message=="Invalid wire proof expected revision") { return Reply("invalid-expected-revision"); }
        catch(InvalidOperationException error) when(error.Message=="Wire proof revision changed") { return Reply("revision-changed"); }
        catch(InvalidOperationException error) when(error.Message=="Wire proof reference owns its transaction") { return Reply("transaction-owned"); }
    }
    public void Dispose()
    { fixtureTransaction?.Dispose();database.Dispose(); }
}
[Trait("Gate","Real")]
public sealed class WireProofRealTests
{
    [Theory,MemberData(nameof(WireProofCases.All),MemberType=typeof(WireProofCases))]
    public void NativeWireEvidenceUsesSameCasesAndAssertions(WireCase test)
    { using var provider=new NativeWireProvider();WireProofCases.AssertCase(test,provider); }
}
