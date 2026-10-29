using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using static AndroidMcp.BuildTool.Sources.Constraints.SourceConstraintValues;

namespace AndroidMcp.BuildTool.Sources.Constraints;

public sealed class HidlSourceConstraints(ILegacyJsonValueEquality equality):IHidlSourceConstraints
{
    private readonly SourceConstraintValues values=new(equality);
    public JsonObject ExactConstraint(JsonObject source,JsonObject record,JsonArray facts)
    {
        if(!values.Is(Get(record,"kind"),"interface"))return Unknown("Published package/type hash does not establish an interface descriptor");
        var descriptor=String(Get(record,"canonical"));
        if(descriptor is null||!HidlName(descriptor))return Unknown("No direct parsed HIDL interface FQName");
        var digest=String(Get(source,"sha256"));
        if(digest is null||digest.Length!=64||digest.Any(value=>value is not(>= '0' and <= '9' or >= 'a' and <= 'f')))return Unknown("No exact SHA256 source bytes");
        var matches=new List<JsonObject>();
        foreach(var fact in facts){
            var item=Object(fact);
            if(!values.Is(Get(item,"format"),"published-hidl-hash-hex64-and-identifier")||!values.Is(Get(item,"declared_identifier"),descriptor)||!values.Is(Get(item,"literal"),digest))continue;
            var provenance=Object(Required(item,"source"));
            if(new[]{"repository","revision"}.All(key=>values.Same(Get(provenance,key),Get(source,key))))matches.Add(item);
        }
        if(matches.Count==0)return Unknown("No same-revision published HIDL interface hash equals these exact source bytes");
        var identities=new JsonArray(Identity(source));
        foreach(var match in matches){var provenance=Required(match,"source");if(!identities.Any(identity=>values.Same(identity,provenance)))identities.Add(provenance?.DeepClone());}
        var lines=new JsonArray();foreach(var match in matches)lines.Add(Required(match,"line")?.DeepClone());
        var output=new JsonObject{["contract"]="exact-hidl-source-constraint/1",["clause"]=Clause(descriptor,JsonValue.Create(digest)),["source_evidence"]=identities,["published_lines"]=lines,["source_bytes_hash_equals_published_identifier_hash"]=true};
        Limits(output);output["normative_reference"]="https://source.android.com/docs/core/architecture/hidl/hashing";return output;
    }
}
