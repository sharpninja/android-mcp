using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Text;
using System.Globalization;

namespace AndroidMcp.BuildTool.Sources;

public sealed class PublishedInterfaceHashSourceFacts : IPublishedInterfaceHashSourceFacts
{
    private static readonly Regex Aidl = new(@"\A[0-9a-fA-F]{40}\z", RegexOptions.CultureInvariant);
    // Capture the format's five fields, then apply the original scalar word
    // and decimal rules. .NET regex word/digit classes differ on valid Unicode.
    private static readonly Regex Hidl = new(@"\A([0-9a-fA-F]{64})[\s\u001c-\u001f]+([^@]+)@([^.]+)\.([^:]+)::(.+)\z", RegexOptions.CultureInvariant);
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
            if (!match.Success || hidl && !HidlFields(match)) { unknown.Add(new JsonObject { ["line"] = line, ["raw"] = raw }); continue; }
            var fact = new JsonObject { ["line"] = line, ["literal"] = hidl ? match.Groups[1].Value : value,
                ["format"] = hidl ? "published-hidl-hash-hex64-and-identifier" : "published-aidl-hash-hex40", ["raw"] = raw };
            if (hidl) fact["declared_identifier"] = value[match.Groups[2].Index..];
            facts.Add(fact);
        }
        return new JsonObject
        {
            ["facts"] = facts, ["unknown_lines"] = unknown, ["compiled_runtime_hash_proven"] = false, ["android_applicability_proven"] = false,
            ["semantic_note"] = hidl ? "Repeated identifiers and compatibility hash lines preserved; HIDL package versions are not Android API versions"
                : "All literal lines retained; selecting a generated interface hash requires compiler/build/version linkage"
        };
    }

    private static bool HidlFields(Match match)
    {
        static bool Names(string value) => value.Split('.').All(part => part.Length > 0 &&
            part[0] is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or '_' && part.EnumerateRunes().All(SourceText.OriginalWord));
        static bool Digits(string value) => value.Length > 0 && value.EnumerateRunes().All(rune => Rune.GetUnicodeCategory(rune) == UnicodeCategory.DecimalDigitNumber);
        return Names(match.Groups[2].Value) && Digits(match.Groups[3].Value) && Digits(match.Groups[4].Value) && Names(match.Groups[5].Value);
    }
}
