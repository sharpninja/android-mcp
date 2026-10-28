using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AndroidMcp.BuildTool.Sources;

public sealed class PublishedInterfaceHashSourceFacts : IPublishedInterfaceHashSourceFacts
{
    private static readonly Regex Aidl = new(@"\A[0-9a-fA-F]{40}\z", RegexOptions.CultureInvariant);
    private static readonly Regex Hidl = new(@"\A([0-9a-fA-F]{64})\s+([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*@\d+\.\d+::[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\z", RegexOptions.CultureInvariant);
    public JsonObject AidlHashes(string text) => Parse(text, false);
    public JsonObject HidlHashes(string text) => Parse(text, true);

    private static JsonObject Parse(string text, bool hidl)
    {
        var facts = new JsonArray(); var unknown = new JsonArray();
        foreach (var (line, raw) in SourceText.Lines(text))
        {
            var comment = raw.IndexOf('#'); var value = SourceText.Strip(comment < 0 ? raw : raw[..comment]);
            if (value.Length == 0) continue;
            var match = (hidl ? Hidl : Aidl).Match(value);
            if (!match.Success) { unknown.Add(new JsonObject { ["line"] = line, ["raw"] = raw }); continue; }
            var fact = new JsonObject { ["line"] = line, ["literal"] = hidl ? match.Groups[1].Value : value,
                ["format"] = hidl ? "published-hidl-hash-hex64-and-identifier" : "published-aidl-hash-hex40", ["raw"] = raw };
            if (hidl) fact["declared_identifier"] = match.Groups[2].Value;
            facts.Add(fact);
        }
        return new JsonObject
        {
            ["facts"] = facts, ["unknown_lines"] = unknown, ["compiled_runtime_hash_proven"] = false, ["android_applicability_proven"] = false,
            ["semantic_note"] = hidl ? "Repeated identifiers and compatibility hash lines preserved; HIDL package versions are not Android API versions"
                : "All literal lines retained; selecting a generated interface hash requires compiler/build/version linkage"
        };
    }
}
