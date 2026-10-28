using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using Json.Schema;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogMetadataObligationProvider:ICatalogMetadataObligationCaseProvider
{
    public Task<JsonObject> ExecuteAsync(CatalogMetadataObligationCall call)=>Task.FromResult(Execute(call));
    private static JsonObject Execute(CatalogMetadataObligationCall call)
    {
        var root=NativeSchemaProvider.Root();var canonical=new CanonicalJson();var metadata=new CatalogSourceMetadata(Path.Combine(root,"contracts/SourceContractMetadata.schema.json"),canonical);
        using var database=CatalogDatabase.CreateInMemory(root,CatalogSchemaProfile.Metadata);
        var trusted=new Dictionary<string,MetadataCatalogIdentity>(StringComparer.Ordinal);
        if(call.Variant is "descriptor" or "wrong-descriptor-pin" or "nontype-descriptor-reference")trusted.Add(new string('a',64),new(call.Variant=="nontype-descriptor-reference"?"method":"class",new string(call.Variant=="wrong-descriptor-pin"?'d':'b',64)));
        if(call.Variant=="member-pin")trusted.Add(new string('a',64),new("method",new string('b',64)));
        var store=new MetadataCatalog(database,canonical,new NativeContentHash(),Path.Combine(root,"contracts/FunctionalGroup.schema.json"),trusted);
        var doc=call.Document.DeepClone().AsObject();var kind=doc.ContainsKey("callback_id")?"callback-descriptor":"functional-group";var key=doc[kind=="callback-descriptor"?"callback_id":"group_id"]!.GetValue<string>();
        MetadataSnapshot Create()=>store.Create(kind,key,JsonSerializer.SerializeToElement(doc),"fixture owner mapping",true,"*");
        int Status(Action action){try{action();return 200;}catch(CatalogException error){return error.Status;}}
        bool Constraint(string sql,params (string Name,object? Value)[] values)
        {using var command=database.Command(sql,null,values);var error=Record.Exception(()=>command.ExecuteNonQuery());return error is SqliteException sqlite && sqlite.SqliteErrorCode==19;}
        switch(call.Variant)
        {
            case "lifecycle":
            {
                doc["members"]=new JsonArray(new JsonObject{["binding_id"]=new string('a',64),["contract_hash"]=new string('b',64),["lifecycle_unit_id"]="u",["role"]="register"});
                var error=Record.Exception(()=>metadata.ValidateGroup(doc));Assert.Contains("coherent",error?.Message ?? "",StringComparison.Ordinal);
                doc["members"]!.AsArray().Add(new JsonObject{["binding_id"]=new string('c',64),["contract_hash"]=new string('d',64),["lifecycle_unit_id"]="u",["role"]="cleanup"});metadata.ValidateGroup(doc);
                return new(){["register_only_rejected"]=error is FormatException,["paired_unit_valid"]=true};
            }
            case "immutable":
            {
                var row=Create();var deleted=Constraint("DELETE FROM metadata_revisions");var rewritten=Constraint("UPDATE metadata_revisions SET reason='changed'");var head=Constraint("DELETE FROM metadata_heads");
                return new(){["delete_revisions_rejected"]=deleted,["rewrite_revisions_rejected"]=rewritten,["delete_heads_rejected"]=head,["row_unchanged"]=store.Get(kind,key)==row};
            }
            case "sql-null":
            {
                doc["schema_version"]=null;var rejected=Constraint("INSERT INTO metadata_revisions VALUES($kind,$key,1,'local','active',NULL,$hash,$body,'fixture')",("$kind",kind),("$key",key),("$hash",new string('a',64)),("$body",doc.ToJsonString()));
                using var count=database.Command("SELECT count(*) FROM metadata_revisions");return new(){["null_schema_SQL_rejected"]=rejected,["no_partial_row"]=(long)count.ExecuteScalar()! ==0};
            }
            case "member-pin":
            {
                doc["members"]=new JsonArray(new JsonObject{["binding_id"]=new string('a',64),["contract_hash"]=new string('c',64),["lifecycle_unit_id"]=null,["role"]="operation"});var wrong=Status(()=>Create());
                doc["members"]![0]!["contract_hash"]=new string('b',64);MetadataSnapshot? row=null;var correct=Status(()=>row=Create());
                return new(){["wrong_pin_status"]=wrong,["correct_pin_status"]=correct,["saved_contract_exact"]=row is not null && JsonNode.Parse(row.Document)!["members"]![0]!["contract_hash"]!.GetValue<string>()==new string('b',64)};
            }
            case "descriptor":
            {
                MetadataSnapshot? first=null;var created=Status(()=>first=Create());Assert.NotNull(first);var old=first!;
                doc["classification"]="unknown";doc["materialization"]=new JsonObject{["strategy"]="interface_dynamic_proxy"};
                var rejected=Status(()=>store.Put(kind,key,JsonSerializer.SerializeToElement(doc),old.ETag,"unproven role cannot assert positive materializer",true));var current=store.Get(kind,key);
                return new(){["create_status"]=created,["strategy"]=JsonNode.Parse(old.Document)!["materialization"]!["strategy"]!.DeepClone(),["positive_unknown_materializer_status"]=rejected,["revision"]=current.Revision,["generation"]=store.Generation,["head_unchanged"]=old==current};
            }
            case "unknown-descriptor":metadata.ValidateDescriptor(doc);return new(){["valid"]=true,["strategy"]=doc["materialization"]!["strategy"]!.DeepClone(),["input_unchanged"]=JsonNode.DeepEquals(doc,call.Document)};
            case "representation":
            {
                var row=Create();var value=store.Representation(kind,key);var api=JsonNode.Parse(File.ReadAllText(Path.Combine(root,"contracts/openapi.json")))!;
                var schema=new JsonObject{["$ref"]="#/components/schemas/MetadataRepresentation",["components"]=api["components"]!.DeepClone()};
                return new(){["schema_valid"]=JsonSchema.FromText(schema.ToJsonString()).Evaluate(JsonSerializer.SerializeToElement(value),new(){OutputFormat=OutputFormat.Flag}).IsValid,
                    ["contains_document_json"]=value.ContainsKey("document_json"),["document_exact"]=JsonNode.DeepEquals(value["document"],JsonNode.Parse(row.Document))};
            }
            case "openapi":
            {
                var api=JsonNode.Parse(File.ReadAllText(Path.Combine(root,"contracts/openapi.json")))!.AsObject();
                void Visit(JsonNode? node)
                {
                    if(node is JsonObject map)
                    {
                        if(map["$ref"] is JsonValue reference)
                        {var pointer=reference.GetValue<string>();Assert.StartsWith("#/",pointer);JsonNode? target=api;foreach(var part in pointer[2..].Split('/'))target=target![part];Assert.NotNull(target);}
                        foreach(var item in map)Visit(item.Value);
                    }
                    else if(node is JsonArray list)foreach(var item in list)Visit(item);
                }
                Visit(api);bool Verbs(string path)=>api["paths"]!["/v1/catalog/"+path]!.AsObject().Select(pair=>pair.Key).ToHashSet(StringComparer.Ordinal).SetEquals(["get","put","delete"]);
                return new(){["all_local_references_resolve"]=true,["descriptor_verbs_exact"]=Verbs("callback-descriptors/{callback_id}"),["group_verbs_exact"]=Verbs("functional-groups/{group_id}")};
            }
            case "missing-descriptor-type":case "wrong-descriptor-pin":case "nontype-descriptor-reference":
            {
                var status=Status(()=>Create());using var count=database.Command("SELECT count(*) FROM metadata_heads");return new(){["status"]=status,["generation"]=store.Generation,["no_partial_row"]=(long)count.ExecuteScalar()! ==0};
            }
            default:throw new ArgumentException("Unknown original metadata fixture",nameof(call));
        }
    }
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogMetadataObligationRealTests
{
    [Theory,MemberData(nameof(CatalogMetadataObligationCases.All),MemberType=typeof(CatalogMetadataObligationCases))]
    public Task SameElevenOriginalDescriptorAndGroupObligations(CatalogMetadataObligationCase test)=>CatalogMetadataObligationCases.AssertCase(test,new NativeCatalogMetadataObligationProvider());
}
