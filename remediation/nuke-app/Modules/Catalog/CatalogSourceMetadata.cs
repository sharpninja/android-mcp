using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Json.Schema;

namespace AndroidMcp.BuildTool.Catalog;

public sealed record CatalogMetadataOccurrence(string SourceId,string DocumentJson,string RecordSha256);
public sealed record CatalogMetadataEvidence(JsonArray? InterfaceEvidence=null,JsonArray? HidlConstraints=null,JsonArray? NativeAbiEvidence=null,
    IReadOnlyDictionary<string,JsonObject>? ResolvedTypes=null,JsonArray? LifecyclePairs=null,JsonArray? AidlConstraints=null);
public sealed record CatalogMetadataGroupPin(int Revision,string ContentHash,JsonObject Document);

/// <summary>One schema/semantic boundary and aggregate composer for source-only metadata.</summary>
public sealed class CatalogSourceMetadata
{
    private readonly JsonObject schema;
    private readonly ICanonicalJson canonical;
    private readonly ICallbackSourceFacts? callbacks;
    private readonly IFunctionalTaxonomySourceFacts? taxonomy;
    private readonly Dictionary<string,JsonSchema> validators=new(StringComparer.Ordinal);
    private static readonly object MetaSchemaEvaluation=new();
    public StoredArtifact AdmittedSchemaPin {get;}
    public CatalogSourceMetadata(string schemaPath,ICanonicalJson canonical,ICallbackSourceFacts? callbacks=null,IFunctionalTaxonomySourceFacts? taxonomy=null)
    {
        var path=Path.GetFullPath(schemaPath);var admitted=CatalogDatabase.ReadContract(Path.GetDirectoryName(path)!,Path.GetFileName(path));
        AdmittedSchemaPin=admitted.Pin with{RelativePath="contracts/SourceContractMetadata.schema.json"};
        schema=JsonNode.Parse(admitted.Text)!.AsObject();this.canonical=canonical;this.callbacks=callbacks;this.taxonomy=taxonomy;
    }
    private static string S(JsonNode? node)=>node?.GetValue<string>() ?? "";
    private static bool? B(JsonNode? node)=>node is JsonValue value && value.TryGetValue<bool>(out var flag)?flag:null;
    private static JsonArray Copy(JsonArray? value)=>(JsonArray?)value?.DeepClone() ?? new();
    private static IEnumerable<JsonObject> Objects(JsonNode? value)=>(value as JsonArray ?? new()).Select(item=>item!.AsObject());
    private static void Reject(bool condition,string message){if(condition)throw new FormatException(message);}
    public string Digest(JsonNode value)=>canonical.Hash(JsonSerializer.SerializeToElement(value));
    public static bool IsValidSchema(JsonElement document)
    {
        lock(MetaSchemaEvaluation)
            return MetaSchemas.Draft202012.Evaluate(document,new(){OutputFormat=OutputFormat.Flag}).IsValid;
    }
    public JsonObject Validate(JsonObject value,string root)
    {
        if(!validators.TryGetValue(root,out var validator))
        {var selected=schema.DeepClone().AsObject();selected["$ref"]="#/$defs/Meta"+root;Reject(!IsValidSchema(JsonSerializer.SerializeToElement(selected)),"Invalid SourceContractMetadata schema");validator=JsonSchema.FromText(selected.ToJsonString());validators.Add(root,validator);}
        Reject(!validator.Evaluate(JsonSerializer.SerializeToElement(value),new(){OutputFormat=OutputFormat.Flag,RequireFormatValidation=true}).IsValid,"Invalid "+root);
        if(root=="EndpointMethod")
        {
            var language=S(value["source_language"]);var own=B(value["explicit_oneway"]);var inherited=B(value["inherited_interface_oneway"]);
            bool? asynchronous=language=="aidl" ? own==true || inherited==true ? true : own==false && inherited==false ? false : null : language=="hidl" ? own : null;
            Reject(S(value["reply_mode"])!=(asynchronous==true?"oneway":asynchronous==false?"synchronous":"unknown"),"Endpoint reply mode contradicts its source-language/oneway evidence");
        }
        return value;
    }
    public JsonObject ValidateMetadata(JsonObject value)
    {
        Validate(value,"Envelope");var facts=value["callback_facts"]!.AsObject();var disposition=facts["type_disposition"]!.AsObject();
        Reject(B(disposition["is_candidate"])!=(S(disposition["role"])=="candidate"),"Callback candidate flag and disposition disagree");
        if(facts["endpoint_method"] is JsonObject endpoint)
        {Validate(endpoint,"EndpointMethod");Reject(S(endpoint["entry_id"])!=S(value["entry_id"]),"Endpoint identity differs from metadata identity");}
        Reject(S(value["functional_facts"]!["entry_id"])!=S(value["entry_id"]),"Functional identity differs from metadata identity");
        foreach(var row in Objects(facts["parameter_candidates"]))Reject(S(row["binding_id"])!=S(value["entry_id"]),"Parameter binding identity differs");
        foreach(var row in Objects(value["interface_evidence"]))
        {
            var source=row["source"]!.AsObject();var evidence=new List<JsonObject>();
            if(row["declared_module"] is JsonObject module)evidence.Add(module["source"]!.AsObject());
            evidence.AddRange(Objects(row["interface_candidates"]).Concat(Objects(row["module_candidates"])).Select(item=>item["source"]!.AsObject()));
            Reject(evidence.Any(item=>new[]{"repository","revision"}.Any(key=>!JsonNode.DeepEquals(item[key],source[key]))),"Cross-repository/revision source evidence");
        }
        foreach(var row in Objects(value["hidl_source_constraints"]))
        {
            var constraint=row["constraint"]!.AsObject();if(constraint.ContainsKey("unknown_reason"))continue;
            var selected=constraint["clause"]!["interface"]!;var source=row["source"]!.AsObject();
            Reject(S(row["declared_descriptor"])!=S(selected["descriptor"]) || !JsonNode.DeepEquals(selected["accepted_hashes"],new JsonArray(source["sha256"]!.DeepClone())),"HIDL descriptor/source bytes mismatch");
            var evidence=Objects(constraint["source_evidence"]).ToArray();
            Reject(evidence.Length==0 || evidence.Any(item=>new[]{"repository","revision"}.Any(key=>!JsonNode.DeepEquals(item[key],source[key]))),"HIDL evidence scope mismatch");
        }
        return value;
    }
    public JsonObject Produce(JsonObject declaration,CatalogMetadataEvidence? evidence=null,JsonObject? sourceContext=null)
    {
        if(callbacks is null || taxonomy is null)throw new InvalidOperationException("Source callback and taxonomy concern producers must be injected before metadata production");
        Reject(declaration["entry_id"] is not JsonValue identity || !identity.TryGetValue<string>(out _),"Missing Entry identity");evidence ??= new();
        var context=sourceContext?.DeepClone().AsObject() ?? new JsonObject
        {
            ["kind"]="aggregate-unresolved",["source_ids"]=new JsonArray(Objects(declaration["provenance"]).Select(row=>S(row["source_id"])).Where(value=>value.Length!=0).Distinct(StringComparer.Ordinal).Order(Comparer<string>.Create(StrictJsonInspector.CompareScalars)).Select(value=>(JsonNode?)JsonValue.Create(value)).ToArray()),
            ["declaration_record_hash"]=null,["unknown_reason"]="Exact binding_declarations occurrence context not supplied; no source-version inheritance/registration inference"
        };
        var disposition=callbacks.TypeCandidate(declaration);var endpoint=S(declaration["kind"]) is "method" or "aidl-member" or "constructor" ? callbacks.EndpointMethod(declaration) : null;
        if(S(context["kind"])=="aggregate-unresolved")
        {disposition["inheritance"]=new JsonArray();if(endpoint is not null){endpoint["explicit_oneway"]=null;endpoint["inherited_interface_oneway"]=null;endpoint["reply_mode"]="unknown";}}
        var value=new JsonObject
        {
            ["schema_version"]="1.0.0",["entry_id"]=declaration["entry_id"]!.DeepClone(),["interface_evidence"]=Copy(evidence.InterfaceEvidence),["hidl_source_constraints"]=Copy(evidence.HidlConstraints),
            ["callback_facts"]=new JsonObject{["type_disposition"]=disposition,["endpoint_method"]=endpoint,["parameter_candidates"]=callbacks.RegistrationCandidates(declaration,evidence.ResolvedTypes ?? new Dictionary<string,JsonObject>()),["lifecycle_pair_candidates"]=Copy(evidence.LifecyclePairs)},
            ["functional_facts"]=taxonomy.Taxonomy(declaration),["native_abi_evidence"]=Copy(evidence.NativeAbiEvidence),["runtime_observation"]=false,["wire_codec_proven"]=false,["authority_granted"]=false,
            ["source_context"]=context,["aidl_source_constraints"]=Copy(evidence.AidlConstraints)
        };
        return ValidateMetadata(value);
    }
    public JsonObject Attach(JsonObject declaration,JsonObject metadata)
    {
        var set=metadata.ContainsKey("variants") ? metadata : new JsonObject{["schema_version"]="1.0.0",["entry_id"]=metadata["entry_id"]?.DeepClone(),["variants"]=new JsonArray(metadata.DeepClone())};
        Validate(set,"Set");Reject(S(declaration["entry_id"])!=S(set["entry_id"]),"Entry identity differs from metadata identity");var contexts=new HashSet<string>(StringComparer.Ordinal);
        foreach(var variant in Objects(set["variants"]))
        {ValidateMetadata(variant);Reject(S(variant["entry_id"])!=S(set["entry_id"]),"Occurrence identity differs from metadata identity");Reject(!contexts.Add(Digest(variant["source_context"]!)),"Duplicate source occurrence context");}
        var result=declaration.DeepClone().AsObject();if(result["extensions"] is null)result["extensions"]=new JsonObject();result["extensions"]!["source_contract_metadata"]=set.DeepClone();return result;
    }
    public JsonObject AttachOccurrences(JsonObject declaration,IEnumerable<CatalogMetadataOccurrence> occurrences,Func<string,JsonObject,CatalogMetadataEvidence>? evidenceLookup=null)
    {
        if(callbacks is null || taxonomy is null)throw new InvalidOperationException("Source callback and taxonomy concern producers must be injected before occurrence attachment");
        var variants=new JsonArray();
        foreach(var row in occurrences)
        {
            var bytes=Encoding.UTF8.GetBytes(row.DocumentJson);Reject(ContentHash.Sha256(bytes)!=row.RecordSha256,"Immutable occurrence record hash mismatch");
            _=canonical.Serialize(bytes);var occurrence=JsonNode.Parse(row.DocumentJson)!.AsObject();
            Reject(S(occurrence["entry_id"])!=S(declaration["entry_id"]),"Occurrence binding identity mismatch");
            Reject(!Objects(occurrence["provenance"]).Any(proof=>S(proof["source_id"])==row.SourceId),"Occurrence source identity absent from provenance");
            variants.Add(Produce(occurrence,evidenceLookup?.Invoke(row.SourceId,occurrence),new JsonObject{["kind"]="exact-occurrence",["source_ids"]=new JsonArray(row.SourceId),["declaration_record_hash"]=row.RecordSha256,["unknown_reason"]=null}));
        }
        if(variants.Count==0)variants.Add(Produce(declaration));
        return Attach(declaration,new JsonObject{["schema_version"]="1.0.0",["entry_id"]=declaration["entry_id"]?.DeepClone(),["variants"]=variants});
    }
    public JsonObject NativeEvidence(string kind,JsonObject document)=>Validate(new JsonObject{["schema_version"]="1.0.0",["kind"]=kind,["confidence"]="structural-only",["runtime_observation"]=false,["wire_codec_proven"]=false,["authority_granted"]=false,["document"]=document.DeepClone()},"NativeEvidence");
    public bool CallbackRegistrationAllowed(JsonObject metadata){ValidateMetadata(metadata);return false;}
    public JsonObject Materialization(string shape,JsonObject? adapter=null)
    {
        if(adapter is not null)
        {
            Reject(adapter.Count!=3 || new[]{"adapter_id","adapter_version","adapter_sha256"}.Any(key=>adapter[key] is not JsonValue value || !value.TryGetValue<string>(out var text) || text.Length==0) || S(adapter["adapter_sha256"]).Length!=64,"Reviewed adapter must have exact id/version/hash pin");
            return new(){["strategy"]="prebuilt_adapter",["adapter"]=adapter.DeepClone()};
        }
        return shape switch
        {
            "abstract-class"=>new(){["strategy"]="unsupported_abstract_class",["unknown_reason"]="ABSTRACT_CLASS_NO_RUNTIME_SUBCLASS"},
            "class" or "nonfinal-class"=>new(){["strategy"]="unsupported_class_subclass",["unknown_reason"]="CLASS_CALLBACK_REQUIRES_REVIEWED_ADAPTER"},
            "unknown"=>new(){["strategy"]="unknown",["unknown_reason"]="Callback materialization shape is unresolved"},
            "java-interface"=>new(){["strategy"]="interface_dynamic_proxy"},"aidl-interface"=>new(){["strategy"]="binder_generic_stub"},
            _=>throw new FormatException("Unknown callback materialization shape; no runtime subclassing inferred")
        };
    }
    public JsonObject ValidateDescriptor(JsonObject value)
    {
        Validate(value,"CallbackDescriptor");var classification=S(value["classification"]);
        Reject((classification is "candidate" or "unknown") && S(value["unknown_reason"]).Length==0,"Unproven callback needs an explicit reason");
        Reject((classification is "proven_callback" or "proven_non_callback") && value["evidence"]!.AsArray().Count==0,"Proven role requires source evidence");
        var methods=Objects(value["methods"]).ToArray();Reject(methods.Select(row=>S(row["method_id"])).Distinct(StringComparer.Ordinal).Count()!=methods.Length,"Duplicate callback method identity");
        var cleanup=value["cleanup"]!;Reject(S(cleanup["status"])=="proven" && (S(cleanup["binding_id"]).Length==0 || S(cleanup["contract_hash"]).Length==0 || cleanup["evidence"]!.AsArray().Count==0),"Proven cleanup requires exact binding/contract/evidence");
        if(S(value["materialization"]!["strategy"])=="prebuilt_adapter")_=Materialization("abstract-class",value["materialization"]!["adapter"]!.AsObject());return value;
    }
    public JsonObject ValidateGroup(JsonObject value,IReadOnlyDictionary<string,CatalogMetadataGroupPin>? knownGroups=null)
    {
        Validate(value,"FunctionalGroup");var members=Objects(value["members"]).ToArray();
        Reject(members.Select(row=>(S(row["binding_id"]),S(row["role"]),S(row["lifecycle_unit_id"]))).Distinct().Count()!=members.Length,"Duplicate group binding role/unit");
        Reject(members.GroupBy(row=>S(row["binding_id"]),StringComparer.Ordinal).Any(group=>group.Select(row=>S(row["contract_hash"])).Distinct(StringComparer.Ordinal).Count()!=1),"One group cannot select incompatible contracts of one binding");
        var units=new Dictionary<string,HashSet<string>>(StringComparer.Ordinal);
        foreach(var row in members.Where(row=>S(row["role"]) is "register" or "cleanup"))
        {var unit=S(row["lifecycle_unit_id"]);Reject(unit.Length==0,"Lifecycle operation requires an atomic unit");if(!units.TryGetValue(unit,out var roles)){roles=new(StringComparer.Ordinal);units.Add(unit,roles);}roles.Add(S(row["role"]));}
        Reject(units.Values.Any(roles=>!roles.SetEquals(["register","cleanup"])),"Registration and cleanup must be one coherent group unit");
        var types=value["required_type_ids"]!.AsArray();Reject(types.Select(S).Distinct(StringComparer.Ordinal).Count()!=types.Count,"Duplicate type dependency");
        var dependencies=Objects(value["dependencies"]).ToArray();Reject(dependencies.Select(row=>S(row["group_id"])).Distinct(StringComparer.Ordinal).Count()!=dependencies.Length || dependencies.Any(row=>S(row["group_id"])==S(value["group_id"])),"Duplicate/self group dependency");
        Reject(!JsonNode.DeepEquals(value["estimated_tokens"],JsonValue.Create(0)) && S(value["estimator_version"]).Length==0,"Nonzero token estimate needs estimator version");
        if(knownGroups is not null)
        {
            var path=new HashSet<string>(StringComparer.Ordinal);var done=new HashSet<string>(StringComparer.Ordinal);
            void Visit(JsonObject group)
            {
                var key=S(group["group_id"]);Reject(path.Contains(key),"Group dependency cycle");if(done.Contains(key))return;path.Add(key);
                foreach(var pin in Objects(group["dependencies"]))
                {Reject(!knownGroups.ContainsKey(S(pin["group_id"])),"Unknown group dependency");var target=knownGroups[S(pin["group_id"])];Reject(target.Revision!=pin["revision"]!.GetValue<int>() || target.ContentHash!=S(pin["content_hash"]),"Group dependency revision/hash mismatch");Visit(target.Document);}
                path.Remove(key);done.Add(key);
            }
            Visit(value);
        }
        return value;
    }
}
