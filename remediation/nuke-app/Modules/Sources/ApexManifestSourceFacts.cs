using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Sources;

public sealed class ApexManifestSourceFacts(IStrictJsonInspector inspector) : IApexManifestSourceFacts
{
    public ApexManifestSourceFacts() : this(new StrictJsonInspector()) { }
    public JsonObject Manifest(string text)
    {
        var masked = SourceText.MaskBuildComments(text);
        StrictJsonInspection inspection;
        try { inspection = inspector.Inspect(new UTF8Encoding(false, true).GetBytes(masked)); }
        catch (EncoderFallbackException) { return Unsupported(text, "Input contains invalid Unicode"); }
        if (inspection.HasFatalErrors || !inspection.Value.HasValue)
            return Unsupported(text, inspection.Diagnostics.FirstOrDefault()?.Message ?? "Invalid source manifest JSON");
        var duplicates = inspection.Diagnostics.Where(x => x.Code == CanonicalJsonErrors.DuplicateProperty).Select(x => x.DecodedPropertyName!);
        var raw = inspection.Value.Value;
        if (raw.ValueKind != JsonValueKind.Object || inspection.HasDuplicateProperties)
            return new JsonObject
            {
                ["status"] = "ambiguous-json", ["duplicate_keys"] = SourceSyntax.Strings(duplicates), ["raw"] = text,
                ["applicability"] = new JsonObject { ["unknown_reason"] = "APEX manifest object/keys unresolved" }
            };
        string? name = null; string? normalized = null; JsonNode? version = null;
        if (raw.TryGetProperty("name", out var rawName) && rawName.ValueKind == JsonValueKind.String && rawName.GetString() is { Length: > 0 } literalName) name = literalName;
        if (raw.TryGetProperty("version", out var rawVersion))
        {
            version = JsonNode.Parse(rawVersion.GetRawText());
            if (rawVersion.ValueKind == JsonValueKind.Number && StrictJsonInspector.IsIntegerToken(rawVersion.GetRawText()) && rawVersion.TryGetInt64(out var number) && number >= 0)
                normalized = number.ToString(CultureInfo.InvariantCulture);
            else if (rawVersion.ValueKind == JsonValueKind.String)
            {
                var literal = rawVersion.GetString()!;
                if (Regex.IsMatch(literal, @"\A(?:0|[1-9][0-9]*)\z", RegexOptions.CultureInvariant) && long.TryParse(literal, NumberStyles.None, CultureInfo.InvariantCulture, out _)) normalized = literal;
            }
        }
        return new JsonObject
        {
            ["status"] = "source-manifest-literals-parsed", ["name"] = name, ["version_literal"] = version, ["normalized_version_code"] = normalized,
            ["raw_properties"] = JsonNode.Parse(raw.GetRawText(), null, new JsonDocumentOptions { MaxDepth = int.MaxValue }),
            ["comment_normalization"] = masked != text ? "comments masked without altering string literals or line positions" : null,
            ["raw_source_text"] = text, ["version_domain"] = "APEX source-manifest version code, not Android API or AIDL frozen version",
            ["installed_identity_or_version_proven"] = false,
            ["applicability"] = new JsonObject { ["unknown_reason"] = "Product selection, build version overrides and installed module version range are unproven" }
        };
    }

    private static JsonObject Unsupported(string text, string reason) => new()
    {
        ["status"] = "unsupported-json", ["reason"] = reason, ["raw"] = text,
        ["applicability"] = new JsonObject { ["unknown_reason"] = "APEX source manifest could not be parsed" }
    };
}
