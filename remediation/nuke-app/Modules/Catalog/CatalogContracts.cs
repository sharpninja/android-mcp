using System.Text.Json;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Catalog;

/// <summary>One version3 semantic contract projection for inventory, overlays and offline aliases.</summary>
public sealed class CatalogContracts(ICanonicalJson canonical)
{
    private static readonly IReadOnlySet<string> TypeKinds=new HashSet<string>(["class","interface","enum","annotation","type","aidl-interface","aidl-parcelable","aidl-union","aidl-enum"],StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> OtherKinds=new HashSet<string>(["package","unsupported","enum-constant"],StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> BindingKinds=new HashSet<string>(["constructor","method","field","property","aidl-member"],StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> WireNames=new HashSet<string>(["nullable","Nullable","NonNull","NotNull","utf8InCpp","FixedSize","Backing","VintfStability","JavaOnlyStableParcelable"],StringComparer.Ordinal);
    private static readonly JsonElement Null=JsonSerializer.SerializeToElement((object?)null);
    private static readonly JsonElement Empty=JsonSerializer.SerializeToElement(Array.Empty<object>());
    private static JsonElement Property(JsonElement value,string name,JsonElement? fallback=null)=>value.ValueKind==JsonValueKind.Object && value.TryGetProperty(name,out var item) ? item.Clone() : fallback ?? Null;
    private static bool Truth(JsonElement value)=>value.ValueKind switch
    {JsonValueKind.True=>true,JsonValueKind.False or JsonValueKind.Null or JsonValueKind.Undefined=>false,JsonValueKind.String=>value.GetString()!.Length!=0,JsonValueKind.Array=>value.GetArrayLength()!=0,JsonValueKind.Object=>value.EnumerateObject().Any(),JsonValueKind.Number=>value.GetDouble()!=0,_=>false};
    public static bool IsTypeKind(string kind)=>TypeKinds.Contains(kind);
    public string EntityKind(JsonElement entry)
    {
        _=canonical.Serialize(entry);var kind=Property(entry,"kind").GetString();
        if(kind is null || !TypeKinds.Contains(kind) && !OtherKinds.Contains(kind) && !BindingKinds.Contains(kind)) throw new ArgumentException("Missing or unknown Entry.kind; no invocation kind is inferred");
        var extension=Property(entry,"extensions");
        return TypeKinds.Contains(kind) ? "type" : OtherKinds.Contains(kind) || kind=="field" &&
            (Truth(Property(extension,"aidl_declaration_kind")) || Truth(Property(extension,"enum_constant")) || Property(extension,"language").GetString()=="hidl") ? "other" : "binding";
    }
    private static JsonElement TypeRef(JsonElement value)
    {
        if(value.ValueKind!=JsonValueKind.Object) return value.Clone();
        return JsonSerializer.SerializeToElement(value.EnumerateObject().Where(property=>property.Name is "text" or "status" or "resolved_binding_id" or "codec_status" or "codec_id")
            .ToDictionary(property=>property.Name,property=>property.Value.Clone(),StringComparer.Ordinal));
    }
    public JsonElement WireAnnotations(JsonElement annotations)
    {
        var selected=new List<(string Name,string Arguments,Dictionary<string,JsonElement> Value)>();
        foreach(var value in annotations.EnumerateArray())
        {
            var name=Property(value,"name").GetString();if(string.IsNullOrEmpty(name) || !WireNames.Contains(name.Split('.')[^1])) continue;
            var arguments=Property(value,"arguments",JsonSerializer.SerializeToElement(""));
            selected.Add((name,arguments.GetString() ?? "",new(StringComparer.Ordinal){["name"]=JsonSerializer.SerializeToElement(name),["arguments"]=arguments}));
        }
        selected.Sort((left,right)=>
        {var order=StrictJsonInspector.CompareScalars(left.Name,right.Name);return order!=0 ? order : StrictJsonInspector.CompareScalars(left.Arguments,right.Arguments);});
        return JsonSerializer.SerializeToElement(selected.Select(value=>value.Value).ToArray());
    }
    public JsonElement Project(JsonElement entry)
    {
        _=canonical.Serialize(entry);var extension=Property(entry,"extensions");
        var result=new Dictionary<string,JsonElement>(StringComparer.Ordinal)
        {
            ["contract_format_version"]=JsonSerializer.SerializeToElement(3),["canonical_signature"]=Property(entry,"canonical_signature"),["kind"]=Property(entry,"kind"),
            ["arguments"]=JsonSerializer.SerializeToElement(Property(entry,"arguments",Empty).EnumerateArray().Select(TypeRef).ToArray()),
            ["return_type"]=TypeRef(Property(entry,"return_type")),["codec_status"]=Property(entry,"codec_status"),["codec_identity"]=Property(extension,"codec_identity")
        };
        result["parameter_semantics"]=JsonSerializer.SerializeToElement(Property(extension,"parameters",Empty).EnumerateArray().Select(parameter=>new Dictionary<string,JsonElement>(StringComparer.Ordinal)
        { ["direction"]=Property(parameter,"direction"),["wire_annotations"]=WireAnnotations(Property(parameter,"all_annotations",Property(parameter,"annotations",Empty))) }).ToArray());
        result["wire_annotations"]=WireAnnotations(Property(extension,"annotations",Empty));
        var own=Property(extension,"oneway");var inherited=Property(extension,"interface_oneway");
        result["oneway"]=own.ValueKind==JsonValueKind.True || inherited.ValueKind==JsonValueKind.True ? JsonSerializer.SerializeToElement(true) :
            extension.ValueKind==JsonValueKind.Object && (extension.TryGetProperty("oneway",out _) || extension.TryGetProperty("interface_oneway",out _)) ? JsonSerializer.SerializeToElement(false) : Null;
        result["hidl_results"]=JsonSerializer.SerializeToElement(Property(extension,"results",Empty).EnumerateArray().Select(item=>new Dictionary<string,JsonElement>(StringComparer.Ordinal)
        { ["name"]=Property(item,"name"),["type"]=Property(item,"type"),["wire_annotations"]=WireAnnotations(Property(item,"all_annotations",Property(item,"annotations",Empty))) }).ToArray());
        var shape=Property(extension,"declared_type_shape");
        if(shape.ValueKind!=JsonValueKind.Null)
        {
            var normalized=shape.EnumerateObject().Where(property=>property.Name is not ("header" or "fields" or "supertypes"))
                .ToDictionary(property=>property.Name,property=>property.Value.Clone(),StringComparer.Ordinal);
            foreach(var field in new[]{"fields","supertypes"})
                normalized[field]=JsonSerializer.SerializeToElement(Property(shape,field,Empty).EnumerateArray().Select(value=>
                {
                    var row=value.EnumerateObject().ToDictionary(property=>property.Name,property=>property.Value.Clone(),StringComparer.Ordinal);
                    row["type"]=TypeRef(Property(value,"type"));return row;
                }).ToArray());
            result["declared_type_shape"]=JsonSerializer.SerializeToElement(normalized);
        }
        return JsonSerializer.SerializeToElement(result);
    }
    public string Digest(JsonElement entry)=>canonical.Hash(Project(entry));
}
