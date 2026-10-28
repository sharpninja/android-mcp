using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogMetadataLeafProvider:ICatalogMetadataLeafProvider
{
    private static JsonObject Reply(params (string Key,object? Value)[] fields)
    {var result=new JsonObject();foreach(var (key,value) in fields)result[key]=JsonSerializer.SerializeToNode(value);return result;}
    private static string Hash(string path){using var file=File.OpenRead(path);return ContentHash.Sha256(file);}
    private static JsonElement Element(JsonNode value)=>JsonSerializer.SerializeToElement(value);
    private static Exception Failure(Action action,string contains)
    {var error=Record.Exception(action);Assert.NotNull(error);Assert.Contains(contains,error!.Message,StringComparison.OrdinalIgnoreCase);return error;}
    public async Task<JsonObject> ExecuteAsync(CatalogMetadataLeafCall call)
    {
        var root=NativeSchemaProvider.Root();var schemaPath=Path.Combine(root,"contracts/SourceContractMetadata.schema.json");var schemaHash=Hash(schemaPath);var canonical=new CanonicalJson();
        using var runtime=new SourcesRuntimeFactory().Create(SourceExtractionProfile.CurrentCandidate);
        var metadata=new CatalogSourceMetadata(schemaPath,canonical,runtime.Concerns.Callbacks,runtime.Concerns.Taxonomy);
        using var database=CatalogDatabase.CreateInMemory(root,CatalogSchemaProfile.Metadata);
        var store=new MetadataCatalog(database,canonical,new NativeContentHash(),Path.Combine(root,"contracts/FunctionalGroup.schema.json"));
        const string kind="functional-group";
        JsonObject Doc()=>call.Document.DeepClone().AsObject();
        MetadataSnapshot Create(JsonObject document,string reason="first reason")=>store.Create(kind,document["group_id"]!.GetValue<string>(),Element(document),reason,true,"*");
        MetadataSnapshot Import(JsonObject document,string reason="first base")=>store.Import(kind,document["group_id"]!.GetValue<string>(),Element(document),reason,true);
        int Count(string table){using var command=database.Command("SELECT count(*) FROM "+table);return checked((int)(long)command.ExecuteScalar()!);}
        string State()
        {
            var tables=new Dictionary<string,List<object?[]>>(StringComparer.Ordinal);
            foreach(var table in new[]{"metadata_state","metadata_heads","metadata_revisions","metadata_bases"})
            {
                using var command=database.Command("SELECT * FROM "+table+" ORDER BY 1,2");using var rows=command.ExecuteReader();var values=new List<object?[]>();
                while(rows.Read()){var row=new object?[rows.FieldCount];for(var index=0;index<row.Length;index++)row[index]=rows.IsDBNull(index)?null:rows.GetValue(index);values.Add(row);}tables.Add(table,values);
            }
            return canonical.Hash(JsonSerializer.SerializeToElement(tables));
        }
        int History(MetadataSnapshot row)=>store.History(kind,row.Key).Count;
        static JsonObject Parsed(MetadataSnapshot row)=>JsonNode.Parse(row.Document)!.AsObject();
        bool Schema(JsonObject value){metadata.Validate(value,"Envelope");return true;}
        bool SemanticReject(JsonObject value,string fragment){Assert.IsType<FormatException>(Failure(()=>metadata.ValidateMetadata(value),fragment));return true;}
        JsonObject result;
        switch(call.Variant)
        {
            case "invalid-schema":
            {
                var owned=Path.Combine(Path.GetTempPath(),"metadata-leaf-schema-"+Guid.NewGuid().ToString("N"));var contracts=Path.Combine(owned,"contracts");Directory.CreateDirectory(contracts);
                foreach(var source in Directory.EnumerateFiles(Path.Combine(root,"contracts"),"*.schema.json"))
                {var copy=Path.Combine(contracts,Path.GetFileName(source));File.Copy(source,copy,false);Assert.Equal(Hash(source),Hash(copy));}
                var copyPath=Path.Combine(contracts,"SourceContractMetadata.schema.json");var schema=JsonNode.Parse(await File.ReadAllTextAsync(copyPath))!.AsObject();
                schema["$defs"]!["MetaEnvelope"]!["required"]=new JsonArray("schema_version","schema_version");
                await File.WriteAllBytesAsync(copyPath,canonical.Serialize(Element(schema)));var invalid=!CatalogSourceMetadata.IsValidSchema(Element(schema));
                Assert.True(invalid,"The shared draft validator accepted the deliberately duplicated required keyword entries.");
                var altered=new CatalogSourceMetadata(copyPath,canonical);var error=Failure(()=>altered.Validate(Doc(),"Envelope"),"schema");Assert.IsType<FormatException>(error);
                using var build=new NativeCatalogBuildProvider();CatalogBuildCases.AssertCase(new("schema-helper-original-build",new(),CatalogBuildCases.Base),build);
                var report=Path.Combine(owned,"invalid-schema-report.json");var validator=new CatalogValidator(owned,canonical);
                var releaseError=await Record.ExceptionAsync(()=>validator.ValidateAsync(Path.Combine(build.EvidenceRoot,"out"),Path.Combine(build.EvidenceRoot,"inventory.json"),report));
                Assert.IsType<InvalidDataException>(releaseError);Assert.Contains("Invalid JSON schema",releaseError!.Message,StringComparison.Ordinal);
                result=Reply(("schema_invalid",invalid),("composer_rejected_before_instance",error.Message.Contains("schema",StringComparison.OrdinalIgnoreCase)),("release_validator_rejected",true),("existing_report_absent",!File.Exists(report)));break;
            }
            case "import-replay":
            {
                var first=Import(Doc());var replay=Import(Doc(),"ignored replay");
                result=Reply(("revision",replay.Revision),("generation",store.Generation),("history",History(replay)),("bases",Count("metadata_bases")),("heads",Count("metadata_heads")),("etag_unchanged",first.ETag==replay.ETag),("reason",replay.Reason));break;
            }
            case "import-clean-next":
            {
                Import(Doc());var next=Doc();next["title"]="Next source";var row=Import(next,"next base");
                result=Reply(("revision",row.Revision),("generation",store.Generation),("history",History(row)),("bases",Count("metadata_bases")),("heads",Count("metadata_heads")),("origin",row.Origin),("state",row.State),("title",Parsed(row)["title"]!.GetValue<string>()));break;
            }
            case "local-import":
            {
                var local=Doc();local["title"]="Local";Create(local);var row=Import(Doc());
                result=Reply(("revision",row.Revision),("generation",store.Generation),("history",History(row)),("bases",Count("metadata_bases")),("origin",row.Origin),("state",row.State),("title",Parsed(row)["title"]!.GetValue<string>()));break;
            }
            case "overlay-replay":
            {
                var first=Import(Doc());var local=Doc();local["title"]="Local";var edit=store.Put(kind,first.Key,Element(local),first.ETag,"local edit",true);
                var row=Import(Doc(),"ignored replay");result=Reply(("revision",row.Revision),("generation",store.Generation),("history",History(row)),("bases",Count("metadata_bases")),("etag_unchanged",row.ETag==edit.ETag),("origin",row.Origin),("title",Parsed(row)["title"]!.GetValue<string>()),("reason",row.Reason));break;
            }
            case "import-restore":
            {
                var first=Import(Doc());var suppressed=store.Delete(kind,first.Key,first.ETag,"suppression reason",true);var restored=Doc();restored["title"]="Restored";
                var row=store.Put(kind,first.Key,Element(restored),suppressed.ETag,"restore reason",true);
                result=Reply(("revision",row.Revision),("generation",store.Generation),("history",History(row)),("bases",Count("metadata_bases")),("origin",row.Origin),("state",row.State),("title",Parsed(row)["title"]!.GetValue<string>()));break;
            }
            case "history-reasons":
            {
                var first=Create(Doc());var next=Doc();next["description"]="second body";var second=store.Put(kind,first.Key,Element(next),first.ETag,"second reason",true);var third=store.Delete(kind,first.Key,second.ETag,"third reason",true);
                var expected=new[]{first,second,third};var before=State();var history=store.History(kind,first.Key);
                var columns=history.Zip(expected).All(pair=>pair.First.Kind==pair.Second.Kind&&pair.First.Key==pair.Second.Key&&pair.First.Revision==pair.Second.Revision&&pair.First.Origin==pair.Second.Origin&&pair.First.State==pair.Second.State&&pair.First.BaseHash==pair.Second.BaseHash&&pair.First.ContentHash==pair.Second.ContentHash&&pair.First.Reason==pair.Second.Reason&&pair.First.Document==pair.Second.Document);
                var documents=history.Count==3&&history[0].Document==Encoding.UTF8.GetString(canonical.Serialize(Element(call.Document)))&&history[1].Document==Encoding.UTF8.GetString(canonical.Serialize(Element(next)))&&history[2].Document==history[1].Document;
                result=Reply(("reasons",history.Select(row=>row.Reason).ToArray()),("revisions",history.Select(row=>row.Revision).ToArray()),("all_columns_exact",columns),("documents_exact",documents),("history_readonly",before==State()));break;
            }
            case "dependency-dag":
            {
                JsonObject Group(string id,params MetadataSnapshot[] dependencies)
                {var doc=Doc();doc["group_id"]=id;doc["dependencies"]=new JsonArray(dependencies.Select(row=>(JsonNode?)new JsonObject{["group_id"]=row.Key,["revision"]=row.Revision,["content_hash"]=row.ContentHash}).ToArray());return doc;}
                var basis=Create(Group("fg:base"));var left=Create(Group("fg:left",basis));var right=Create(Group("fg:right",basis));var selected=Create(Group("fg:root",left,right));
                result=Reply(("accepted",true),("heads",Count("metadata_heads")),("revisions",Count("metadata_revisions")),("generation",store.Generation),("root_state",selected.State));break;
            }
            case "dependency-revision":case "dependency-hash":
            {
                var target=Doc();target["group_id"]="fg:target";var first=Create(target);
                if(call.Variant=="dependency-revision")store.Put(kind,first.Key,Element(target),first.ETag,"same content new revision",true);
                var parent=Doc();parent["dependencies"]=new JsonArray(new JsonObject{["group_id"]=first.Key,["revision"]=first.Revision,["content_hash"]=call.Variant=="dependency-hash"?new string('0',64):first.ContentHash});
                var before=State();var error=Record.Exception(()=>Create(parent));var status=Assert.IsType<CatalogException>(error).Status;
                result=Reply(("status",status),("heads",Count("metadata_heads")),("revisions",Count("metadata_revisions")),("generation",store.Generation),("rejected_without_mutation",State()==before));break;
            }
            case "functional-identity":
            {
                var value=metadata.Produce(Doc());value["functional_facts"]!["entry_id"]=new string('f',64);var schema=Schema(value);SemanticReject(value,"Functional identity differs");
                var candidate=metadata.Produce(Doc());candidate["callback_facts"]!["type_disposition"]!["role"]="unknown";candidate["callback_facts"]!["type_disposition"]!["is_candidate"]=true;
                var candidateSchema=Schema(candidate);var rejected=SemanticReject(candidate,"candidate flag");
                result=Reply(("schema_precondition",schema),("error_contains","Functional identity differs"),("candidate_schema_precondition",candidateSchema),("candidate_flag_rejected",rejected),("candidate_error_contains","candidate flag"));break;
            }
            case "endpoint-identity":case "parameter-identity":
            {
                var value=metadata.Produce(Doc());var endpoint=call.Variant=="endpoint-identity";var fragment=endpoint?"Endpoint identity differs":"Parameter binding identity differs";
                if(endpoint)value["callback_facts"]!["endpoint_method"]!["entry_id"]=new string('f',64);else value["callback_facts"]!["parameter_candidates"]![0]!["binding_id"]=new string('f',64);
                var schema=Schema(value);SemanticReject(value,fragment);result=Reply(("schema_precondition",schema),("error_contains",fragment));break;
            }
            case "interface-revision":
            {
                var fact=runtime.Concerns.Applicability.Produce(CatalogAggregateMetadataCases.Source(),CatalogAggregateMetadataCases.InterfaceLinkage());var value=metadata.Produce(Doc(),new(InterfaceEvidence:new JsonArray(fact)));
                value["interface_evidence"]![0]!["module_candidates"]![0]!["source"]!["revision"]="r2";var schema=Schema(value);SemanticReject(value,"Cross-repository/revision source evidence");
                result=Reply(("schema_precondition",schema),("error_contains","Cross-repository/revision source evidence"));break;
            }
            case "hidl-guards":
            {
                var source=CatalogAggregateMetadataCases.Source();source["path"]="IFoo.hal";var hashSource=source.DeepClone().AsObject();hashSource["path"]="current.txt";
                var constraint=new JsonObject{["source"]=source.DeepClone(),["declared_descriptor"]="p@1.0::IFoo",["constraint"]=new JsonObject
                {
                    ["contract"]="exact-hidl-source-constraint/1",["clause"]=new JsonObject{["interface"]=new JsonObject{["descriptor"]="p@1.0::IFoo",["minimum_version"]=null,["accepted_hashes"]=new JsonArray(source["sha256"]!.DeepClone())}},
                    ["source_evidence"]=new JsonArray(source.DeepClone(),hashSource),["published_lines"]=new JsonArray(1),["source_bytes_hash_equals_published_identifier_hash"]=true,
                    ["runtime_observation"]=false,["android_platform_range"]=null,["wire_codec_proven"]=false,["os_or_client_authority_granted"]=false,["normative_reference"]="https://source.android.com/docs/core/architecture/hidl/hashing"
                }};
                var value=metadata.Produce(Doc(),new(HidlConstraints:new JsonArray(constraint.DeepClone())));var positiveSchema=Schema(value);
                var descriptor=value.DeepClone().AsObject();descriptor["hidl_source_constraints"]![0]!["declared_descriptor"]="other@1.0::IFoo";var descriptorSchema=Schema(descriptor);var descriptorRejected=SemanticReject(descriptor,"descriptor/source bytes mismatch");
                var hash=value.DeepClone().AsObject();hash["hidl_source_constraints"]![0]!["constraint"]!["clause"]!["interface"]!["accepted_hashes"]![0]=new string('c',64);var hashSchema=Schema(hash);var hashRejected=SemanticReject(hash,"descriptor/source bytes mismatch");
                var scope=value.DeepClone().AsObject();scope["hidl_source_constraints"]![0]!["constraint"]!["source_evidence"]![1]!["revision"]="r2";var scopeSchema=Schema(scope);var scopeRejected=SemanticReject(scope,"scope mismatch");
                var empty=value.DeepClone().AsObject();empty["hidl_source_constraints"]![0]!["constraint"]!["source_evidence"]=new JsonArray();var invalid=Record.Exception(()=>Schema(empty)) is FormatException;var emptyRejected=Record.Exception(()=>metadata.ValidateMetadata(empty)) is FormatException;
                result=Reply(("positive_schema_precondition",positiveSchema),("positive_preserved",JsonNode.DeepEquals(value["hidl_source_constraints"]![0],constraint)),("descriptor_schema_precondition",descriptorSchema),("descriptor_rejected",descriptorRejected),("hash_schema_precondition",hashSchema),("hash_rejected",hashRejected),("scope_schema_precondition",scopeSchema),("scope_rejected",scopeRejected),("empty_evidence_schema_invalid",invalid),("empty_evidence_rejected",emptyRejected),("no_runtime_authority",!value["runtime_observation"]!.GetValue<bool>()&&!value["wire_codec_proven"]!.GetValue<bool>()&&!value["authority_granted"]!.GetValue<bool>()));break;
            }
            case "materialization":
            {
                var strategies=new Dictionary<string,string>(StringComparer.Ordinal);foreach(var shape in new[]{"abstract-class","class","nonfinal-class","unknown","java-interface","aidl-interface"})strategies.Add(shape,metadata.Materialization(shape)["strategy"]!.GetValue<string>());
                var adapter=new JsonObject{["adapter_id"]="reviewed-fixture",["adapter_version"]="1",["adapter_sha256"]=new string('a',64)};var before=adapter.DeepClone();var selected=metadata.Materialization("abstract-class",adapter);adapter["adapter_id"]="changed after call";
                bool Bad(Action<JsonObject> change){var value=before.DeepClone().AsObject();change(value);return Record.Exception(()=>metadata.Materialization("abstract-class",value)) is FormatException;}
                result=Reply(("strategies",strategies),("unknown_shape_rejected",Record.Exception(()=>metadata.Materialization("invented")) is FormatException),("valid_adapter_exact_copy",JsonNode.DeepEquals(before,selected["adapter"])),("missing_pin_rejected",Bad(value=>value.Remove("adapter_id"))),("extra_pin_rejected",Bad(value=>value["extra"]=true)),("empty_pin_rejected",Bad(value=>value["adapter_id"]="")),("nonstring_pin_rejected",Bad(value=>value["adapter_id"]=1)),("short_hash_rejected",Bad(value=>value["adapter_sha256"]=new string('a',63))));break;
            }
            case "occurrence-context":
            {
                var sourceId=new string('b',64);var declaration=Doc();declaration["provenance"]=new JsonArray(new JsonObject{["source_id"]=sourceId});
                CatalogMetadataOccurrence Row(JsonObject doc){var raw=doc.ToJsonString();return new(sourceId,raw,ContentHash.Sha256(Encoding.UTF8.GetBytes(raw)));}
                var row=Row(declaration);var wrong=declaration.DeepClone().AsObject();wrong["entry_id"]=new string('f',64);var binding=Record.Exception(()=>metadata.AttachOccurrences(declaration,[Row(wrong)]));Assert.IsType<FormatException>(binding);Assert.Contains("binding identity",binding!.Message,StringComparison.Ordinal);
                var missing=declaration.DeepClone().AsObject();missing["provenance"]=new JsonArray();var provenance=Record.Exception(()=>metadata.AttachOccurrences(declaration,[Row(missing)]));Assert.IsType<FormatException>(provenance);Assert.Contains("provenance",provenance!.Message,StringComparison.Ordinal);
                var variant=metadata.Produce(declaration,sourceContext:CatalogAggregateMetadataCases.ExactContext());variant["entry_id"]=new string('f',64);variant["functional_facts"]!["entry_id"]=new string('f',64);metadata.ValidateMetadata(variant);
                var set=new JsonObject{["schema_version"]="1.0.0",["entry_id"]=declaration["entry_id"]!.DeepClone(),["variants"]=new JsonArray(variant)};var variantError=Failure(()=>metadata.Attach(declaration,set),"Occurrence identity differs");Assert.IsType<FormatException>(variantError);
                var evidence=metadata.NativeEvidence("record",CatalogAggregateMetadataCases.NativeRecord());var calls=0;var exact=false;
                var attached=metadata.AttachOccurrences(declaration,[row],(id,doc)=>{calls++;exact=id==sourceId&&JsonNode.DeepEquals(doc,declaration);return new(NativeAbiEvidence:new JsonArray(evidence.DeepClone()));});
                var retained=JsonNode.DeepEquals(attached["extensions"]!["source_contract_metadata"]!["variants"]![0]!["native_abi_evidence"]![0],evidence);
                var fallback=metadata.AttachOccurrences(declaration,[])["extensions"]!["source_contract_metadata"]!["variants"]!.AsArray();var envelope=fallback[0]!;
                result=Reply(("binding_mismatch_rejected",true),("provenance_mismatch_rejected",true),("variant_identity_rejected",true),("lookup_exact_source",calls==1&&exact),("lookup_evidence_retained",retained),("fallback_variants",fallback.Count),("fallback_context",envelope["source_context"]!["kind"]!.GetValue<string>()),("no_runtime_authority",!envelope["runtime_observation"]!.GetValue<bool>()&&!envelope["wire_codec_proven"]!.GetValue<bool>()&&!envelope["authority_granted"]!.GetValue<bool>()));break;
            }
            case "missing-entry":
            {
                var probe=new UnexpectedMetadataConcerns();var guarded=new CatalogSourceMetadata(schemaPath,canonical,probe,probe);var doc=Doc();doc.Remove("entry_id");
                Assert.IsType<FormatException>(Failure(()=>guarded.Produce(doc),"Missing Entry identity"));result=Reply(("error_contains","Missing Entry identity"),("concern_calls",probe.Calls));break;
            }
            case "header-domain":
            {
                var row=Create(Doc());var headers=new[]{row.ETag+"\n",row.ETag+"\r\n",row.ETag+",", "W/"+row.ETag,row.ETag+","+row.ETag," "+row.ETag};var before=headers.ToArray();var statuses=new List<int>();
                foreach(var header in headers)statuses.Add(Assert.IsType<CatalogException>(Record.Exception(()=>store.Delete(kind,row.Key,header,"bad header",true))).Status);
                var absent=Assert.IsType<CatalogException>(Record.Exception(()=>store.Delete(kind,"fg:absent",row.ETag+"\n","bad header before lookup",true))).Status;
                result=Reply(("statuses",statuses.ToArray()),("reject_before_lookup",absent==400),("generation",store.Generation),("revisions",Count("metadata_revisions")),("headers_unchanged",headers.SequenceEqual(before,StringComparer.Ordinal)));break;
            }
            default:throw new ArgumentException("Unknown finite metadata closure fixture",nameof(call));
        }
        result["source_schema_unchanged"]=Hash(schemaPath)==schemaHash;return result;
    }
    // This guard spy produces no facts. It fails if an invalid Entry reaches a concern.
    private sealed class UnexpectedMetadataConcerns:ICallbackSourceFacts,IFunctionalTaxonomySourceFacts
    {
        public int Calls {get;private set;}
        private T Unexpected<T>(){Calls++;throw new InvalidOperationException("Invalid Entry reached a concern");}
        public JsonObject TypeCandidate(JsonObject declaration)=>Unexpected<JsonObject>();
        public JsonObject EndpointMethod(JsonObject declaration,JsonObject? owner=null)=>Unexpected<JsonObject>();
        public JsonArray RegistrationCandidates(JsonObject declaration,IReadOnlyDictionary<string,JsonObject> resolvedTypes)=>Unexpected<JsonArray>();
        public JsonArray PossiblePairs(JsonArray candidates)=>Unexpected<JsonArray>();
        public JsonObject Taxonomy(JsonObject declaration)=>Unexpected<JsonObject>();
    }
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogMetadataLeafRealTests
{
    [Theory,MemberData(nameof(CatalogMetadataLeafCases.All),MemberType=typeof(CatalogMetadataLeafCases))]
    public Task SameNineteenWholeGroupClauseFixturesBindTheSharedNativeAuthorities(CatalogMetadataLeafCase test)=>CatalogMetadataLeafCases.AssertCase(test,new NativeCatalogMetadataLeafProvider());
}
