using System.Text.Json;
using Json.Schema;
using System.Text.Json.Nodes;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogMetadataBodyCall(string Operation,JsonObject Document);
public sealed record CatalogMetadataBodyReply(bool Accepted,int? Status=null,int? HistoryRows=null,bool? HistoryExact=null,bool? NoEnumeration=null,bool InputUnchanged=true,string? ErrorContains=null);
public sealed record CatalogMetadataBodyCase(string Id,string OriginalFunction,CatalogMetadataBodyCall Call,CatalogMetadataBodyReply Expected);
public interface ICatalogMetadataBodyCaseProvider { Task<CatalogMetadataBodyReply> ExecuteAsync(CatalogMetadataBodyCall call); }
public static class CatalogMetadataBodyCases
{
    // Original source_metadata_reference.py descriptor/group guards and
    // source_metadata_store.py history/validate_document callers. These are
    // module-body proofs, separate from the26 original unit method closures.
    public static JsonObject Method(string id="dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd")=>new()
    {["method_id"]=id,["contract_hash"]=new string('e',64),["reply_mode"]="unknown",["required_type_ids"]=new JsonArray()};
    public static JsonObject Member(char hash='b',string? unit=null)=>new()
    {["binding_id"]=new string('a',64),["contract_hash"]=new string(hash,64),["role"]="operation",["lifecycle_unit_id"]=unit};
    public static JsonObject Dependency(string id)=>new(){["group_id"]=id,["revision"]=1,["content_hash"]=new string('a',64)};
    private static string Error(string operation)=>operation switch
    {
        "duplicate-method"=>"Duplicate callback method identity","proven-no-evidence"=>"Proven role requires source evidence",
        "proven-empty-cleanup"=>"Proven cleanup requires exact binding/contract/evidence","bad-adapter"=>"Invalid CallbackDescriptor",
        "register-no-unit"=>"Lifecycle operation requires an atomic unit","duplicate-member"=>"Duplicate group binding role/unit",
        "conflicting-member"=>"One group cannot select incompatible contracts of one binding","duplicate-type"=>"Duplicate type dependency",
        "self-dependency"=>"Duplicate/self group dependency","dependency-cycle"=>"Group dependency cycle","estimator-pin"=>"Nonzero token estimate needs estimator version",
        _=>throw new ArgumentException("Unknown original guard",nameof(operation))
    };
    public static IReadOnlyDictionary<string,JsonObject> CycleGroups()
    {
        var a=MetadataCrudCases.Group();a["group_id"]="fg:aa";a["dependencies"]=new JsonArray(Dependency("fg:bb"));
        var b=MetadataCrudCases.Group();b["group_id"]="fg:bb";b["dependencies"]=new JsonArray(Dependency("fg:aa"));
        return new Dictionary<string,JsonObject>(StringComparer.Ordinal){["fg:aa"]=a,["fg:bb"]=b};
    }
    private static CatalogMetadataBodyCase Descriptor(string id,string operation,Action<JsonObject> edit,bool store=false)
    {var doc=CatalogMetadataObligationCases.Descriptor();edit(doc);return new(id,store?"MetadataCatalog.validate_document":"validate_descriptor",new(operation,doc),new(false,Status:store?422:null,ErrorContains:store?null:Error(operation)));}
    private static CatalogMetadataBodyCase Group(string id,string operation,Action<JsonObject> edit)
    {var doc=MetadataCrudCases.Group();edit(doc);return new(id,"validate_group",new(operation,doc),new(false,ErrorContains:Error(operation)));}
    public static IEnumerable<object[]> All=>Generate().Select(test=>new object[]{test});
    public static IEnumerable<CatalogMetadataBodyCase> Generate()
    {
        yield return Descriptor("META-BODY-D01-duplicate-method","duplicate-method",doc=>doc["methods"]=new JsonArray(Method(),Method()));
        yield return Descriptor("META-BODY-D02-proven-needs-source","proven-no-evidence",doc=>doc["classification"]="proven_callback");
        yield return Descriptor("META-BODY-D03-proven-cleanup-exact","proven-empty-cleanup",doc=>doc["cleanup"]!["status"]="proven");
        yield return Descriptor("META-BODY-D04-reviewed-adapter-pin","bad-adapter",doc=>doc["materialization"]=new JsonObject{["strategy"]="prebuilt_adapter",["adapter"]=new JsonObject{["adapter_id"]="fixture",["adapter_version"]="1",["adapter_sha256"]=new string('a',63)}});
        yield return Descriptor("META-BODY-D05-method-trusted-pin","untrusted-method",doc=>doc["methods"]=new JsonArray(Method()),true);
        yield return Descriptor("META-BODY-D06-registration-trusted-pin","untrusted-registration",doc=>doc["registration_binding_ids"]=new JsonArray(new string('d',64)),true);
        yield return Descriptor("META-BODY-D07-cleanup-trusted-pin","untrusted-cleanup",doc=>{doc["cleanup"]!["binding_id"]=new string('d',64);doc["cleanup"]!["contract_hash"]=new string('e',64);},true);
        yield return Descriptor("META-BODY-D08-method-type-trusted-pin","untrusted-method-type",doc=>{var method=Method(new string('f',64));method["required_type_ids"]=new JsonArray(new string('d',64));doc["methods"]=new JsonArray(method);},true);
        yield return Group("META-BODY-G01-register-needs-unit","register-no-unit",doc=>{var member=Member();member["role"]="register";doc["members"]=new JsonArray(member);});
        yield return Group("META-BODY-G02-duplicate-member-role","duplicate-member",doc=>doc["members"]=new JsonArray(Member(),Member()));
        yield return Group("META-BODY-G03-conflicting-contracts","conflicting-member",doc=>doc["members"]=new JsonArray(Member(),Member('c',"u")));
        yield return Group("META-BODY-G04-duplicate-type","duplicate-type",doc=>doc["required_type_ids"]=new JsonArray(new string('a',64),new string('a',64)));
        yield return Group("META-BODY-G05-self-dependency","self-dependency",doc=>doc["dependencies"]=new JsonArray(Dependency("fg:location")));
        yield return Group("META-BODY-G06-cycle","dependency-cycle",doc=>doc["dependencies"]=new JsonArray(Dependency("fg:aa")));
        yield return Group("META-BODY-G07-estimator-pin","estimator-pin",doc=>doc["estimated_tokens"]=1);
        yield return new("META-BODY-H01-history-exact-rows","MetadataCatalog.history",new("history",MetadataCrudCases.Group()),new(true,HistoryRows:3,HistoryExact:true));
        yield return new("META-BODY-H02-history-absent-empty","MetadataCatalog.history",new("empty-history",MetadataCrudCases.Group()),new(true,HistoryRows:0,HistoryExact:true));
        yield return new("META-BODY-I01-no-concerns-produce","produce_entry_metadata",new("missing-produce-concerns",CatalogAggregateMetadataCases.Declaration()),new(false,NoEnumeration:true,ErrorContains:"must be injected"));
        yield return new("META-BODY-I02-no-concerns-before-cursor","attach_occurrences",new("missing-attach-concerns",CatalogAggregateMetadataCases.Declaration()),new(false,NoEnumeration:true,ErrorContains:"must be injected"));
    }
    public static async Task AssertCase(CatalogMetadataBodyCase test,ICatalogMetadataBodyCaseProvider provider)
    {
        if(test.Call.Operation=="dependency-cycle")
        {
            // A cycle guard requires schema-valid nodes. The original Python
            // validate_group validates this schema before visiting dependencies.
            var schema=JsonSchema.FromText(File.ReadAllText(Path.Combine(NativeSchemaProvider.Root(),"contracts/FunctionalGroup.schema.json")));
            foreach(var value in CycleGroups().Values.Append(test.Call.Document))
                Assert.True(schema.Evaluate(JsonSerializer.SerializeToElement(value),new(){OutputFormat=OutputFormat.Flag}).IsValid,"Cycle fixture must satisfy the canonical FunctionalGroup schema before testing traversal");
        }
        var before=test.Call.Document.DeepClone();Assert.Equal(test.Expected,await provider.ExecuteAsync(test.Call));Assert.True(JsonNode.DeepEquals(before,test.Call.Document));
    }
}
public sealed class ScriptedCatalogMetadataBody(CatalogMetadataBodyCase test,int wrong=-1):ICatalogMetadataBodyCaseProvider
{
    public Task<CatalogMetadataBodyReply> ExecuteAsync(CatalogMetadataBodyCall call)
    {
        Assert.Same(test.Call,call);var value=test.Expected;return Task.FromResult(wrong switch
        {0=>value with{Accepted=!value.Accepted},1=>value with{Status=(value.Status??0)+1},2=>value with{HistoryRows=(value.HistoryRows??0)+1},
         3=>value with{HistoryExact=value.HistoryExact is null?true:!value.HistoryExact},4=>value with{NoEnumeration=value.NoEnumeration is null?true:!value.NoEnumeration},5=>value with{InputUnchanged=!value.InputUnchanged},6=>value with{ErrorContains="wrong"},_=>value});
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogMetadataBodyMockTests
{
    [Theory,MemberData(nameof(CatalogMetadataBodyCases.All),MemberType=typeof(CatalogMetadataBodyCases))]
    public Task OriginalMetadataModuleGuardsAndHistoryResponsesPass(CatalogMetadataBodyCase test)=>CatalogMetadataBodyCases.AssertCase(test,new ScriptedCatalogMetadataBody(test));
    [Theory,MemberData(nameof(CatalogMetadataBodyCases.All),MemberType=typeof(CatalogMetadataBodyCases))]
    public async Task EveryGuardHistoryAndAbsentConcernMutationRejected(CatalogMetadataBodyCase test)
    {for(var wrong=0;wrong<7;wrong++)Assert.NotNull(await Record.ExceptionAsync(()=>CatalogMetadataBodyCases.AssertCase(test,new ScriptedCatalogMetadataBody(test,wrong))));}
}
