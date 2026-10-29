using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public enum SchemaFixtureProfile { Entry, Bridge, Metadata, Combined }
public sealed record SchemaCommand(string Sql,object?[] Parameters,string?[][] Rows,bool Rejected=false,int? ErrorCode=null);
public sealed record SchemaReply(string?[][] Rows,bool Rejected=false,int? ErrorCode=null);
public sealed record SqliteSchemaCase(string Id,SchemaFixtureProfile Profile,SchemaCommand[] Commands);
public interface ISchemaCaseProvider:IDisposable { SchemaReply Execute(SchemaCommand command); }

public static class SqliteSchemaCases
{
    public const string A="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    public const string B="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string Group="{\"schema_version\":\"1.0.0\",\"group_id\":\"fg:location\"}";
    private static SchemaCommand Sql(string text,params object?[] parameters)=>new(text,parameters,[]);
    private static SchemaCommand Reject(string text,params object?[] parameters)=>new(text,parameters,[],true,19);
    private static SchemaCommand Query(string text,params string?[][] rows)=>new(text,[],rows);
    private static SchemaCommand Registry(string state="staged")=>Sql("INSERT INTO registries VALUES('r',1,'1.2.0','fixture',37,?,'now',?,'{}')",A,state);
    private static SchemaCommand Binding()=>Sql("INSERT INTO bindings(binding_id,signature,package,kind,name,raw_signature) VALUES(?,'java:a.A','a','class','A','fixture')",A);
    private static SchemaCommand Client()=>Sql("INSERT INTO clients VALUES('fixture','fixture','active',0,1,0,0,NULL)");
    private static SchemaCommand MetadataRevision(int revision=1)=>Sql("INSERT INTO metadata_revisions VALUES('functional-group','fg:location',?,'local','active',NULL,?,?,'fixture reason')",revision,A,Group);
    public static IEnumerable<object[]> All=>new[]
    {
        new SqliteSchemaCase("SQL-01-entry-integrity",SchemaFixtureProfile.Entry,
            [Query("PRAGMA user_version",["3"]),Query("PRAGMA foreign_keys",["1"]),Query("PRAGMA integrity_check",["ok"]),Query("PRAGMA foreign_key_check")]),
        new SqliteSchemaCase("SQL-02-all-natural-primary-keys-not-null",SchemaFixtureProfile.Combined,
            [Query("SELECT s.name||'.'||p.name FROM sqlite_master s JOIN pragma_table_info(s.name) p WHERE s.type='table' AND p.pk>0 AND p.type<>'INTEGER' AND p.\"notnull\"=0 ORDER BY 1")]),
        new SqliteSchemaCase("SQL-03-registry-closed-state",SchemaFixtureProfile.Entry,
            [Reject("INSERT INTO registries VALUES('r',1,'1.2.0','fixture',37,?,'now','invented','{}')",A),Query("SELECT count(*) FROM registries",["0"])]),
        new SqliteSchemaCase("SQL-04-registry-transitions",SchemaFixtureProfile.Entry,
            [Registry(),Reject("UPDATE registries SET state='published'"),Query("SELECT state FROM registries",["staged"]),Sql("UPDATE registries SET state='validated'"),Sql("UPDATE registries SET state='published'"),Sql("UPDATE registries SET state='retired'"),Reject("UPDATE registries SET state='published'"),Query("SELECT state FROM registries",["retired"])]),
        new SqliteSchemaCase("SQL-05-rejected-terminal",SchemaFixtureProfile.Entry,
            [Registry(),Sql("UPDATE registries SET state='rejected'"),Reject("UPDATE registries SET state='staged'"),Query("SELECT state FROM registries",["rejected"])]),
        new SqliteSchemaCase("SQL-06-sources-immutable",SchemaFixtureProfile.Entry,
            [Sql("INSERT INTO sources VALUES('source','fixture','url','revision','now',?,37,'fixture','evidence','fixture')",A),Reject("UPDATE sources SET path='changed'"),Reject("DELETE FROM sources"),Query("SELECT path FROM sources",["fixture"])]),
        new SqliteSchemaCase("SQL-07-bindings-immutable",SchemaFixtureProfile.Entry,
            [Binding(),Reject("UPDATE bindings SET name='changed'"),Reject("DELETE FROM bindings"),Query("SELECT name FROM bindings",["A"])]),
        new SqliteSchemaCase("SQL-08-registry-entry-sealed",SchemaFixtureProfile.Entry,
            [Binding(),Registry(),Sql("INSERT INTO registry_entries VALUES('r',?,'{\"version\":1}',?)",A,A),Sql("UPDATE registries SET state='validated'"),Sql("UPDATE registries SET state='published'"),Sql("INSERT INTO active_registry(singleton,registry_id) VALUES(1,'r')"),Reject("UPDATE registry_entries SET document_json='{}'"),Query("SELECT document_json FROM effective_entries",["{\"version\":1}"])]),
        new SqliteSchemaCase("SQL-09-registry-invalid-json",SchemaFixtureProfile.Entry,
            [Binding(),Registry(),Reject("INSERT INTO registry_entries VALUES('r',?,'not-json',?)",A,A),Query("SELECT count(*) FROM registry_entries",["0"])]),
        new SqliteSchemaCase("SQL-10-permission-logic-closed",SchemaFixtureProfile.Entry,
            [Binding(),Sql("INSERT INTO sources VALUES('source','fixture','url','revision','now',?,37,'fixture','evidence','fixture')",A),Reject("INSERT INTO permission_relations(binding_id,source_id,line,logic,expression_json) VALUES(?,'source',1,'value','{}')",A),Query("SELECT count(*) FROM permission_relations",["0"])]),
        new SqliteSchemaCase("SQL-11-bridge-integrity",SchemaFixtureProfile.Bridge,
            [Query("PRAGMA foreign_keys",["1"]),Query("PRAGMA integrity_check",["ok"]),Query("PRAGMA foreign_key_check")]),
        new SqliteSchemaCase("SQL-12-credential-fk",SchemaFixtureProfile.Bridge,
            [Client(),Reject("INSERT INTO credentials VALUES('credential','missing',zeroblob(32),'fixture','active',0,NULL,NULL,0)"),Query("SELECT count(*) FROM credentials",["0"])]),
        new SqliteSchemaCase("SQL-13-logging-mode-closed",SchemaFixtureProfile.Bridge,
            [Client(),Reject("INSERT INTO client_logging_config(client_id,mode,paused,logging_revision,retention_seconds,byte_quota,record_byte_cap,redaction_profile_version,updated_at) VALUES('fixture','full-raw',0,1,1,1,1,1,0)"),Query("SELECT count(*) FROM client_logging_config",["0"])]),
        new SqliteSchemaCase("SQL-14-backend-kind-closed",SchemaFixtureProfile.Bridge,
            [Reject("INSERT INTO connection_profiles(profile_id,backend_kind,display_name,row_version,android_user_id,endpoint_metadata_json,verification_strength) VALUES('fixture','invented','fixture',1,0,'{}','fixture')"),Query("SELECT count(*) FROM connection_profiles",["0"])]),
        new SqliteSchemaCase("SQL-15-raw-authority-columns-not-json",SchemaFixtureProfile.Bridge,
            [Client(),Sql("INSERT INTO connection_profiles(profile_id,backend_kind,display_name,row_version,android_user_id,endpoint_metadata_json,verification_strength) VALUES('fixture','self_adb','fixture',1,0,'{}','fixture')"),Reject("INSERT INTO client_raw_grants VALUES('fixture','fixture','a.I',0,1,0,?,'allow','{\"effect\":\"allow\"}',1)",A),Query("SELECT count(*) FROM client_raw_grants",["0"])]),
        new SqliteSchemaCase("SQL-16-metadata-integrity",SchemaFixtureProfile.Metadata,
            [Query("PRAGMA foreign_keys",["1"]),Query("SELECT generation FROM metadata_state",["0"]),Query("PRAGMA integrity_check",["ok"]),Query("PRAGMA foreign_key_check")]),
        new SqliteSchemaCase("SQL-17-metadata-null-schema-rejected",SchemaFixtureProfile.Metadata,
            [Reject("INSERT INTO metadata_revisions VALUES('functional-group','fg:location',1,'local','active',NULL,?,'{\"schema_version\":null,\"group_id\":\"fg:location\"}','fixture')",A),Query("SELECT count(*) FROM metadata_revisions",["0"])]),
        new SqliteSchemaCase("SQL-18-metadata-empty-reason",SchemaFixtureProfile.Metadata,
            [Reject("INSERT INTO metadata_revisions VALUES('functional-group','fg:location',1,'local','active',NULL,?,?,'   ')",A,Group),Query("SELECT count(*) FROM metadata_revisions",["0"])]),
        new SqliteSchemaCase("SQL-19-metadata-base-immutable",SchemaFixtureProfile.Metadata,
            [Sql("INSERT INTO metadata_bases VALUES('functional-group','fg:location',?,?)",A,Group),Reject("UPDATE metadata_bases SET document_json='{}'"),Reject("DELETE FROM metadata_bases"),Query("SELECT count(*) FROM metadata_bases",["1"])]),
        new SqliteSchemaCase("SQL-20-metadata-history-and-head-cas",SchemaFixtureProfile.Metadata,
            [MetadataRevision(),Sql("INSERT INTO metadata_heads VALUES('functional-group','fg:location',1)"),Reject("UPDATE metadata_revisions SET reason='changed'"),Reject("DELETE FROM metadata_revisions"),Reject("DELETE FROM metadata_heads"),Reject("UPDATE metadata_heads SET revision=3"),MetadataRevision(2),Sql("UPDATE metadata_heads SET revision=2"),Reject("UPDATE metadata_heads SET revision=2"),Query("SELECT revision FROM metadata_heads",["2"]),Query("SELECT generation FROM metadata_state",["2"]),Query("SELECT count(*) FROM metadata_revisions",["2"])]),
        new SqliteSchemaCase("SQL-21-metadata-initial-head-revision",SchemaFixtureProfile.Metadata,
            [Sql("BEGIN IMMEDIATE"),MetadataRevision(2),Reject("INSERT INTO metadata_heads VALUES('functional-group','fg:location',2)"),Sql("ROLLBACK"),Query("SELECT count(*) FROM metadata_revisions",["0"]),Query("SELECT generation FROM metadata_state",["0"])]),
        new SqliteSchemaCase("SQL-22-metadata-imported-content-pin",SchemaFixtureProfile.Metadata,
            [Sql("INSERT INTO metadata_bases VALUES('functional-group','fg:location',?,?)",A,Group),Reject("INSERT INTO metadata_revisions VALUES('functional-group','fg:location',1,'imported','active',?,?,?,'fixture')",A,B,Group),Query("SELECT count(*) FROM metadata_revisions",["0"])]),
        new SqliteSchemaCase("SQL-23-combined-one-domain",SchemaFixtureProfile.Combined,
            [Query("SELECT name FROM sqlite_master WHERE name IN ('catalog_meta','clients','metadata_state') ORDER BY name",["catalog_meta"],["clients"],["metadata_state"]),Query("PRAGMA foreign_key_check")]),
        // Foreign keys are disabled only inside these private fresh attack fixtures,
        // so a NULL rejection proves the natural key guard rather than a missing parent.
        new SqliteSchemaCase("SQL-24-pin-natural-key-rejects-null",SchemaFixtureProfile.Bridge,
            [Query("SELECT name,\"notnull\",pk FROM pragma_table_info('device_view_pins') WHERE name='pin_id'",["pin_id","1","1"]),
             Sql("PRAGMA foreign_keys=OFF"),Query("PRAGMA foreign_keys",["0"]),
             Reject("INSERT INTO device_view_pins VALUES(NULL,'fixture-view','execution','fixture-expiry')"),
             Query("SELECT count(*) FROM device_view_pins",["0"])]),
        new SqliteSchemaCase("SQL-25-wire-selection-natural-key-rejects-null",SchemaFixtureProfile.Bridge,
            [Query("SELECT name,\"notnull\",pk FROM pragma_table_info('wire_proof_active') WHERE name='contract_id'",["contract_id","1","1"]),
             Sql("PRAGMA foreign_keys=OFF"),Query("PRAGMA foreign_keys",["0"]),
             Reject("INSERT INTO wire_proof_active VALUES(NULL,1)"),Query("SELECT count(*) FROM wire_proof_active",["0"])])
    }.Select(x=>new object[]{x});
    public static void AssertCase(SqliteSchemaCase test,ISchemaCaseProvider provider)
    {
        foreach(var command in test.Commands)
        {
            var actual=provider.Execute(command);
            Assert.Equal(command.Rejected,actual.Rejected);Assert.Equal(command.ErrorCode,actual.ErrorCode);
            Assert.True(command.Rows.Length==actual.Rows.Length,
                "SQL row count mismatch. Expected "+command.Rows.Length+", actual "+actual.Rows.Length+": "+
                string.Join(" | ",actual.Rows.Select(row=>string.Join(",",row.Select(cell=>cell ?? "<NULL>")))));
            for(var i=0;i<command.Rows.Length;i++) Assert.Equal(command.Rows[i],actual.Rows[i]);
        }
    }
}
public sealed class ScriptedSchemaProvider(SqliteSchemaCase expected,int corruptAt=-1,int corruptField=0):ISchemaCaseProvider
{
    private int index;
    public SchemaReply Execute(SchemaCommand command)
    {
        Assert.Same(expected.Commands[index],command);var position=index++;
        var result=new SchemaReply(command.Rows,command.Rejected,command.ErrorCode);
        if(position!=corruptAt) return result;
        return corruptField switch {0=>result with{Rejected=!result.Rejected},1=>result with{ErrorCode=999},_=>result with{Rows=[["wrong"]]}};
    }
    public void AssertConsumed()=>Assert.Equal(expected.Commands.Length,index);
    public void Dispose() { }
}
[Trait("Gate","Mock")]
public sealed class SqliteSchemaMockTests
{
    [Theory,MemberData(nameof(SqliteSchemaCases.All),MemberType=typeof(SqliteSchemaCases))]
    public void CorrectMocksExerciseEveryStatement(SqliteSchemaCase test)
    { using var fake=new ScriptedSchemaProvider(test);SqliteSchemaCases.AssertCase(test,fake);fake.AssertConsumed(); }
    [Theory,MemberData(nameof(SqliteSchemaCases.All),MemberType=typeof(SqliteSchemaCases))]
    public void EveryResponseMutationAtEveryStatementRejected(SqliteSchemaCase test)
    {
        for(var position=0;position<test.Commands.Length;position++)
        for(var field=0;field<3;field++)
        {
            using var fake=new ScriptedSchemaProvider(test,position,field);
            Assert.NotNull(Record.Exception(()=>SqliteSchemaCases.AssertCase(test,fake)));
        }
    }
}
