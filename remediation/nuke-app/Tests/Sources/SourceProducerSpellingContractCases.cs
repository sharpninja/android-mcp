using System.Text.Json.Nodes;

namespace AndroidMcp.BuildTool.Tests.Sources;

public static class SourceProducerSpellingContractCases
{
    public static IReadOnlyList<SourceFactCase> All { get; } = Create();
    private static IReadOnlyList<SourceFactCase> Create()
    {
        var result = new List<SourceFactCase>();
        SourceFactCase Copy(string baseline) { var value = SourceFactContractCases.Get(baseline); return value with { Id = "SPELLING-" + result.Count, Input = (JsonObject)value.Input.DeepClone(), Expected = value.Expected.DeepClone() }; }
        foreach (var name in new[] { "Callback", "Listener", "Observer", "DeathRecipient", "OutcomeReceiver", "ResultReceiver", "Runnable", "Callable", "Consumer", "Function", "Predicate", "Continuation" })
            foreach (var spelling in new[] { name, "p." + name.ToUpperInvariant() + "<T>", name + "$Tail", name + "\n" })
            { var test = Copy("FACT-CB01"); test.Input["name"] = spelling; result.Add(test); }
        foreach (var spelling in new[] { "LİSTENER", "Lıstener", "Obſerver", "CallbacK", "CallbackX", "Listener[]", "listen", "" })
        {
            var test = Copy(spelling is "CallbackX" or "Listener[]" or "listen" or "" ? "FACT-CB03" : "FACT-CB01"); test.Input["name"] = spelling; result.Add(test);
        }
        foreach (var (spelling, role) in new[] { ("unregısterListener", "unregistration-candidate"), ("unſubscribeListener", "unregistration-candidate"), ("SETListener", "registration-candidate"), ("bİndListener", "registration-candidate"), ("requeſtListener", "registration-candidate"), ("observeListener", "callback-parameter-candidate") })
        { var test = Copy("FACT-CB06"); test.Input["name"] = spelling; test.Expected[0]!["role"] = role; result.Add(test); }
        foreach (var (arguments, literal, symbol) in new (string, string?, string?)[]
        {
            ("\u001c\u001d\"clipboard\"\u001e\u001f", "clipboard", null), ("value\u001c=\u001d\"clipboard\"", "clipboard", null),
            ("A²", null, "A²"), ("A𐐀", null, "A𐐀"), ("A\u0301", null, null), ("A\u203f", null, null),
            ("A😀", null, null), ("$A_1.B$", null, "$A_1.B$"), ("1A", null, null), ("A..B", null, null)
        })
        {
            var test = Copy("FACT-FN01"); test.Input["extensions"]!["annotations"]![0]!["arguments"] = arguments;
            var row = test.Expected["functional_evidence"]![0]!; row["source_annotation"]!["arguments"] = arguments;
            row["service_string_literal"] = literal; row["service_symbol_expression"] = symbol;
            row["unresolved"]![0] = symbol is null ? "Service selector is literal or unresolved" : "Symbolic service values require exact same-snapshot constant resolution";
            result.Add(test);
        }
        return result;
    }
    public static IEnumerable<object[]> Cases => All.Select(test => new object[] { test.Id });
    public static SourceFactCase Get(string id) => All.Single(test => test.Id == id);
}
