using System.Text.Json;
using System.Text.Json.Nodes;
using System.Diagnostics.CodeAnalysis;
using AndroidMcp.BuildTool.Core;
namespace AndroidMcp.BuildTool.Modules.Review;

public sealed record ReviewNormalizationProjection(byte[] Json,IReadOnlyList<StoredArtifact> Inputs);
// Shared domain codec admission, using the existing pin/path/private/canonical authorities.
internal static class ReviewNormalization
{
    internal static void Need([DoesNotReturnIf(false)] bool condition,string code)
    {if(!condition)throw new ReviewException(code,"Review normalization prerequisite is unavailable: "+code);}
    internal static string Text(JsonNode? value,string name)=>value is JsonObject obj&&obj[name] is JsonValue item&&item.TryGetValue<string>(out var text)?text:"";
    internal static JsonArray Array(JsonNode? value,string name,string code)
    {Need(value is JsonObject obj&&obj[name] is JsonArray,code);return (JsonArray)value![name]!;}
    internal static async Task<JsonObject> ReadAsync(IPinnedArtifactReader pins,ICanonicalJson canonical,StoredArtifact pin,CancellationToken token)
    {
        var raw=await ReadBytesAsync(pins,pin,token);var strict=canonical.Serialize(raw);
        ReviewPrepareService.PublicPayload("",ReviewPrepareService.Utf8.GetString(strict));
        return JsonSerializer.Deserialize<JsonObject>(strict)??throw new ReviewException("NORMALIZATION_SCHEMA_UNAVAILABLE","Expected one control object.");
    }
    internal static async Task<ReadOnlyMemory<byte>> ReadBytesAsync(IPinnedArtifactReader pins,StoredArtifact pin,CancellationToken token)
    {
        Need(pin is not null&&ArtifactPaths.IsRelative(pin.RelativePath),"NORMALIZATION_SCHEMA_UNAVAILABLE");
        ReviewPrepareService.PublicPayload(pin.RelativePath,"");
        return await pins.ReadAsync(pin,token);
    }
}
