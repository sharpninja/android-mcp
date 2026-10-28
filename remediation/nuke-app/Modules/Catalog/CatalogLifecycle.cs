using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AndroidMcp.BuildTool.Catalog;

/// <summary>Source-declared applicability projection. Unknown evidence is retained without device inference.</summary>
public sealed partial class CatalogLifecycle
{
    private static JsonElement Property(JsonElement value,string name)=>value.ValueKind==JsonValueKind.Object && value.TryGetProperty(name,out var result) ? result.Clone() : JsonSerializer.SerializeToElement((object?)null);
    private static string? String(JsonElement value)=>value.ValueKind==JsonValueKind.String ? value.GetString() : null;
    private static long[] Version(JsonElement value)
    {
        var text=String(value);
        if(text is null || !PlatformVersion().IsMatch(text)) throw new ArgumentException("Unknown platform version syntax");
        var parts=text.Split('.');var result=new long[2];
        for(var index=0;index<parts.Length;index++)
        {
            if(!long.TryParse(parts[index],NumberStyles.None,CultureInfo.InvariantCulture,out var number) || number>int.MaxValue)
                throw new ArgumentException("Platform version out of range");
            result[index]=number;
        }
        return result;
    }
    private static Dictionary<string,object?> Platform(long[] introduced,long[]? removed)=>new(StringComparer.Ordinal){["introduced"]=introduced,["removed"]=removed};
    private static JsonElement Serialize(object value)=>JsonSerializer.SerializeToElement(value);
    public JsonElement Project(JsonElement entry,JsonElement source)
    {
        var extension=entry.GetProperty("extensions");var sourceId=source.GetProperty("source_id").GetString()!;
        try
        {
            if(String(Property(extension,"source_kind"))!="sdk-api-index")
            {
                var platform=Property(source,"platform_api_version");
                if(String(platform) is not { } text || !text.Contains('.')) throw new ArgumentException("Source API major alone does not prove minor applicability");
                return Serialize(new Dictionary<string,object?>(StringComparer.Ordinal){["alternatives"]=new[]{new Dictionary<string,object?>(StringComparer.Ordinal){["platform_point"]=Version(platform)}},["evidence_ids"]=new[]{sourceId}});
            }
            var unknown=String(Property(extension,"lifecycle_unknown_reason"));if(!string.IsNullOrEmpty(unknown)) throw new ArgumentException(unknown);
            var own=Property(extension,"api_lifecycle");var parent=Property(extension,"container_api_lifecycle");
            var attributes=new Dictionary<string,JsonElement>(StringComparer.Ordinal);
            foreach(var values in new[]{parent,own}) if(values.ValueKind==JsonValueKind.Object) foreach(var property in values.EnumerateObject()) attributes[property.Name]=property.Value.Clone();
            var introduced=Version(attributes.TryGetValue("since",out var since) ? since : Serialize("1"));
            long[]? removed=null;
            if(attributes.TryGetValue("removed",out var removedValue) && !string.IsNullOrEmpty(String(removedValue))) removed=Version(removedValue);
            if(removed is not null && (removed[0]<introduced[0] || removed[0]==introduced[0] && removed[1]<=introduced[1])) throw new ArgumentException("Malformed source lifecycle interval");
            var alternatives=new List<Dictionary<string,object?>>();var sdks=Property(extension,"sdk_extensions");
            if(sdks.ValueKind==JsonValueKind.Array && sdks.GetArrayLength()!=0)
            {
                var rows=sdks.EnumerateArray().ToArray();var platformPairs=rows.Where(row=>String(Property(row,"sdk_id"))=="0").ToArray();
                if(platformPairs.Length>1 || platformPairs.Length==1 && !Version(platformPairs[0].GetProperty("minimum_version")).SequenceEqual(introduced) ||
                    platformPairs.Length==0 && own.ValueKind==JsonValueKind.Object && own.TryGetProperty("since",out _))
                    throw new ArgumentException("Inconsistent explicit since/sdk platform evidence");
                foreach(var row in rows)
                {
                    var id=row.GetProperty("sdk_id").GetString()!;
                    if(id=="0") {alternatives.Add(new(StringComparer.Ordinal){["platform"]=Platform(Version(row.GetProperty("minimum_version")),removed)});continue;}
                    var raw=row.GetProperty("minimum_version");var text=raw.ValueKind==JsonValueKind.String ? raw.GetString()! : raw.GetRawText();
                    if(!long.TryParse(text,NumberStyles.Integer,CultureInfo.InvariantCulture,out var minimum)) throw new ArgumentException("Invalid extension minimum");
                    if(minimum>int.MaxValue) throw new ArgumentException("Extension minimum out of range");
                    var clause=new Dictionary<string,object?>(StringComparer.Ordinal){["extensions"]=new Dictionary<string,long>(StringComparer.Ordinal){[id]=minimum}};
                    if(removed is not null) clause["platform"]=Platform([0,0],removed);alternatives.Add(clause);
                }
            }
            else alternatives.Add(new(StringComparer.Ordinal){["platform"]=Platform(introduced,removed)});
            return Serialize(new Dictionary<string,object?>(StringComparer.Ordinal){["alternatives"]=alternatives,["evidence_ids"]=new[]{sourceId}});
        }
        catch(ArgumentException error)
        {
            return Serialize(new Dictionary<string,object?>(StringComparer.Ordinal){["unknown_reason"]=error.Message,
                ["x-raw"]=new Dictionary<string,object?>(StringComparer.Ordinal){["source_id"]=sourceId,["api_lifecycle"]=Property(extension,"api_lifecycle"),["platform_api_version"]=Property(source,"platform_api_version")}});
        }
    }
    [GeneratedRegex("^[0-9]+(?:\\.[0-9]+)?$",RegexOptions.CultureInvariant)]
    private static partial Regex PlatformVersion();
}
