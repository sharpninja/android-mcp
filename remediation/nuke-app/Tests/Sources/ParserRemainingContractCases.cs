using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

// Three finite inputs finish two original method compositions. The structured
// status assertion has its own explicit disposition; it is never called satisfied.
public static class ParserRemainingContractCases
{
    public static IReadOnlyList<ParserOriginalCase> All { get; } = Create();
    public static IEnumerable<object[]> Cases => All.Select(test => new object[] { test.Source.Id });
    public static ParserOriginalCase Get(string id) => All.Single(test => test.Source.Id == id);
    private static JsonObject Metadata(string text, string status) => new()
    {
        ["aidl_schema"] = new JsonObject
        {
            ["raw_schema"] = text, ["parse_status"] = status, ["codec_status"] = "unknown",
            ["transaction_metadata"] = new JsonObject { ["wire_status"] = "unknown" }
        }
    };
    private static JsonObject CloneRow(string id, int index) => (JsonObject)SourceExtractionContractCases.Get(id).ExpectedRecords[index].DeepClone();
    private static ParserOriginalCase Case(string id, string method, string text, JsonObject[] rows,
        string status, StructuralParseStatus structural = StructuralParseStatus.Clean, string? retired = null, params string[] issues) =>
        new(new(id, "aidl", text, structural, rows, issues, "Exact original " + method + " input"), method, text, null, [], Metadata(text, status), retired);

    private static IReadOnlyList<ParserOriginalCase> Create()
    {
        const string structured = "package a; @JavaDerive(toString=true) parcelable P { int n; }";
        var declaration = CloneRow("AIDL-02", 0);
        declaration["raw"] = "@JavaDerive(toString=true) parcelable P";
        declaration["annotations"] = new JsonArray(new JsonObject { ["name"] = "JavaDerive", ["arguments"] = "toString=true" });
        var structuredField = CloneRow("AIDL-11", 1);
        structuredField["canonical"] = "a.P#n"; structuredField["name"] = "n"; structuredField["raw"] = "int n"; structuredField["line"] = 1;

        const string external = "package a; parcelable P cpp_header \"P.h\";";
        var externalDeclaration = CloneRow("AIDL-06", 0);
        externalDeclaration["raw"] = "parcelable P cpp_header \"P.h\";";

        const string partial = "package a; parcelable P { new_syntax; int good; }";
        var partialDeclaration = CloneRow("AIDL-02", 0);
        partialDeclaration["extras"]!["parse_status"] = "field-metadata-partial";
        var unsupported = new JsonObject
        {
            ["kind"] = "unsupported", ["owner"] = "a.P", ["package"] = "a", ["name"] = "unparsed",
            ["raw"] = "new_syntax", ["line"] = 1, ["return_type"] = null,
            ["parameters"] = new JsonArray(), ["annotations"] = new JsonArray(),
            ["extras"] = new JsonObject { ["declared_ordinal"] = 0 }
        };
        var good = CloneRow("AIDL-11", 1);
        good["canonical"] = "a.P#good"; good["name"] = "good"; good["raw"] = "int good"; good["line"] = 1;
        good["extras"]!["declared_ordinal"] = 1;
        return
        [
            Case("PARSER-STRUCTURED-STATUS", "test_aidl_structured", structured, [declaration, structuredField], "field-metadata-parsed",
                retired: "Original low-level parse_status contains retained is an obsolete unresolved-field intermediate label. It is not satisfied; final enriched field inventory, original raw schema and unknown wire/codec facts replace that intermediate label. See aidl-structured-status-decision.json; no closure before mock/native proof."),
            Case("AIDL-MEMBER-EXTERNAL-PH", "test_external_parcelable_and_unknown_nested_schema_stay_visible", external,
                [externalDeclaration], "declaration-only"),
            Case("AIDL-MEMBER-PARTIAL-GOOD", "test_external_parcelable_and_unknown_nested_schema_stay_visible", partial,
                [partialDeclaration, unsupported, good], "field-metadata-partial", StructuralParseStatus.Partial, issues: ["aidl-semantic-gap"])
        ];
    }

    public static void AssertContract(ParserOriginalCase test, ISourceExtractor provider)
    {
        ExtractionResult? observed = null;
        var capture = new ScriptedSourceExtractor(provider.Kind, source => observed = provider.Extract(source));
        ParserOriginalContractCases.AssertContract(test, capture, new ScriptedPermissionExpression((_, _, _) => throw new InvalidOperationException("No permission consumer requested")));
        Assert.NotNull(observed);
        Assert.Equal("a.P", observed.Records[0]["canonical"]!.GetValue<string>());
        var members = observed.Records.Skip(1).ToArray();
        switch (test.Source.Id)
        {
            case "PARSER-STRUCTURED-STATUS":
                Assert.NotNull(test.RetiredAssertion);
                Assert.Single(members); Assert.Equal("n", members[0]["name"]!.GetValue<string>());
                Assert.Equal("field-metadata-parsed", observed.Metadata!["aidl_schema"]!["parse_status"]!.GetValue<string>());
                Assert.Null(members[0]["extras"]!["wire_tag"]);
                break;
            case "AIDL-MEMBER-EXTERNAL-PH": Assert.Empty(members); break;
            case "AIDL-MEMBER-PARTIAL-GOOD":
                Assert.Equal("unsupported", members[0]["kind"]!.GetValue<string>());
                Assert.Equal("good", members[1]["name"]!.GetValue<string>());
                Assert.Contains("partial", observed.Metadata!["aidl_schema"]!["parse_status"]!.GetValue<string>());
                Assert.Null(members[1]["extras"]!["wire_tag"]);
                break;
        }
    }
    public static IEnumerable<object[]> SourceMutations => All.SelectMany(test => SourceMutationCases.For(test.Source));
    public static IEnumerable<object[]> MetadataMutations => All.SelectMany(test => SourceMutationCases.For(test.MetadataMutationCase()));
}
