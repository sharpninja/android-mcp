using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Microsoft.Data.Sqlite;

namespace AndroidMcp.BuildTool.Catalog;

/// <summary>Scoped database attachment of Source-owned archive/target facts. Never parses XML or infers grants.</summary>
internal sealed class CatalogAnnotationImporter(CatalogDatabase database,SqliteTransaction transaction,IReadOnlyDictionary<string,JsonObject> sources,
    IReadOnlyDictionary<string,Dictionary<string,string?>> currentTargets,IReadOnlyDictionary<string,Dictionary<string,string>> currentKeys,
    IReadOnlyDictionary<string,Dictionary<string,string>> currentOwners,IReadOnlySet<string> indexPlatforms,ICanonicalJson canonical,
    IPermissionExpressionSourceFacts permissions,IAnnotationTargetSourceFacts lexical,IReadOnlyDictionary<string,string> symbols)
{
    private static string S(JsonNode? value)=>value?.GetValue<string>() ?? "";
    private string Text(JsonNode? value)=>Encoding.UTF8.GetString(canonical.Serialize(JsonSerializer.SerializeToElement(value)));
    private static string Identity(string signature)=>ContentHash.Sha256(Encoding.UTF8.GetBytes(signature));
    private IEnumerable<(string Source,JsonObject Document)> Declarations(string binding)
    {
        using var query=database.Command("SELECT source_id,document_json FROM binding_declarations WHERE binding_id=$id ORDER BY source_id,line",transaction,("$id",binding));using var reader=query.ExecuteReader();
        while(reader.Read()) yield return(reader.GetString(0),JsonNode.Parse(reader.GetString(1))!.AsObject());
    }
    private JsonObject? Header(string owner,string source)
    {
        using var query=database.Command("SELECT document_json FROM binding_declarations WHERE binding_id=$id AND source_id=$source ORDER BY line LIMIT 1",transaction,("$id",Identity("java:"+owner)),("$source",source));
        return query.ExecuteScalar() is string raw ? JsonNode.Parse(raw)!.AsObject() : null;
    }
    private static string Raw(JsonObject doc)=>S(doc["provenance"]?[0]?["raw_signature"]);
    private HashSet<string> Formals(JsonObject doc,JsonObject header)=>new(lexical.FormalNames(Raw(doc)).Concat(lexical.FormalNames(Raw(header))),StringComparer.Ordinal);
    private HashSet<string> Eligible(string binding,JsonObject annotationSource)
    {
        var fields=new[]{"repository","revision","platform_api_version"};if(fields.Any(field=>S(annotationSource[field]).Length==0)) return new(StringComparer.Ordinal);
        return Declarations(binding).Select(row=>row.Source).Where(id=>S(sources[id]["kind"])=="sdk-signature" && fields.All(field=>S(sources[id][field])==S(annotationSource[field]))).ToHashSet(StringComparer.Ordinal);
    }
    private bool DeclaredTypeMatches(string binding,JsonObject item,IReadOnlySet<string> eligible)
    {
        if(!S(item["target_canonical"]).Contains('#')) return true;
        var expected=item["declared_type"]?.GetValue<string>();var shortened=item["declared_type_shortened"]?.GetValue<string>();
        var roots=(item["declared_type_java_lang_roots"] as JsonArray ?? []).Select(value=>S(value)).ToHashSet(StringComparer.Ordinal);var evidence=new List<bool>();
        foreach(var row in Declarations(binding))
        {
            if(!eligible.Contains(row.Source)) continue;var actual=row.Document["return_type"]?["text"]?.GetValue<string>();
            if(expected is null){evidence.Add(S(row.Document["kind"])=="constructor" && actual is null);continue;}
            if(actual==expected){evidence.Add(true);continue;}if(actual!=shortened){evidence.Add(false);continue;}
            var header=Header(S(row.Document["owner"]),row.Source);evidence.Add(header is not null && !roots.Overlaps(Formals(row.Document,header)));
        }
        return evidence.Count!=0 && evidence.All(value=>value);
    }
    private bool ShorteningSupported(string binding,JsonObject item,IReadOnlySet<string> eligible)
    {
        var roots=(item["parameter_java_lang_roots"] as JsonArray ?? []).Select(value=>S(value)).ToHashSet(StringComparer.Ordinal);var evidence=new List<bool>();
        foreach(var row in Declarations(binding))
        {
            if(!eligible.Contains(row.Source)) continue;var header=Header(S(row.Document["owner"]),row.Source);if(header is null) return false;
            evidence.Add(!roots.Overlaps(Formals(row.Document,header)));
        }
        return evidence.Count!=0 && evidence.All(value=>value);
    }
    internal (int Annotations,int Mapped) Attach(JsonObject source,JsonObject archive)
    {
        var platform=S(source["platform_api_version"]);if(platform.Length==0) throw new InvalidDataException("Explicit annotation platform required");
        var sid=S(source["source_id"]);var total=0;var mapped=0;
        foreach(var item in archive["items"]!.AsArray().OfType<JsonObject>())
        {
            var target=S(item["target_canonical"]);var matches=new List<(string Canonical,string Binding)>();var matchKind="current-sdk-index-exact";
            if(currentTargets.GetValueOrDefault(platform)?.TryGetValue(target,out var current)==true && current is not null) matches.Add((target,current));
            var package=S(item["package_candidate"]);
            if(package.Length!=0 && sources.Values.Any(value=>S(value["kind"])=="java-source" && S(value["platform_api_version"])==platform))
            {
                var signature="java-source:"+package;using var query=database.Command("SELECT count(*) FROM bindings WHERE binding_id=$id",transaction,("$id",Identity(signature)));
                if((long)query.ExecuteScalar()! !=0) matches.Add((signature,Identity(signature)));
            }
            if(matches.Count==0)
            {
                var key=lexical.XmlJvmKey(S(item["item_name"]),currentOwners.GetValueOrDefault(platform) ?? new Dictionary<string,string>(StringComparer.Ordinal));
                if(key is not null && currentKeys.GetValueOrDefault(platform)?.TryGetValue(key,out var proven)==true){matches.Add(("jvm:"+key,proven));matchKind="current-sdk-index-jvm-erasure-exact";}
            }
            if(matches.Count==0 && !indexPlatforms.Contains(platform))
            {
                matchKind="same-snapshot-sdk-signature";
                foreach(var candidate in new[]{target,S(item["java_lang_shortened_candidate"]),package}.Where(value=>value.Length!=0).Distinct(StringComparer.Ordinal))
                {
                    var binding=Identity("java:"+candidate);var eligible=Eligible(binding,source);
                    if(eligible.Count!=0 && DeclaredTypeMatches(binding,item,eligible) && (candidate==target || package.Length!=0 || ShorteningSupported(binding,item,eligible))) matches.Add((candidate,binding));
                }
            }
            string? bid=null;string? matched=null;
            if(matches.Count==1)
            {
                (matched,bid)=matches[0];if(matched!=target && matchKind!="current-sdk-index-jvm-erasure-exact") matchKind=(matchKind=="same-snapshot-sdk-signature" ? "same-snapshot-sdk-signature-" : "")+(package.Length!=0 ? "package-namespace" : "java-lang-spelling-equivalence");
            }
            else matchKind="unresolved-or-ambiguous";
            var annotations=new JsonArray();
            foreach(var annotation in item["annotations"]!.AsArray().OfType<JsonObject>())
            {
                var evidenceAnnotation=(JsonObject)annotation.DeepClone();evidenceAnnotation.Remove("annotation_ordinal");
                var permission=permissions.Permission(new JsonArray(evidenceAnnotation.DeepClone()),symbols);evidenceAnnotation["permission"]=permission;annotations.Add(evidenceAnnotation.DeepClone());
                var evidence=new JsonObject{["item_name"]=item["item_name"]?.DeepClone(),["archive_member"]=item["archive_member"]?.DeepClone(),["item_ordinal"]=item["item_ordinal"]?.DeepClone(),["annotation"]=evidenceAnnotation,
                    ["target_resolution"]=bid is null ? "unknown" : "exact",["target_match_kind"]=matchKind,["matched_canonical_signature"]=matched,["target_resolution_reason"]=bid is null ? "Target absent or ambiguous in the acquired declaration inventory" : null};
                var locator=S(item["archive_member"])+":"+item["item_ordinal"]!.ToJsonString()+":"+annotation["annotation_ordinal"]!.ToJsonString();
                using var insert=database.Command("INSERT INTO annotation_evidence VALUES($source,$locator,$binding,$scope,$document)",transaction,("$source",sid),("$locator",locator),("$binding",bid),("$scope",S(item["scope"])),("$document",Text(evidence)));insert.ExecuteNonQuery();total++;if(bid is not null) mapped++;
            }
            if(bid is not null && S(item["scope"])=="declaration")
            {
                var permission=permissions.Permission(annotations,symbols);if(S(permission["op"])!="unknown")
                {using var insert=database.Command("INSERT INTO permission_relations(binding_id,source_id,line,annotation,logic,expression_json,raw_annotation) VALUES($binding,$source,$line,'external',$logic,$expression,$raw)",transaction,("$binding",bid),("$source",sid),("$line",item["item_ordinal"]!.GetValue<int>()+1),("$logic",S(permission["op"])),("$expression",Text(permission)),("$raw",Text(annotations)));insert.ExecuteNonQuery();}
            }
        }
        return(total,mapped);
    }
}
