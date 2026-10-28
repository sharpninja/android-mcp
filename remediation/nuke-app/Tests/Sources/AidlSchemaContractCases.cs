using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record AidlSchemaCase(SourceExtractionCase Source, JsonObject Schema)
{
    public ExtractionResult Correct() => Source.CorrectResult() with { Metadata = new JsonObject { ["aidl_schema"] = Schema.DeepClone() } };
    public SourceExtractionCase MetadataCase() => Source with { ExpectedRecords = [Schema] };
}
public static class AidlSchemaContractCases
{
    private const string Generic = "All source bytes retained; generated transaction IDs, resolved imports and wire codecs unknown";
    private const string Structured = "Source fields/enumerants inventoried; imports, runtime type resolution, wire tags/layouts and codecs remain unverified";
    private const string NoDeclaration = "No supported top-level AIDL declaration";
    private const string BadAnnotation = "AIDL annotation/syntax unresolved: Unclosed annotation";
    private static JsonObject Row(string kind, string? canonical, string raw, JsonObject? extras = null, string? package = null)
    {
        var row = new JsonObject { ["kind"] = kind, ["raw"] = raw };
        if (canonical is not null) row["canonical"] = canonical;
        if (package is not null) row["package"] = package;
        if (extras is not null) row["extras"] = extras;
        return row;
    }
    private static JsonObject Declaration(string? descriptor, string? kind, string status, string reason,
        int? line = null, int? ordinal = null, int? start = null, int? end = null, string? parent = null, int? depth = null) => new()
    {
        ["descriptor"] = descriptor, ["declaration_kind"] = kind, ["parse_status"] = status, ["reason"] = reason,
        ["declaration_line"] = line, ["declaration_ordinal"] = ordinal,
        ["declaration_span"] = start.HasValue ? new JsonArray(start.Value, end!.Value) : null,
        ["parent_descriptor"] = parent, ["declaration_depth"] = depth
    };
    private static AidlSchemaCase Case(string id, string text, string package, string? descriptor, string? kind, string status,
        JsonObject[] declarations, JsonObject[] records, StructuralParseStatus structural = StructuralParseStatus.Clean, string[]? issues = null, string[]? imports = null)
    {
        var schema = new JsonObject
        {
            ["package"] = package, ["descriptor"] = descriptor, ["declaration_kind"] = kind, ["raw_schema"] = text, ["parse_status"] = status,
            ["transaction_metadata"] = new JsonObject
            {
                ["imports"] = new JsonArray((imports ?? []).Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
                ["wire_status"] = "unknown", ["declarations"] = new JsonArray(declarations.Cast<JsonNode>().ToArray())
            },
            ["codec_status"] = "unknown"
        };
        return new(new(id, "aidl", text, structural, records, issues ?? [], "build_catalog aidl_schemas exact8column concern plus originalSourceIntegration malformed fixture"), schema);
    }
    public static IReadOnlyList<AidlSchemaCase> All { get; } =
    [
        Case("ASCH-01", "interface I {}", "", "I", "interface", "methods-parsed",
            [Declaration("I", "interface", "methods-parsed", Generic, 1, 0, 0, 14, depth: 0)], [Row("aidl-interface", "I", "interface I")]),
        Case("ASCH-02", "parcelable P { int x; }", "", null, "parcelable", "field-metadata-parsed",
            [Declaration("P", "parcelable", "field-metadata-parsed", Structured, 1, 0, 0, 23, depth: 0)],
            [Row("aidl-parcelable", "P", "parcelable P", new() { ["schema_resolution_reason"] = Structured }), Row("field", "P#x", "int x")]),
        Case("ASCH-03", "interface I { broken; }", "", "I", "interface", "partial",
            [Declaration("I", "interface", "partial", Generic, 1, 0, 0, 23, depth: 0)],
            [Row("aidl-interface", "I", "interface I"), Row("unsupported", null, "broken")], StructuralParseStatus.Partial, ["aidl-semantic-gap"]),
        Case("ASCH-04", "interface A {} interface B {}", "", null, "multiple", "declarations-inventoried",
            [Declaration("A", "interface", "methods-parsed", Generic, 1, 0, 0, 14, depth: 0), Declaration("B", "interface", "methods-parsed", Generic, 1, 1, 14, 29, depth: 0)],
            [Row("aidl-interface", "A", "interface A"), Row("aidl-interface", "B", "interface B")]),
        Case("ASCH-05", "interface O { parcelable P {} }", "", "O", "interface", "methods-parsed",
            [Declaration("O", "interface", "methods-parsed", Generic, 1, 0, 0, 31, depth: 0), Declaration("O.P", "parcelable", "field-metadata-parsed", Structured, 1, 1, 13, 29, "O", 1)],
            [Row("aidl-interface", "O", "interface O"), Row("aidl-parcelable", "O.P", "parcelable P", new() { ["schema_resolution_reason"] = Structured })]),
        Case("ASCH-06", "interface I;", "", "I", "interface", "declaration-only",
            [Declaration("I", "interface", "declaration-only", Generic, 1, 0, 0, 12, depth: 0)], [Row("aidl-interface", "I", "interface I;")]),
        Case("ASCH-07", "unsupported source", "", null, null, "unsupported",
            [Declaration(null, null, "unsupported", NoDeclaration)], [Row("unsupported", null, "unsupported source", new() { ["parse_error"] = NoDeclaration, ["source_bytes_retained"] = true }, "")],
            StructuralParseStatus.Unsupported, ["aidl-semantic-gap"]),
        Case("ASCH-08", "package a; @Broken( interface IFoo {", "", null, null, "unsupported",
            [Declaration(null, null, "unsupported", BadAnnotation)], [Row("unsupported", null, "package a; @Broken( interface IFoo {", new() { ["parse_error"] = BadAnnotation, ["source_bytes_retained"] = true }, "")],
            StructuralParseStatus.Malformed, ["aidl-semantic-gap", "aidl-reconciliation"]),
        Case("ASCH-09", "interface I {", "", "I", "interface", "unsupported",
            [Declaration("I", "interface", "unsupported", "Unbalanced declaration body", 1, 0, 0, 13, depth: 0)],
            [Row("unsupported", "I", "interface I", new() { ["parse_status"] = "unsupported", ["parse_error"] = "Unbalanced declaration body" })],
            StructuralParseStatus.Malformed, ["aidl-semantic-gap", "aidl-reconciliation"]),
        Case("ASCH-10", "interface A {} parcelable P { broken; }", "", null, "multiple", "partial",
            [Declaration("A", "interface", "methods-parsed", Generic, 1, 0, 0, 14, depth: 0), Declaration("P", "parcelable", "field-metadata-partial", Structured, 1, 1, 14, 39, depth: 0)],
            [Row("aidl-interface", "A", "interface A"), Row("aidl-parcelable", "P", "parcelable P", new() { ["schema_resolution_reason"] = Structured }), Row("unsupported", null, "broken")],
            StructuralParseStatus.Partial, ["aidl-semantic-gap"]),
        Case("ASCH-11", "package a;\nimport b.C;\ninterface I {}", "a", "a.I", "interface", "methods-parsed",
            [Declaration("a.I", "interface", "methods-parsed", Generic, 3, 0, 22, 37, depth: 0)], [Row("aidl-interface", "a.I", "interface I")], imports: ["b.C"]),
        Case("ASCH-12", "//\U0001f600\ninterface I {}", "", "I", "interface", "methods-parsed",
            [Declaration("I", "interface", "methods-parsed", Generic, 2, 0, 0, 18, depth: 0)], [Row("aidl-interface", "I", "//\U0001f600\ninterface I")]),
        Case("ASCH-13", "", "", null, null, "unsupported", [Declaration(null, null, "unsupported", NoDeclaration)],
            [Row("unsupported", null, "", new() { ["parse_error"] = NoDeclaration, ["source_bytes_retained"] = true }, "")], StructuralParseStatus.Unsupported, ["aidl-semantic-gap"])
    ];
    public static IEnumerable<object[]> Cases => All.Select(test => new object[] { test.Source.Id });
    public static IEnumerable<object[]> NonemptyCases => All.Where(test => test.Source.Text.Length > 0).Select(test => new object[] { test.Source.Id });
    public static IEnumerable<object[]> MetadataMutations => All.SelectMany(test => SourceMutationCases.For(test.MetadataCase()));
    public static IEnumerable<object[]> RecordMutations => All.SelectMany(test => SourceMutationCases.For(test.Source));
    public static AidlSchemaCase Get(string id) => All.Single(test => test.Source.Id == id);
    public static void AssertContract(AidlSchemaCase test, ISourceExtractor provider)
    {
        ExtractionResult? actual = null;
        var recording = new ScriptedSourceExtractor(provider.Kind, source => actual = provider.Extract(source));
        SourceExtractionContractCases.AssertContract(test.Source, recording);
        Assert.Single(recording.Received); Assert.Equal(test.Source.Document().Context, recording.Received[0].Context);
        Assert.NotNull(actual); Assert.NotNull(actual.Metadata); Assert.True(actual.Metadata.ContainsKey("aidl_schema"));
        Assert.True(JsonNode.DeepEquals(test.Schema, actual.Metadata["aidl_schema"]), "AIDL raw schema/locator summary differs: " + test.Source.Id);
    }
}
