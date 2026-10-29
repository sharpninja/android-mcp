using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources;

namespace AndroidMcp.BuildTool.Tests.Sources;

public static class SoongLexicalCompletionContractCases
{
    public static IReadOnlyList<SoongModuleCase> All { get; } = Create();
    private static IReadOnlyList<SoongModuleCase> Create()
    {
        var rows = new List<SoongModuleCase>();
        foreach (var family in new[] { SoongModuleFamily.Interfaces, SoongModuleFamily.Apex })
        {
            var kind = family == SoongModuleFamily.Interfaces ? "aidl_interface" : "apex";
            void Add(string key, string rawValue, JsonObject value, bool complete = true, string separator = " ", string prefix = "")
            {
                var raw = kind + separator + "{ " + key + ":" + rawValue + " }";
                var moduleRaw = complete ? raw : kind + separator + "{";
                var properties = SoongContractCases.Object(complete ? raw[(raw.IndexOf('{'))..] : "{", complete,
                    complete ? new[] { SoongContractCases.Property(key, value) } : Array.Empty<JsonObject>());
                var module = new JsonObject { ["kind"] = kind, ["line"] = prefix.Length == 0 ? 1 : 2, ["properties"] = properties, ["raw"] = moduleRaw };
                if (family == SoongModuleFamily.Interfaces)
                { module["status"] = complete ? "static-properties-parsed" : "unclosed-module-block"; module["runtime_module_identity_proven"] = false; module["android_applicability_proven"] = false; }
                else
                {
                    var literals = new JsonObject();
                    foreach (var name in new[] { "name", "manifest", "defaults", "apex_name", "version", "min_sdk_version", "updatable", "package_name" })
                        literals[name] = complete && key == name && value["kind"]!.GetValue<string>() == "literal" ? value["value"]?.DeepClone() : null;
                    module["literal_properties"] = literals; module["installed_module_version_proven"] = false;
                }
                rows.Add(new("SOONG-SCALAR-" + rows.Count, prefix + raw, family, new JsonArray(module)));
            }
            foreach (var suffix in new[] { "_", "é", "Ⅷ", "²", "𐐀", "𝟘" }) Add("name" + suffix, "\"x\"", SoongContractCases.Lit("\"x\"", "x"));
            foreach (var suffix in new[] { "\u0301", "\u203f", "\u200d", "😀" }) Add("name" + suffix, "\"x\"", SoongContractCases.Lit("\"x\"", "x"), false);
            Add("éname", "\"x\"", SoongContractCases.Lit("\"x\"", "x"), false); // ASCII-first, not a general Unicode identifier.
            Add("😀", "\"x\"", SoongContractCases.Lit("\"x\"", "x")); // Original fallback is one scalar token.
            foreach (var (raw, number) in new[] { ("12", 12), ("١٢", 12), ("𝟙𝟚", 12), ("१𝟚٣", 123), ("𝟘𝟘𝟜", 4) })
                Add("version", raw, SoongContractCases.Lit(raw, number));
            foreach (var raw in new[] { "²", "Ⅷ", "𝟙²", "١ + 𝟚" }) Add("version", raw, new JsonObject { ["kind"] = "unresolved-expression", ["raw"] = raw });
            Add("version", "9223372036854775808", SoongContractCases.Lit("9223372036854775808", JsonValue.Create(9223372036854775808UL)!));
            Add("name", "\"x\"", SoongContractCases.Lit("\"x\"", "x"), separator: "\u001f");
            Add("name", "\"x\"", SoongContractCases.Lit("\"x\"", "x"), prefix: "// 𐐀 comment\n");
        }
        return rows;
    }
    public static IEnumerable<object[]> Cases => All.Select(test => new object[] { test.Id });
    public static SoongModuleCase Get(string id) => All.Single(test => test.Id == id);
}
