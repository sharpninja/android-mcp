using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;

namespace AndroidMcp.BuildTool.Catalog;

public sealed record CatalogDeclaration(string SourceId,int? Line,JsonObject Document);
/// <summary>Snapshot-scoped declaration projection. Source lexical facts are supplied once by Sources.</summary>
public sealed class CatalogProjection
{
    private readonly IReadOnlyDictionary<string,JsonObject> sources;
    private readonly IAnnotationTargetSourceFacts formalFacts;
    private readonly IStructuralTypeHeaderSourceFacts headerFacts;
    private readonly CatalogContracts contracts;
    private readonly ICanonicalJson canonical;
    private readonly CatalogLifecycle lifecycle;
    private readonly Dictionary<string,string> snapshots=new(StringComparer.Ordinal);
    private readonly Dictionary<(string Snapshot,string Language,string Owner),HashSet<string>> index=new();
    private readonly Dictionary<(string Source,string Owner),List<CatalogDeclaration>> fields=new();
    private readonly Dictionary<(string Source,string Owner),HashSet<string>> formals=new();
    private readonly HashSet<(string Source,string Owner)> malformedFormals=[];
    private readonly Dictionary<(string Snapshot,string Entry),HashSet<string>> typeHashes=new();
    private static readonly IReadOnlySet<string> Primitives=new HashSet<string>(["boolean","byte","char","short","int","long","float","double","void"],StringComparer.Ordinal);
    private static string? String(JsonNode? value)=>value is null ? null : value.GetValue<string>();
    private static JsonObject Extra(JsonObject value)=>value["extensions"]!.AsObject();
    private static string Owner(JsonObject value)=>String(value["owner"]) ?? "";
    private string Text(JsonNode value)=>Encoding.UTF8.GetString(canonical.Serialize(JsonSerializer.SerializeToElement(value)));
    private static string Raw(JsonObject document)=>document["provenance"]![0]!["raw_signature"]!.GetValue<string>();
    private static HashSet<string> Set<TKey>(Dictionary<TKey,HashSet<string>> values,TKey key) where TKey:notnull
    {if(!values.TryGetValue(key,out var result)){result=new(StringComparer.Ordinal);values.Add(key,result);}return result;}
    public CatalogProjection(IReadOnlyDictionary<string,JsonObject> sources,IReadOnlyList<CatalogDeclaration> declarations,
        IAnnotationTargetSourceFacts formalFacts,IStructuralTypeHeaderSourceFacts headerFacts,CatalogContracts contracts,ICanonicalJson canonical,CatalogLifecycle? lifecycle=null)
        :this(sources,()=>declarations,formalFacts,headerFacts,contracts,canonical,lifecycle) { }
    public CatalogProjection(IReadOnlyDictionary<string,JsonObject> sources,Func<IEnumerable<CatalogDeclaration>> declarations,
        IAnnotationTargetSourceFacts formalFacts,IStructuralTypeHeaderSourceFacts headerFacts,CatalogContracts contracts,ICanonicalJson canonical,CatalogLifecycle? lifecycle=null)
    {
        this.sources=sources.ToDictionary(item=>item.Key,item=>(JsonObject)item.Value.DeepClone(),StringComparer.Ordinal);
        this.formalFacts=formalFacts;this.headerFacts=headerFacts;this.contracts=contracts;this.canonical=canonical;this.lifecycle=lifecycle ?? new();
        foreach(var (id,source) in sources)
        {
            var key=!string.IsNullOrEmpty(String(source["repository"])) && !string.IsNullOrEmpty(String(source["revision"]))
                ? new JsonArray(source["repository"]?.DeepClone(),source["revision"]?.DeepClone(),source["platform_api_version"]?.DeepClone(),source["api_level"]?.DeepClone(),source["channel"]?.DeepClone())
                : new JsonArray(id);
            snapshots.Add(id,canonical.Hash(JsonSerializer.SerializeToElement(key)));
        }
        var types=()=>declarations().Where(row=>CatalogContracts.IsTypeKind(String(row.Document["kind"]) ?? ""));
        foreach(var row in types())
        {
            var doc=row.Document;var owner=Owner(doc);var key=(row.SourceId,owner);
            try{Set(formals,key).UnionWith(formalFacts.FormalNames(Raw(doc)));}
            catch(FormatException){malformedFormals.Add(key);}
            var qualified=owner.Length!=0 ? owner : String(doc["canonical_signature"])!.Split(':',2)[1].Replace('/','.').Replace('$','.');
            Set(index,(snapshots[row.SourceId],String(Extra(doc)["language"])!,qualified)).Add(String(doc["entry_id"])!);
        }
        foreach(var row in declarations())
        {
            var kind=String(row.Document["kind"]);var language=String(Extra(row.Document)["language"]);
            if(kind is not ("field" or "enum-constant") || language is not ("aidl" or "hidl")) continue;
            var key=(row.SourceId,Owner(row.Document));if(!fields.TryGetValue(key,out var list)){list=[];fields.Add(key,list);}
            list.Add(row with{Document=(JsonObject)row.Document.DeepClone()});
        }
        foreach(var row in types())
            Set(typeHashes,(snapshots[row.SourceId],String(row.Document["entry_id"])!)).Add(contracts.Digest(JsonSerializer.SerializeToElement(Project(row.Document,row.SourceId))));
    }
    public JsonObject TypeReference(JsonObject value,JsonObject document,string sourceId)
    {
        var result=(JsonObject)value.DeepClone();var text=String(value["text"])!;var name=Regex.Replace(text,"(?:\\[\\])+$","");
        JsonObject Unknown(string reason){result["status"]="unknown";result["reason"]=reason;return result;}
        if(Primitives.Contains(name)){result["status"]="primitive";result["reason"]="Intrinsic language type; this does not prove a wire codec";return result;}
        if(!Regex.IsMatch(name,"^[A-Za-z_$][\\w.$]*$",RegexOptions.CultureInvariant)) return Unknown("Generic/wildcard or unrecognized type schema requires source proof");
        var extension=Extra(document);var language=String(extension["language"])!;var owner=Owner(document);var package=String(document["package"]) ?? "";
        HashSet<string> known;
        try{known=new(formalFacts.FormalNames(Raw(document)),StringComparer.Ordinal);}
        catch(FormatException){return Unknown("Malformed formal/header evidence retained; no type instantiation inferred");}
        var parent=owner;
        while(parent.Length!=0 && parent!=package)
        {
            if(malformedFormals.Contains((sourceId,parent))) return Unknown("Malformed containing formal/header evidence retained");
            if(formals.TryGetValue((sourceId,parent),out var names)) known.UnionWith(names);
            var dot=parent.LastIndexOf('.');parent=dot<0 ? "" : parent[..dot];
        }
        if(known.Contains(name.Split('.',2)[0])) return Unknown("Formal type variable requires instantiation proof");
        var groups=new List<string[]>();
        if(name.Contains('.')) groups.Add([name.Replace('$','.')]);
        else
        {
            parent=owner;
            while(parent.Length!=0 && parent!=package)
            {groups.Add([parent+"."+name]);var dot=parent.LastIndexOf('.');parent=dot<0 ? "" : parent[..dot];}
            var imported=(extension["imports"] as JsonArray ?? []).Select(item=>item!.GetValue<string>()).Where(item=>item.Split('.')[^1]==name).ToArray();
            if(imported.Length!=0) groups.Add(imported);groups.Add([(package.Length==0 ? "" : package+".")+name]);
            if(String(sources[sourceId]["kind"]) is "sdk-signature" or "source-signature") groups.Add(["java.lang."+name]);
        }
        foreach(var group in groups)
        {
            var matches=new HashSet<string>(StringComparer.Ordinal);
            foreach(var candidate in group) if(index.TryGetValue((snapshots[sourceId],language,candidate),out var found)) matches.UnionWith(found);
            if(matches.Count>1) return Unknown("Ambiguous same-snapshot declared type");
            if(matches.Count!=1) continue;
            result["status"]="resolved";result["resolved_binding_id"]=matches.Single();result["reason"]="Unique declared type in exact repository/revision/platform snapshot";
            var source=sources[sourceId];result["x-resolution-source-snapshot"]=!string.IsNullOrEmpty(String(source["repository"])) && !string.IsNullOrEmpty(String(source["revision"]))
                ? new JsonArray(source["repository"]?.DeepClone(),source["revision"]?.DeepClone(),source["platform_api_version"]?.DeepClone(),source["api_level"]?.DeepClone(),source["channel"]?.DeepClone()) : new JsonArray(sourceId);
            return result;
        }
        return Unknown("No proven declared type in the same snapshot");
    }
    private static (int Rank,long Position) SourceOrder(CatalogDeclaration declaration)
    {
        var doc=declaration.Document;var extension=Extra(doc);
        if(Integer(extension["declared_ordinal"],out var number)) return(0,number);
        if(extension["source_character_span"] is JsonArray span && span.Count!=0 && Integer(span[0],out number)) return(1,number);
        if(Integer(doc["provenance"]?[0]?["line"],out number)) return(2,number);
        return(3,0);
    }
    private static bool Integer(JsonNode? node,out long number)
    {
        if(node is JsonValue value)
        {if(value.TryGetValue<long>(out number)) return true;if(value.TryGetValue<int>(out var small)){number=small;return true;}}
        number=0;return false;
    }
    public JsonObject Project(JsonObject document,string sourceId)
    {
        var result=(JsonObject)document.DeepClone();var extension=Extra(result);
        result["arguments"]=new JsonArray((result["arguments"] as JsonArray ?? []).Select(value=>(JsonNode)TypeReference(value!.AsObject(),result,sourceId)).ToArray());
        if(result["return_type"] is JsonObject returned) result["return_type"]=TypeReference(returned,result,sourceId);
        if(!CatalogContracts.IsTypeKind(String(result["kind"]) ?? "")) return result;
        var sourceFields=fields.GetValueOrDefault((sourceId,Owner(result))) ?? [];
        var ordered=sourceFields.OrderBy(SourceOrder).ThenBy(row=>Text(row.Document),Comparer<string>.Create(StrictJsonInspector.CompareScalars)).ToArray();
        var orderKnown=ordered.Select(SourceOrder).Distinct().Count()==ordered.Length && ordered.All(row=>SourceOrder(row).Rank!=3);
        var shapeFields=new JsonArray();
        for(var position=0;position<ordered.Length;position++)
        {
            var field=ordered[position];var row=field.Document;var extra=Extra(row);var constant=extra.ContainsKey("constant_raw") ? extra["constant_raw"] : extra["constant_expression_raw"];
            shapeFields.Add(new JsonObject{["name"]=row["name"]?.DeepClone(),["type"]=row["return_type"] is JsonObject type ? TypeReference(type,row,field.SourceId) : null,
                ["constant"]=constant?.DeepClone(),["declaration_order"]=orderKnown ? position : null,
                ["wire_annotations"]=JsonNode.Parse(contracts.WireAnnotations(JsonSerializer.SerializeToElement(extra["annotations"] as JsonArray ?? [])).GetRawText())});
        }
        var header=headerFacts.StructuralHeader(result);var supers=new JsonArray();
        foreach(var super in header.Supertypes) supers.Add(new JsonObject{["relation"]=super["relation"]?.DeepClone(),["type"]=TypeReference(new(){["text"]=super["text"]?.DeepClone(),["status"]="unknown",["codec_status"]="unknown"},result,sourceId)});
        var shape=new JsonObject{["kind"]=result["kind"]?.DeepClone(),["qualified_name"]=Owner(result).Length!=0 ? Owner(result) : String(result["canonical_signature"])!.Split(':',2)[1],
            ["type_parameters"]=header.TypeParameters?.DeepClone(),["supertypes"]=supers,["wire_annotations"]=JsonNode.Parse(contracts.WireAnnotations(JsonSerializer.SerializeToElement(extension["annotations"] as JsonArray ?? [])).GetRawText()),
            ["fields"]=shapeFields,["field_order_status"]=orderKnown && String(extension["language"]) is "aidl" or "hidl" ? "source-declared" : "not-proven-by-this-source-view"};
        if(String(extension["language"])=="hidl") shape["hidl_traits"]=new JsonObject{["declaration_kind"]=extension["hidl_declaration_kind"]?.DeepClone(),["storage_type_raw"]=extension["storage_type_raw"]?.DeepClone(),["typedef_target_raw"]=extension["target_type_raw"]?.DeepClone()};
        extension["declared_type_shape"]=shape;return result;
    }
    internal static JsonObject Incomplete(JsonObject rule,string reason)
    {
        var result=(JsonObject)rule.DeepClone();
        if(result["alternatives"] is JsonArray alternatives) foreach(var item in alternatives) item!["unknown_reason"]=reason;
        else result["unknown_reason"]=(String(result["unknown_reason"]) ?? "")+"; "+reason;
        return result;
    }
    public JsonObject Entity(string entryId,IReadOnlyList<CatalogDeclaration> projectedRows)
    {
        var variants=new Dictionary<string,JsonObject>(StringComparer.Ordinal);string? kind=null;
        foreach(var row in projectedRows)
        {
            var doc=row.Document;var extension=Extra(doc);kind=contracts.EntityKind(JsonSerializer.SerializeToElement(doc));
            var rule=JsonNode.Parse(lifecycle.Project(JsonSerializer.SerializeToElement(doc),JsonSerializer.SerializeToElement(sources[row.SourceId])).GetRawText())!.AsObject();
            var dependencies=new SortedDictionary<string,string>(StringComparer.Ordinal);var unresolved=new List<string>();
            if(malformedFormals.Contains((row.SourceId,Owner(doc)))) unresolved.Add("Malformed formal/header evidence retained");
            if(String(extension["field_type_status"])=="unknown") unresolved.Add("API index omits the required field type schema");
            var references=(doc["arguments"] as JsonArray ?? []).Select(value=>value!.AsObject()).ToList();
            if(doc["return_type"] is JsonObject returned) references.Add(returned);
            if(CatalogContracts.IsTypeKind(String(doc["kind"]) ?? ""))
            {
                var shape=extension["declared_type_shape"] as JsonObject;
                foreach(var field in new[]{"fields","supertypes"}) foreach(var item in shape?[field] as JsonArray ?? []) if(item?["type"] is JsonObject type) references.Add(type);
                if(shape?["fields"] is JsonArray values && values.Count!=0 && String(shape["field_order_status"])!="source-declared") unresolved.Add("Structured field declaration order is unproven");
            }
            else if(Owner(doc).Length!=0)
            {
                var owners=index.GetValueOrDefault((snapshots[row.SourceId],String(extension["language"])!,Owner(doc)));
                if(owners?.Count==1) references.Add(new(){["text"]=Owner(doc),["status"]="resolved",["resolved_binding_id"]=owners.Single(),["codec_status"]="unknown"});
                else unresolved.Add("Containing type is absent or ambiguous");
            }
            foreach(var reference in references)
            {
                if(String(reference["status"])=="primitive") continue;
                var target=String(reference["resolved_binding_id"]);var hashes=target is null ? null : typeHashes.GetValueOrDefault((snapshots[row.SourceId],target));
                if(String(reference["status"])!="resolved" || hashes?.Count!=1){unresolved.Add(String(reference["reason"]) ?? "Required type schema is ambiguous");continue;}
                dependencies[target!]=hashes.Single();
            }
            if(unresolved.Count!=0) rule=Incomplete(rule,"Required type schema unresolved: "+string.Join("; ",unresolved.Distinct(StringComparer.Ordinal).Order(Comparer<string>.Create(StrictJsonInspector.CompareScalars))));
            var variant=new JsonObject{["schema_hash"]=contracts.Digest(JsonSerializer.SerializeToElement(doc)),["availability"]=rule,
                ["dependencies"]=new JsonArray(dependencies.Select(item=>(JsonNode)new JsonObject{["entry_id"]=item.Key,["schema_hash"]=item.Value}).ToArray())};
            variants[Text(variant)]=variant;
        }
        return new(){["entry_id"]=entryId,["entity_kind"]=kind,["variants"]=new JsonArray(variants.OrderBy(item=>item.Key,Comparer<string>.Create(StrictJsonInspector.CompareScalars)).Select(item=>(JsonNode)item.Value).ToArray())};
    }
}
