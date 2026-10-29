using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Json.Schema;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogAggregateMetadataProvider:ICatalogAggregateMetadataCaseProvider
{
    public Task<JsonObject> ExecuteAsync(CatalogAggregateMetadataCall call)=>Task.FromResult(Execute(call));
    private static JsonObject Execute(CatalogAggregateMetadataCall call)
    {
        using var runtime=new SourcesRuntimeFactory().Create(SourceExtractionProfile.CurrentCandidate);var canonical=new CanonicalJson();
        var root=NativeSchemaProvider.Root();var schemaPath=Path.Combine(root,"contracts/SourceContractMetadata.schema.json");
        var service=new CatalogSourceMetadata(schemaPath,canonical,runtime.Concerns.Callbacks,runtime.Concerns.Taxonomy);var doc=call.Declaration;
        bool Rejected(Action action,string? text=null){var error=Record.Exception(action);if(text is not null)Assert.Contains(text,error?.Message ?? "",StringComparison.OrdinalIgnoreCase);return error is FormatException;}
        switch(call.Variant)
        {
            case "schema":return new(){["schema_exists"]=File.Exists(schemaPath)};
            case "unclassified":{var value=service.Produce(doc);return new(){["role"]=value["callback_facts"]!["type_disposition"]!["role"]!.DeepClone(),["classification"]=value["functional_facts"]!["classification"]!.DeepClone()};}
            case "candidate":{var value=service.Produce(doc);return new(){["role"]=value["callback_facts"]!["type_disposition"]!["role"]!.DeepClone(),["registration_allowed"]=service.CallbackRegistrationAllowed(value)};}
            case "interface-evidence":
            {
                var fact=runtime.Concerns.Applicability.Produce(CatalogAggregateMetadataCases.Source(),CatalogAggregateMetadataCases.InterfaceLinkage());
                var value=service.Produce(doc,new(InterfaceEvidence:new JsonArray(fact.DeepClone())));
                return new(){["interface_evidence_exact"]=JsonNode.DeepEquals(value["interface_evidence"]![0],fact),["version_range"]=fact["module_candidates"]![0]!["version_range"]?.DeepClone()};
            }
            case "runtime-claim":{var value=service.Produce(doc);value["runtime_observation"]=true;return new(){["rejected"]=Rejected(()=>service.ValidateMetadata(value))};}
            case "identity":{var value=service.Produce(doc);value["entry_id"]=new string('f',64);return new(){["identity_rejected"]=Rejected(()=>service.Attach(doc,value),"identity")};}
            case "attach":
            {
                var before=doc.DeepClone();var value=service.Produce(doc);var result=service.Attach(doc,value);
                return new(){["input_equal"]=JsonNode.DeepEquals(doc,before),["single_canonical_extension"]=result["extensions"]!.AsObject().Count==before["extensions"]!.AsObject().Count+1 && result["extensions"]!.AsObject().ContainsKey("source_contract_metadata"),
                    ["variants_exact"]=JsonNode.DeepEquals(result["extensions"]!["source_contract_metadata"]!["variants"],new JsonArray(value.DeepClone()))};
            }
            case "oneway":return new(){["aggregate_reply_mode"]=service.Produce(doc)["callback_facts"]!["endpoint_method"]!["reply_mode"]!.DeepClone(),["exact_reply_mode"]=service.Produce(doc,sourceContext:CatalogAggregateMetadataCases.ExactContext())["callback_facts"]!["endpoint_method"]!["reply_mode"]!.DeepClone()};
            case "duplicate-context":{var value=service.Produce(doc);return new(){["duplicate_rejected"]=Rejected(()=>service.Attach(doc,new(){["schema_version"]="1.0.0",["entry_id"]=doc["entry_id"]!.DeepClone(),["variants"]=new JsonArray(value.DeepClone(),value.DeepClone())}),"Duplicate")};}
            case "unknown-field":{var value=service.Produce(doc);value["grant_all"]=true;return new(){["unknown_field_rejected"]=Rejected(()=>service.ValidateMetadata(value))};}
            case "native-record":
            {
                var record=CatalogAggregateMetadataCases.NativeRecord();var value=service.NativeEvidence("record",record);record["wire_codec_proven"]=true;
                return new(){["confidence"]=value["confidence"]!.DeepClone(),["runtime_observation"]=value["runtime_observation"]!.DeepClone(),["wire_claim_rejected"]=Rejected(()=>service.NativeEvidence("record",record))};
            }
            case "abstract-materialization":
            {
                var value=service.Materialization("abstract-class");return new(){["materialization_strategy"]=value["strategy"]!.DeepClone(),["unknown_reason"]=value["unknown_reason"]!.DeepClone(),["unpinned_adapter_rejected"]=Rejected(()=>service.Materialization("abstract-class",new(){["adapter_id"]="fake"}))};
            }
            case "hidl-constraint":
            {
                var value=CatalogAggregateMetadataCases.HidlConstraint();service.Validate(value,"HidlConstraint");value["constraint"]!["clause"]=new JsonObject{["interface"]=new JsonObject{["descriptor"]="p@1.0::IFoo"}};
                return new(){["unknown_constraint_valid"]=true,["unknown_plus_positive_rejected"]=Rejected(()=>service.Validate(value,"HidlConstraint"))};
            }
            case "null-direction":{var row=service.Produce(doc)["callback_facts"]!["parameter_candidates"]![0]!;return new(){["direction"]=row["direction"]?.DeepClone(),["registration_guaranteed"]=row["registration_guaranteed"]!.DeepClone()};}
            case "reply-mode":{var value=runtime.Concerns.Callbacks.EndpointMethod(doc);value["reply_mode"]="synchronous";return new(){["invented_synchronous_rejected"]=Rejected(()=>service.Validate(value,"EndpointMethod"),"reply mode")};}
            case "native-descriptor":
            {
                var row=CatalogAggregateMetadataCases.NativeDescriptor();var value=service.NativeEvidence("descriptor-candidate",row);var unchanged=JsonNode.DeepEquals(value["document"],row);row["fact"]!["compiled_endpoint_proven"]=true;
                return new(){["document_exact"]=unchanged,["compiled_endpoint_claim_rejected"]=Rejected(()=>service.NativeEvidence("descriptor-candidate",row))};
            }
            case "occurrences":
            {
                using var build=new NativeCatalogBuildProvider();Assert.Equal("ok",build.Build(new()).Outcome);
                using var database=CatalogDatabase.OpenExisting(Path.Combine(build.EvidenceRoot,"out/catalog.sqlite"));
                string id,raw;using(var command=database.Command("SELECT entry_id,document_json FROM effective_entries ORDER BY entry_id LIMIT 1"))using(var rows=command.ExecuteReader()){Assert.True(rows.Read());id=rows.GetString(0);raw=rows.GetString(1);}
                var occurrences=new List<CatalogMetadataOccurrence>();using(var command=database.Command("SELECT source_id,document_json,record_sha256 FROM binding_declarations WHERE binding_id=$id ORDER BY source_id,line",null,("$id",id)))using(var rows=command.ExecuteReader())while(rows.Read())occurrences.Add(new(rows.GetString(0),rows.GetString(1),rows.GetString(2)));
                var entry=JsonNode.Parse(raw)!.AsObject();var output=service.AttachOccurrences(entry,occurrences);var unextended=entry.DeepClone().AsObject();unextended["extensions"]!.AsObject().Remove("source_contract_metadata");
                var entrySchema=JsonSchema.FromText(File.ReadAllText(Path.Combine(root,"contracts/Entry.schema.json")));var broken=occurrences.ToArray();broken[0]=broken[0] with{DocumentJson=broken[0].DocumentJson+" "};
                return new(){["entry_schema_valid"]=entrySchema.Evaluate(JsonSerializer.SerializeToElement(output),new(){OutputFormat=OutputFormat.Flag}).IsValid,
                    ["variant_count_matches_occurrences"]=output["extensions"]!["source_contract_metadata"]!["variants"]!.AsArray().Count==occurrences.Count,
                    ["hash_changed"]=service.Digest(unextended)!=service.Digest(output),["idempotent"]=JsonNode.DeepEquals(service.AttachOccurrences(output,occurrences),output),["raw_whitespace_tamper_rejected"]=Rejected(()=>service.AttachOccurrences(entry,broken),"hash")};
            }
            default:throw new ArgumentException("Unknown original aggregate fixture",nameof(call));
        }
    }
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogAggregateMetadataRealTests
{
    [Theory,MemberData(nameof(CatalogAggregateMetadataCases.All),MemberType=typeof(CatalogAggregateMetadataCases))]
    public Task SameSeventeenOriginalAggregateAssertions(CatalogAggregateMetadataCase test)=>CatalogAggregateMetadataCases.AssertCase(test,new NativeCatalogAggregateMetadataProvider());
}
