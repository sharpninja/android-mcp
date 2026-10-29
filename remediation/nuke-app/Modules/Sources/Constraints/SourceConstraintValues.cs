using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Sources.Constraints;

// Projection and selection over existing loaded JSON. No source/JSON parsing,
// hashing, file access or runtime observation lives in the constraint helpers.
internal sealed class SourceConstraintValues(ILegacyJsonValueEquality equality)
{
    internal static JsonNode? Get(JsonObject value,string key)=>value.TryGetPropertyValue(key,out var result)?result:null;
    internal static JsonNode? Required(JsonObject value,string key)=>value.TryGetPropertyValue(key,out var result)?result:throw new KeyNotFoundException(key);
    internal static JsonObject Object(JsonNode? node)=>node as JsonObject??throw new ArgumentException("Admitted JSON object required");
    internal static JsonArray Array(JsonNode? node)=>node as JsonArray??throw new ArgumentException("Admitted JSON array required");
    internal static JsonObject ObjectOrEmpty(JsonObject value,string key)=>value.TryGetPropertyValue(key,out var node)?Object(node):new();
    internal static JsonArray ArrayOrEmpty(JsonObject value,string key)=>value.TryGetPropertyValue(key,out var node)?Array(node):new();
    internal static string? String(JsonNode? value)=>value is JsonValue scalar&&scalar.TryGetValue<string>(out var text)?text:null;
    internal static string Text(JsonNode? value)=>String(value)??throw new ArgumentException("Admitted JSON string required");
    internal bool Same(JsonNode? left,JsonNode? right)=>equality.Equal(JsonSerializer.SerializeToElement(left),JsonSerializer.SerializeToElement(right));
    internal bool Is(JsonNode? value,string expected)=>Same(value,JsonValue.Create(expected));
    internal bool Truth(JsonNode? value)=>value switch{null=>false,JsonObject obj=>obj.Count>0,JsonArray array=>array.Count>0,_=>String(value) is {} text?text.Length>0:!Same(value,JsonValue.Create(0))};
    internal static JsonObject Identity(JsonObject source)
    {var identity=new JsonObject();foreach(var key in new[]{"repository","revision","path","sha256"})identity[key]=Required(source,key)?.DeepClone();return identity;}
    internal static JsonObject Unknown(string reason)=>new(){["unknown_reason"]=reason};
    internal static JsonObject Clause(string descriptor,JsonNode? digest)=>new(){["interface"]=new JsonObject{["descriptor"]=descriptor,["minimum_version"]=null,["accepted_hashes"]=new JsonArray(digest?.DeepClone())}};
    internal static void Limits(JsonObject result){result["runtime_observation"]=false;result["android_platform_range"]=null;result["wire_codec_proven"]=false;result["os_or_client_authority_granted"]=false;}
    internal static bool Identifier(string text)=>text.Length>0&&(text[0] is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '_')&&text.EnumerateRunes().Skip(1).All(SourceText.OriginalWord);
    internal static bool Qualified(string text)=>text.Split('.').All(Identifier);
    internal static bool HidlName(string text)
    {
        var parts=text.Split('@');if(parts.Length!=2||!Qualified(parts[0]))return false;
        var suffix=parts[1].Split("::",StringSplitOptions.None);if(suffix.Length!=2||!Identifier(suffix[1]))return false;
        var versions=suffix[0].Split('.');return versions.Length==2&&versions.All(version=>version.Length>0&&version.EnumerateRunes().All(value=>Rune.GetUnicodeCategory(value)==UnicodeCategory.DecimalDigitNumber));
    }
}
