using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record HtmlDecodeCase(string Id, string Input, string Expected);
public sealed record HtmlEntityDataCase(string Id, JsonObject Official, string Legacy, JsonObject Expected)
{
    public SourceExtractionCase MutationCase() => new(Id, "html-entity-data", Legacy, StructuralParseStatus.Clean, [Expected], [], "Pinned official WHATWG entries and restricted literal legacy table DATA");
}
public static class SourceHtmlContractCases
{
    public static IReadOnlyList<HtmlDecodeCase> DecodeCases { get; } =
    [
        new("DEC-01", "plain", "plain"), new("DEC-02", "", ""), new("DEC-03", "&amp;&lt;&gt;&quot;&apos;", "&<>\"'"),
        new("DEC-04", "&Aopf;", "\U0001d538"), new("DEC-05", "&NotEqualTilde;", "\u2242\u0338"),
        new("DEC-06", "&notin;", "\u2209"), new("DEC-07", "&notit;", "\u00acit;"), new("DEC-08", "&notin", "\u00acin"),
        new("DEC-09", "&amp=foo", "&=foo"), new("DEC-10", "&unknown;", "&unknown;"), new("DEC-11", "&AMP &amp", "& &"),
        new("DEC-12", "&amp;lt;", "&lt;"), new("DEC-13", "&#65; &#x41; &#X41;", "A A A"),
        new("DEC-14", "&#65", "A"), new("DEC-15", "&#x1F600;", "\U0001f600"),
        new("DEC-16", "&#0;", "\ufffd"), new("DEC-17", "&#xD800;", "\ufffd"), new("DEC-18", "&#xDFFF;", "\ufffd"),
        new("DEC-19", "&#1114112;", "\ufffd"), new("DEC-20", "&#999999999999999999999999999999999;", "\ufffd"),
        new("DEC-21", "&#1;&#8;&#11;&#14;&#31;&#127;", ""), new("DEC-22", "&#9;&#10;&#12;&#13;", "\t\n\f\r"),
        new("DEC-23", "&#xFDD0;&#xFDEF;&#xFFFE;&#x10FFFF;", ""), new("DEC-24", "&#128;", "\u20ac"),
        new("DEC-25", "&#129;&#141;&#143;&#144;&#157;", "\u0081\u008d\u008f\u0090\u009d"),
        new("DEC-26", "&#x;&#-1;&&x", "&#x;&#-1;&&x"), new("DEC-27", "&Tab;&NewLine;", "\t\n"),
        new("DEC-28", "&CounterClockwiseContourIntegral;", "\u2233"), new("DEC-29", "&COPY; &copy", "\u00a9 \u00a9"),
        new("DEC-30", "&#130;&#131;&#132;&#133;&#134;&#135;&#136;&#137;&#138;&#139;&#140;&#142;", "\u201a\u0192\u201e\u2026\u2020\u2021\u02c6\u2030\u0160\u2039\u0152\u017d"),
        new("DEC-31", "&#145;&#146;&#147;&#148;&#149;&#150;&#151;&#152;&#153;&#154;&#155;&#156;&#158;&#159;", "\u2018\u2019\u201c\u201d\u2022\u2013\u2014\u02dc\u2122\u0161\u203a\u0153\u017e\u0178")
    ];
    private static JsonObject Entry(int[] points, string characters) => new() { ["codepoints"] = new JsonArray(points.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()), ["characters"] = characters };
    private static JsonObject Data() => new() { ["&amp;"] = Entry([38], "&"), ["&Aopf;"] = Entry([120120], "\U0001d538") };
    private static JsonObject Result(int official, int legacy, string[]? missing = null, string[]? extra = null, JsonObject[]? changed = null) => new()
    {
        ["official_entries"] = official, ["legacy_entries"] = legacy, ["equivalent"] = (missing?.Length ?? 0) + (extra?.Length ?? 0) + (changed?.Length ?? 0) == 0,
        ["missing_in_legacy"] = new JsonArray((missing ?? []).Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
        ["extra_in_legacy"] = new JsonArray((extra ?? []).Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
        ["changed_values"] = new JsonArray((changed ?? []).Cast<JsonNode>().ToArray())
    };
    public static IReadOnlyList<HtmlEntityDataCase> DataCases { get; } =
    [
        new("DATA-01", Data(), "# table data\nhtml5 = {\n 'amp;': '&',\n 'Aopf;': '\\U0001d538',\n}\n", Result(2, 2)),
        new("DATA-02", Data(), "html5 = {\n 'amp;': '&',\n}\n", Result(2, 1, missing: ["Aopf;"])),
        new("DATA-03", Data(), "html5 = {\n 'amp;': '&',\n 'Aopf;': '\\U0001d538',\n 'apos;': \"'\",\n}\n", Result(2, 3, extra: ["apos;"])),
        new("DATA-04", Data(), "html5 = {\n 'amp;': 'wrong',\n 'Aopf;': '\\U0001d538',\n}\n", Result(2, 2, changed: [new() { ["name"] = "amp;", ["official"] = "&", ["legacy"] = "wrong" }])),
        new("DATA-05", new() { ["&quot;"] = Entry([34], "\""), ["&bsol;"] = Entry([92], "\\"), ["&Tab;"] = Entry([9], "\t") },
            "html5 = {\n 'quot;': '\"',\n 'bsol;': '\\\\',\n 'Tab;': '\\t',\n}\n", Result(3, 3)),
        new("DATA-06", new() { ["&aacute;"] = Entry([225], "\u00e1"), ["&acE;"] = Entry([8766, 819], "\u223e\u0333") },
            "html5 = {\n 'aacute;': '\\xe1',\n 'acE;': '\\u223e\\u0333',\n}\n", Result(2, 2))
    ];
    public static IEnumerable<object[]> DecoderCases => DecodeCases.Select(x => new object[] { x.Id });
    public static IEnumerable<object[]> AuditCases => DataCases.Select(x => new object[] { x.Id });
    public static IEnumerable<object[]> AuditMutations => DataCases.SelectMany(x => SourceMutationCases.For(x.MutationCase()));
    public static HtmlDecodeCase Decoder(string id) => DecodeCases.Single(x => x.Id == id);
    public static HtmlEntityDataCase Audit(string id) => DataCases.Single(x => x.Id == id);
    public static void AssertDecode(HtmlDecodeCase test, ISourceHtmlDecoder provider) => Assert.Equal(test.Expected, provider.Decode(test.Input));
    public static void AssertAudit(HtmlEntityDataCase test, IHtmlEntityDataAudit provider)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(test.Official); var original = bytes.ToArray(); var result = provider.Compare(bytes, test.Legacy);
        Assert.True(JsonNode.DeepEquals(test.Expected, result), "HTML entity data comparison differs: " + test.Id); Assert.Equal(original, bytes);
    }
}
