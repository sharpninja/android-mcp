using System.Text;
using System.Text.Json.Nodes;

namespace AndroidMcp.BuildTool.Tests.Sources;

public static class NativeDescriptorSpellingContractCases
{
    public static IReadOnlyList<NativeDescriptorCase> All { get; } = Create();
    private static IReadOnlyList<NativeDescriptorCase> Create()
    {
        var result = new List<NativeDescriptorCase>();
        void Add(string text, string language, bool matched, string symbol = "IFoo", int start = 0)
        {
            var baseline = NativeDescriptorContractCases.Get(language == "rust" ? "ND-01" : "ND-06");
            var report = (JsonObject)baseline.Expected.DeepClone(); var facts = report["facts"]!.AsArray();
            var row = (JsonObject)facts[language == "rust" ? 0 : 1]!.DeepClone(); facts.Clear();
            if (matched)
            {
                row["raw"] = text[start..]; row["character_span"] = new JsonArray(text[..start].EnumerateRunes().Count(), text.EnumerateRunes().Count());
                if (language == "rust") row["declared_symbol_token"] = symbol;
                facts.Add(row);
            }
            result.Add(new("ND-SPELLING-" + result.Count, text, language, report));
        }
        Add("AIBinder_Class_define(\u001c\"android.X\"\u001f,a,b,c)", "cpp", true);
        Add("AIBinder_Class_define\u001c(\"android.X\",a,b,c)", "cpp", true);
        Add("declare_binder_interface! { IFoo[\u001c\"android.foo.IFoo\"\u001f] {} }", "rust", true);
        Add("declare_binder_interface\u001c!\u001d{ IFoo[\"android.foo.IFoo\"] {} }", "rust", true);
        Add("declare_binder_interface! { IFoo²[\"android.foo.IFoo\"] {} }", "rust", true, "IFoo²");
        Add("declare_binder_interface! { IFoo𐐀[\"android.foo.IFoo\"] {} }", "rust", true, "IFoo𐐀");
        foreach (var (prefix, matched) in new[] { ("²", false), ("𐐀", false), ("\u0301", true), ("\u203f", true) })
            Add(prefix + "AIBinder_Class_define(\"android.X\",a,b,c)", "cpp", matched, start: prefix.Length);
        return result;
    }
    public static IEnumerable<object[]> Cases => All.Select(test => new object[] { test.Id });
    public static NativeDescriptorCase Get(string id) => All.Single(test => test.Id == id);
}
