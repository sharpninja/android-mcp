using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Catalog.Migration;
using AndroidMcp.BuildTool.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog.Migration;

public sealed class NativeCatalogMigrationProvider:ICatalogMigrationCaseProvider
{
    private const string Binding=CatalogMigrationCases.Binding,Hash=CatalogMigrationCases.Hash;
    private static readonly byte[] Existing=Encoding.UTF8.GetBytes("independently retained occupied path");
    internal static string Root()
    {
        for(var directory=new DirectoryInfo(AppContext.BaseDirectory);directory is not null;directory=directory.Parent)
            if(Directory.Exists(Path.Combine(directory.FullName,"publication","android-mcp-git")))return directory.FullName;
        throw new DirectoryNotFoundException("Retained published migration inputs were not found");
    }
    private static void Execute(CatalogDatabase db,string sql,params (string Name,object? Value)[] parameters)
    {using var command=db.Command(sql,null,parameters);command.ExecuteNonQuery();}
    private static object? Scalar(CatalogDatabase db,string sql){using var command=db.Command(sql);return command.ExecuteScalar();}
    private static JsonArray Rows(CatalogDatabase db,string sql)
    {
        using var command=db.Command(sql);using var reader=command.ExecuteReader();var result=new JsonArray();
        while(reader.Read()){var row=new JsonArray();for(var index=0;index<reader.FieldCount;index++)row.Add(reader.IsDBNull(index)?null:JsonSerializer.SerializeToNode(reader.GetValue(index)));result.Add(row);}return result;
    }
    private static void NewBytes(string path,byte[] bytes){using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);stream.Write(bytes);stream.Flush(true);}
    internal static string ReadPinned(string path,string hash){var bytes=File.ReadAllBytes(path);Assert.Equal(hash,ContentHash.Sha256(bytes));return Encoding.UTF8.GetString(bytes);}
    internal static IStreamingArtifactReader ConfiguredReader(string root)
        =>AndroidMcp.BuildTool.Build.CreateConfiguredArtifactStore(root);
    internal static string[] OldScripts()
    {
        var evidence=Path.Combine(Root(),"publication","android-mcp-git","rehydration","producers","evidence");
        return [ReadPinned(Path.Combine(evidence,"original-001-initial.sql"),"9343d74cbed451847f7c135d02e155030603f9cdaf71033245749812647310bb"),ReadPinned(Path.Combine(evidence,"original-002-evidence-and-publication.sql"),"7fdc164e218711a843d296bd7ac1a754169636ebddabc2e24fe439640577bfd3")];
    }
    private static void Registry(CatalogDatabase db,string id,long version,string status)=>Execute(db,"INSERT INTO registries VALUES($id,$version,'1.1.0','fixture',37,$hash,'now',$state,'{}')",("$id",id),("$version",version),("$hash",Hash),("$state",status));
    private static void Entry(CatalogDatabase db)
    {
        Execute(db,"INSERT INTO bindings(binding_id,signature,package,kind,name,raw_signature) VALUES($id,'java:a.A','a','class','A','fixture')",("$id",Binding));
        Execute(db,"INSERT INTO effective_entries VALUES($id,'{}',$hash,'fixture')",("$id",Binding),("$hash",Hash));
    }
    private static void Populate(CatalogDatabase old,string variant)
    {
        if(variant=="version-three")Execute(old,"PRAGMA user_version=3");
        if(variant=="source-fk"){Execute(old,"PRAGMA foreign_keys=OFF");Execute(old,"INSERT INTO occurrences VALUES('absent-binding','absent-source',1)");return;}
        if(variant=="target-constraint"){Execute(old,"INSERT INTO bindings(binding_id,signature,package,kind,name,raw_signature) VALUES('invalid-id','java:a.A','a','class','A','fixture')");return;}
        if(variant=="missing-overlay-history")
        {
            Registry(old,"r",1,"published");Execute(old,"INSERT INTO active_registry VALUES(1,'r')");Entry(old);
            Execute(old,"INSERT INTO overlays VALUES($id,$id,'java:a.A',1,'r',$hash,'suppressed','{}','now','now')",("$id",Binding),("$hash",Hash));return;
        }
        if(variant is "offline-analyze" or "published-five" or "retired-before-published" or "matching-base" or "null-base" or "stale-base" or "deleted-local-collision" or "transforms")
        {
            if(variant=="retired-before-published"){Registry(old,"old",1,"retired");Registry(old,"selected",5,"published");Execute(old,"INSERT INTO active_registry VALUES(1,'selected')");}
            else
            {
                Registry(old,variant=="offline-analyze"?"fixture":"r",variant=="published-five"?5:1,"published");
                if(variant!="offline-analyze")Execute(old,"INSERT INTO active_registry VALUES(1,'r')");
            }
        }
        if(variant=="offline-analyze")Execute(old,"ANALYZE");
        if(variant is "matching-base" or "null-base" or "stale-base" or "deleted-local-collision" or "entries-without-active" or "transforms")Entry(old);
        if(variant is "matching-base" or "null-base" or "stale-base" or "deleted-local-collision")
        {
            var local=variant=="deleted-local-collision";
            Execute(old,"INSERT INTO overlays VALUES($id,$imported,'java:a.A',1,$registry,$base,$state,'{}','now','now')",
                ("$id",Binding),("$imported",local?null:Binding),("$registry",local?null:"r"),("$base",variant=="null-base"||local?null:variant=="stale-base"?"stale":Hash),("$state",local?"deleted":"suppressed"));
            Execute(old,"INSERT INTO entry_history VALUES($id,1,$operation,'{}','fixture','now')",("$id",Binding),("$operation",local?"delete":"suppress"));
        }
        if(variant!="transforms")return;
        Execute(old,"INSERT INTO catalog_meta VALUES('schema_version','1.1.0')");
        Execute(old,"INSERT INTO sources VALUES('s','api.txt','https://example.invalid/api.txt','rev','now',$hash,37,'stable','sdk-signature','fixture')",("$hash",Hash));
        Execute(old,"INSERT INTO occurrences VALUES($id,'s',1)",("$id",Binding));
        Execute(old,"INSERT INTO binding_declarations VALUES($id,'s',1,'{}',$hash)",("$id",Binding),("$hash",Hash));
        Execute(old,"INSERT INTO permission_definitions VALUES('p','s','normal','{}')");
        Execute(old,"INSERT INTO permission_relations VALUES(1,$id,'s',1,'fixture','value','{\"op\":\"permission\",\"name\":\"p\"}','fixture')",("$id",Binding));
        Execute(old,"INSERT INTO aidl_schemas VALUES('s','a','a.I','interface','fixture','ok','{}','unknown')");
        Execute(old,"INSERT INTO issues VALUES(1,'s',1,'fixture','reason','raw')");Execute(old,"INSERT INTO registry_sources VALUES('r','s')");
        Execute(old,"INSERT INTO source_details VALUES('s','{}')");Execute(old,"INSERT INTO parse_reports VALUES('s','{}')");
        Execute(old,"INSERT INTO annotation_evidence VALUES('s','item',$id,'direct','{}')",("$id",Binding));
        Execute(old,"INSERT INTO permission_symbols VALUES('p','p','s',1)");
        Execute(old,"INSERT INTO import_jobs VALUES('job',$hash,'r','published','{}')",("$hash",Hash));
    }
    private static string Diagnostic(Exception error)
    {
        foreach(var meaning in new[]{"Destination must be new","Only storage user_version=2 can migrate to v3","Source integrity/FK failure","Migrated integrity/FK failure","file is not a database","No active registry for legacy effective entries","Use an imported overlay for an active imported identity","Imported overlay base must match active entry","CHECK constraint failed","Artifact already exists and was retained"})
            if(error.Message.Contains(meaning,StringComparison.Ordinal))return meaning;
        return error.GetType().Name+": "+error.Message;
    }
    public async Task<JsonObject> ExecuteAsync(CatalogMigrationCall call)
    {
        var workspace=Root();var published=Path.Combine(workspace,"publication","android-mcp-git");
        var catalog=call.TargetProfile=="release-011"?Path.Combine(published,"rehydration","producers","release-011"):Path.Combine(workspace,"remediation","catalog");
        var oldScripts=OldScripts();
        var targetNames=CatalogMigrationCases.TargetNames.Concat(call.TargetProfile=="current"?new[]{"source_metadata_ddl.sql"}:Array.Empty<string>()).ToArray();
        var targetHashes=CatalogMigrationCases.TargetHashes.Concat(call.TargetProfile=="current"?new[]{CatalogMigrationCases.MetadataHash}:Array.Empty<string>()).ToArray();
        Assert.Equal(targetNames,Directory.GetFiles(Path.Combine(catalog,"migrations"),"*.sql").Select(Path.GetFileName).Order(StringComparer.Ordinal));
        var targetScripts=targetNames.Select((name,index)=>ReadPinned(Path.Combine(catalog,"migrations",name),targetHashes[index])).ToArray();
        var root=Path.Combine(Path.GetTempPath(),"catalog-migration-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var source=Path.Combine(root,"old.sqlite");var target=Path.Combine(root,"new.sqlite");var stage=target+".staging";
        if(call.Variant=="source-corrupt")NewBytes(source,Encoding.UTF8.GetBytes("independent non SQLite source"));
        else using(var old=CatalogDatabase.CreatePrivateFile(source,call.Variant=="version-one"?oldScripts[..1]:oldScripts))Populate(old,call.Variant);
        if(call.Variant=="destination-exists")NewBytes(target,Existing);
        if(call.Variant=="stage-exists")NewBytes(stage,Existing);
        var original=File.ReadAllBytes(source);var readonlyOpened=false;var closedBeforePublication=false;
        var migration=new CatalogCopyMigration(catalog,ConfiguredReader,(path,readOnly)=>{readonlyOpened=readOnly;return CatalogDatabase.OpenExisting(path,readOnly);},outputRoot=>
        {
            using(var sourceClosed=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.None)){}
            using(var stageClosed=new FileStream(stage,FileMode.Open,FileAccess.Read,FileShare.None)){}
            closedBeforePublication=true;
            if(call.Variant=="publish-conflict")NewBytes(target,Existing);
            return new FileArtifactStore(outputRoot,bytes=>ContentHash.Sha256(bytes.Span));
        });
        CatalogMigrationResult? migrated=null;var error=await Record.ExceptionAsync(async()=>migrated=await migration.MigrateAsync(source,target));
        var targetListExact=migrated is null||migrated.TargetMigrations.Select(pin=>pin.RelativePath).SequenceEqual(targetNames.Select(name=>"migrations/"+name))&&migrated.TargetMigrations.Select(pin=>pin.Sha256).SequenceEqual(targetHashes);
        var retained=call.Variant switch{"destination-exists" or "publish-conflict"=>File.ReadAllBytes(target).SequenceEqual(Existing),"stage-exists"=>File.ReadAllBytes(stage).SequenceEqual(Existing),_=>true};
        var rolledBack=true;
        if(error is not null&&File.Exists(stage)&&call.Variant is not ("stage-exists" or "publish-conflict"))
        {
            using var failed=CatalogDatabase.OpenExisting(stage);
            rolledBack=(long)Scalar(failed,"SELECT count(*) FROM registries")! ==0&&(long)Scalar(failed,"SELECT count(*) FROM bindings")! ==0&&(long)Scalar(failed,"SELECT count(*) FROM overlays")! ==0;
        }
        var result=new JsonObject
        {
            ["outcome"]=error is null?"migrated":"rejected",["diagnostic"]=error is null?"":Diagnostic(error),["source_bytes_unchanged"]=original.SequenceEqual(File.ReadAllBytes(source)),
            ["destination_exists"]=File.Exists(target),["stage_exists"]=File.Exists(stage),["existing_bytes_retained"]=retained,["source_open_readonly"]=readonlyOpened,["target_list_exact"]=targetListExact,["failed_transaction_rolled_back"]=rolledBack
        };
        if(error is not null)return result;
        Assert.NotNull(migrated);using var next=CatalogDatabase.OpenExisting(target);
        using var reference=CatalogDatabase.CreatePrivateFile(Path.Combine(root,"exact-target-reference.sqlite"),targetScripts);
        var schemaSql="SELECT type,name,tbl_name,sql FROM sqlite_master WHERE name NOT LIKE 'sqlite_%' ORDER BY type,name";
        result["user_version"]=(long)Scalar(next,"PRAGMA user_version")!;
        result["rebuilt_with_corrected_extractor"]=(string)Scalar(next,"SELECT value FROM catalog_meta WHERE key='rebuilt_with_corrected_extractor'")!;
        result["integrity"]=(string)Scalar(next,"PRAGMA integrity_check")!;result["foreign_keys"]=Rows(next,"PRAGMA foreign_key_check").Count;
        result["stage_equals_final"]=File.ReadAllBytes(stage).SequenceEqual(File.ReadAllBytes(target))&&migrated.Destination.Sha256==ContentHash.Sha256(File.ReadAllBytes(target))&&migrated.RetainedStage.Sha256==migrated.Destination.Sha256;
        result["schema_equals_exact_target"]=JsonNode.DeepEquals(Rows(reference,schemaSql),Rows(next,schemaSql));
        result["metadata_tables"]=(long)Scalar(next,"SELECT count(*) FROM sqlite_master WHERE type='table' AND name='metadata_state'")! ==1;
        result["registry_generation"]=(long)Scalar(next,"SELECT registry_generation FROM catalog_view")!;
        var assertions=closedBeforePublication&&migrated.Source.Sha256==ContentHash.Sha256(original)&&migrated.Source.Length==original.LongLength;
        assertions&=call.Variant switch
        {
            "offline-analyze"=>(string)Scalar(next,"SELECT state FROM registries")! =="published"&&(long)Scalar(next,"SELECT count(*) FROM sqlite_master WHERE name='sqlite_stat1'")! ==0,
            "published-five"=>(string)Scalar(next,"SELECT registry_id FROM active_registry")! =="r",
            "retired-before-published"=>JsonNode.DeepEquals(Rows(next,"SELECT registry_id,state FROM registries ORDER BY registry_id"),JsonNode.Parse("[[\"old\",\"retired\"],[\"selected\",\"published\"]]"))&&(string)Scalar(next,"SELECT registry_id FROM active_registry")! =="selected",
            "matching-base"=>JsonNode.DeepEquals(Rows(next,"SELECT state,base_record_hash FROM overlays"),new JsonArray(new JsonArray("suppressed",Hash))),
            "transforms"=>TransformAssertions(next),
            "no-active-empty"=>(long)Scalar(next,"SELECT count(*) FROM active_registry")! ==0&&(long)Scalar(next,"SELECT count(*) FROM registry_entries")! ==0,
            _=>false
        };
        result["original_assertions"]=assertions;return result;
    }
    private static bool TransformAssertions(CatalogDatabase next)
    {
        var checks=(string)Scalar(next,"SELECT value FROM catalog_meta WHERE key='schema_version'")! =="1.2.0";
        checks&=JsonNode.DeepEquals(Rows(next,"SELECT logic,expression_json FROM permission_relations"),JsonNode.Parse("[[\"permission\",\"{\\\"op\\\":\\\"permission\\\",\\\"name\\\":\\\"p\\\"}\"]]"));
        checks&=JsonNode.DeepEquals(Rows(next,"SELECT idempotency_key,request_sha256,registry_id,state,result_json FROM legacy_import_jobs"),new JsonArray(new JsonArray("job",Hash,"r","published","{}")));
        checks&=JsonNode.DeepEquals(Rows(next,"SELECT registry_id,entry_id,document_json,record_sha256 FROM registry_entries"),new JsonArray(new JsonArray("r",Binding,"{}",Hash)));
        checks&=(string)Scalar(next,"SELECT etag FROM effective_entries")! =="\"cat1:1:"+Hash+":"+Binding+":0:active\"";
        checks&=(string)Scalar(next,"SELECT record_sha256 FROM binding_declarations")! ==Hash;
        foreach(var table in new[]{"sources","bindings","occurrences","binding_declarations","permission_definitions","permission_relations","aidl_schemas","issues","registry_sources","source_details","parse_reports","annotation_evidence","permission_symbols"})checks&=(long)Scalar(next,"SELECT count(*) FROM "+table)! ==1;
        return checks;
    }
}
[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004"),Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogMigrationRealTests
{
    [Theory,MemberData(nameof(CatalogMigrationCases.All),MemberType=typeof(CatalogMigrationCases))]
    public Task SameOriginalCallersAndMigrationEffectsBindSharedNativeStorage(CatalogMigrationCase test)=>CatalogMigrationCases.AssertCase(test,new NativeCatalogMigrationProvider());
}
