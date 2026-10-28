using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record ParserOriginalAssertion(int RecordIndex, string[] Path);
public sealed record ParserOriginalPermission(int RecordIndex, JsonObject Expected,
    IReadOnlyDictionary<string, string>? Symbols = null, bool Aidl = false);
public sealed record ParserOriginalCase(SourceExtractionCase Source, string OriginalMethod, string OriginalFragment,
    ParserOriginalPermission? Permission, IReadOnlyList<ParserOriginalAssertion> Controls, JsonObject? Metadata = null,
    string? RetiredAssertion = null)
{
    public ExtractionResult CorrectResult() => Source.CorrectResult() with { Metadata = (JsonObject?)Metadata?.DeepClone() };
    public SourceExtractionCase PermissionMutationCase() => new(Source.Id, "permission-expression", OriginalFragment,
        StructuralParseStatus.Clean, [Permission!.Expected], [], OriginalMethod);
    public SourceExtractionCase MetadataMutationCase() => new(Source.Id, Source.Kind, Source.Text,
        StructuralParseStatus.Clean, [Metadata!], [], OriginalMethod);
}

// Original parser helper contracts are exercised through the migrated
// ISourceExtractor -> IPermissionExpressionSourceFacts consumer path.
// No public compatibility API or duplicate lexical/parser implementation.
public static class ParserOriginalContractCases
{
    public static IReadOnlyList<ParserOriginalCase> All { get; } = Create();
    public static IEnumerable<object[]> Cases => All.Select(test => new object[] { test.Source.Id });
    public static IEnumerable<object[]> PermissionCases => All.Where(test => test.Permission is not null).Select(test => new object[] { test.Source.Id });
    public static ParserOriginalCase Get(string id) => All.Single(test => test.Source.Id == id);
    public static IReadOnlyList<string> PendingOriginalMethods { get; } =
        ["test_aidl_structured: old low-level structured-body parse_status contains retained; enriched native status differs and requires a separate explicit caller/disposition review."];

    private static ParserOriginalAssertion Control(int row, string path) => new(row, path.Split('/'));
    private static JsonObject Leaf(string name) => new() { ["op"] = "permission", ["name"] = name };
    private static JsonObject Annotation(string name, string arguments = "") => new() { ["name"] = name, ["arguments"] = arguments };
    private static JsonObject RawSchema(string text) => new() { ["aidl_schema"] = new JsonObject { ["raw_schema"] = text, ["codec_status"] = "unknown" } };
    private static IReadOnlyList<ParserOriginalAssertion> Controls(SourceExtractionCase source, params ParserOriginalAssertion[] extra) =>
        source.ExpectedRecords.SelectMany((row, index) => row.ContainsKey("canonical")
            ? new[] { Control(index, "kind"), Control(index, "canonical") } : [Control(index, "kind")])
            .Concat(extra).DistinctBy(item => item.RecordIndex + ":" + string.Join('/', item.Path)).ToArray();

    private static IReadOnlyList<ParserOriginalCase> Create()
    {
        var tests = new List<ParserOriginalCase>();
        // Exact indentation from the original Python triple-quoted input.
        const string aidl = "package a; import b.B;\n @EnforcePermission(\"android.permission.X\") oneway interface IFoo {\n @RequiresNoPermission void ping(in B b, out int[] value) = 7;\n @PermissionManuallyEnforced oneway void send(in String s);\n const int C=3;\n }";
        var aidlCase = SourceExtractionContractCases.Get("AIDL-01") with { Id = "PARSER-AIDL", Text = aidl };
        tests.Add(new(aidlCase, "test_aidl", aidl, new(0, Leaf("android.permission.X"), Aidl: true),
            Controls(aidlCase, Control(0, "annotations/0/name"), Control(0, "annotations/0/arguments"), Control(0, "extras/oneway"),
                Control(1, "extras/declared_method_id"), Control(1, "extras/wire_transaction_code"), Control(1, "parameters/1/direction"),
                Control(3, "name"), Control(3, "return_type"), Control(3, "extras/is_constant"), Control(3, "extras/constant_raw"),
                Control(3, "extras/wire_tag"), Control(3, "extras/wire_layout_status")), RawSchema(aidl),
            "Original members[2].kind == unsupported is retired only as the obsolete low-level constant wrapper; final enriched C field/raw value and unknown wire status are required instead. See aidl-intermediate-shape-decision.json."));

        var overloads = SourceExtractionContractCases.Get("SIG-01") with { Id = "PARSER-OVERLOADS" };
        tests.Add(new(overloads, "test_overloads_and_annotations", overloads.Text, new(5, Leaf("example.permission.P")),
            Controls(overloads, Control(5, "parameters/0/type"), Control(5, "return_type"), Control(5, "annotations/0/name"), Control(5, "annotations/0/arguments"))));
        var parameter = SourceExtractionContractCases.Get("SIG-04") with { Id = "PARSER-PARAMETER" };
        tests.Add(new(parameter, "test_parameter_permission_not_callable", "method public void x(@RequiresPermission(\"example.permission.P\") String);",
            new(2, (JsonObject)PermissionExpressionContractCases.Get("PERM-07").Expected.DeepClone()),
            Controls(parameter, Control(2, "annotations"), Control(2, "parameters/0/annotations/0/name"), Control(2, "parameters/0/annotations/0/arguments"))));
        var unknown = SourceExtractionContractCases.Get("SIG-02") with { Id = "PARSER-UNKNOWN" };
        tests.Add(new(unknown, "test_source_unknown_retained", unknown.Text, null, Controls(unknown, Control(1, "raw"))));
        var orphan = SourceExtractionContractCases.Get("SIG-03") with { Id = "PARSER-OWNER" };
        tests.Add(new(orphan, "test_owner_reset_after_type", orphan.Text, null, Controls(orphan, Control(3, "extras/parse_error"))));

        const string commentFragment = "const String X=\"//x\"; // hi";
        const string commented = "package a; interface I {\nconst String X=\"//x\"; // hi\n}";
        var commentBase = SourceExtractionContractCases.Get("AIDL-10");
        var commentOwner = (JsonObject)commentBase.ExpectedRecords[0].DeepClone();
        var commentField = (JsonObject)commentBase.ExpectedRecords[1].DeepClone();
        commentField["name"] = "X"; commentField["canonical"] = "a.I#X"; commentField["return_type"] = "String";
        commentField["raw"] = "const String X=\"//x\""; commentField["line"] = 2; commentField["extras"]!["constant_raw"] = "\"//x\"";
        var comment = new SourceExtractionCase("PARSER-COMMENT", "aidl", commented, StructuralParseStatus.Clean, [commentOwner, commentField], [],
            "test_comment_quoted original fragment embedded unchanged in an actual AIDL constant consumer");
        tests.Add(new(comment, "test_comment_quoted", commentFragment, null,
            Controls(comment, Control(1, "raw"), Control(1, "extras/constant_raw"), Control(1, "extras/wire_tag")), RawSchema(commented)));

        foreach (var (id, original, expectedId, annotation) in new[]
        {
            ("MODE-VALUE", "@RequiresPermission(value=\"example.permission.X\")", "PERM-01", Annotation("RequiresPermission", "value=\"example.permission.X\"")),
            ("MODE-ALL", "@RequiresPermission(allOf={\"example.permission.A\",\"example.permission.B\"})", "PERM-02", Annotation("RequiresPermission", "allOf={\"example.permission.A\",\"example.permission.B\"}")),
            ("MODE-CONDITIONAL", "@RequiresPermission(anyOf={\"example.permission.A\",\"example.permission.B\"},conditional=true)", "PERM-03", Annotation("RequiresPermission", "anyOf={\"example.permission.A\",\"example.permission.B\"},conditional=true")),
            ("MODE-READ", "@RequiresPermission.Read(@RequiresPermission(\"example.permission.X\"))", "PERM-04", Annotation("RequiresPermission.Read", "@RequiresPermission(\"example.permission.X\")")),
            ("MODE-MANUAL", "@PermissionManuallyEnforced", "PERM-05", Annotation("PermissionManuallyEnforced")),
            ("MODE-NONE", "@RequiresNoPermission", "PERM-06", Annotation("RequiresNoPermission")),
            ("MODE-ABSENT", "", "PERM-07", new JsonObject()),
            ("MODE-PREDICATE", "@RequiresPermission(\"carrier privileges\")", "PERM-10", Annotation("RequiresPermission", "\"carrier privileges\"")),
            ("MODE-BARE-READ", "@RequiresPermission.Read", "PERM-11", Annotation("RequiresPermission.Read"))
        })
            tests.Add(AnnotatedSignature(id, original, original.Length == 0 ? [] : new JsonArray(annotation),
                (JsonObject)PermissionExpressionContractCases.Get(expectedId).Expected.DeepClone()));
        var symbols = new Dictionary<string, string>(StringComparer.Ordinal) { ["android.Manifest.permission.X"] = "android.permission.X" };
        var symbolAst = Leaf("android.permission.X"); symbolAst["x-symbol"] = "android.Manifest.permission.X";
        tests.Add(AnnotatedSignature("MODE-SYMBOL", "@RequiresPermission(android.Manifest.permission.X)",
            new JsonArray(Annotation("RequiresPermission", "android.Manifest.permission.X")), symbolAst, symbols));

        const string enforce = "package a; @EnforcePermission(\"X\") interface I {}";
        var enforcedOwner = (JsonObject)commentBase.ExpectedRecords[0].DeepClone();
        enforcedOwner["raw"] = "@EnforcePermission(\"X\") interface I";
        enforcedOwner["annotations"] = new JsonArray(Annotation("EnforcePermission", "\"X\""));
        var enforced = new SourceExtractionCase("MODE-AIDL-UNQUALIFIED", "aidl", enforce, StructuralParseStatus.Clean, [enforcedOwner], [], "test_permission_modes exact EnforcePermission literal through AIDL declaration consumer");
        tests.Add(new(enforced, "test_permission_modes", "@EnforcePermission(\"X\")", new(0, (JsonObject)PermissionExpressionContractCases.Get("PERM-09").Expected.DeepClone(), Aidl: true),
            Controls(enforced, Control(0, "annotations/0/name"), Control(0, "annotations/0/arguments")), RawSchema(enforce)));
        return tests;
    }

    private static ParserOriginalCase AnnotatedSignature(string id, string original, JsonArray annotations, JsonObject expected,
        IReadOnlyDictionary<string, string>? symbols = null)
    {
        var template = SourceExtractionContractCases.Get("SIG-04");
        var raw = "method " + (original.Length == 0 ? "" : original + " ") + "public void x();";
        var method = (JsonObject)template.ExpectedRecords[2].DeepClone();
        method["canonical"] = "a.A#x()"; method["raw"] = raw; method["parameters"] = new JsonArray(); method["annotations"] = annotations;
        var source = new SourceExtractionCase(id, "signature", "package a {\n public class A {\n " + raw + "\n }\n}", StructuralParseStatus.Clean,
            [(JsonObject)template.ExpectedRecords[0].DeepClone(), (JsonObject)template.ExpectedRecords[1].DeepClone(), method], [],
            "test_permission_modes original annotation fragment embedded unchanged in a migrated callable consumer");
        return new(source, "test_permission_modes", original, new(2, expected, symbols),
            Controls(source, Control(2, "annotations")));
    }

    public static void AssertContract(ParserOriginalCase test, ISourceExtractor provider, IPermissionExpressionSourceFacts permissionProvider)
    {
        ExtractionResult? observed = null;
        SourceExtractionContractCases.AssertContract(test.Source, new ScriptedSourceExtractor(provider.Kind, source => observed = provider.Extract(source)));
        Assert.NotNull(observed);
        if (test.Metadata is not null) SourceExtractionContractCases.AssertSubset(test.Metadata, observed.Metadata, "original-parser-metadata");
        if (test.Permission is not null)
        {
            var annotations = observed.Records[test.Permission.RecordIndex]["annotations"]!.AsArray();
            var before = annotations.DeepClone();
            var symbols = test.Permission.Symbols?.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            var result = permissionProvider.Permission(annotations, symbols, test.Permission.Aidl);
            Assert.True(JsonNode.DeepEquals(test.Permission.Expected, result), "Original permission assertion differs: " + test.Source.Id);
            Assert.True(JsonNode.DeepEquals(before, annotations), "Permission consumer mutated parsed annotations");
            Assert.Equal(test.Permission.Symbols?.OrderBy(pair => pair.Key, StringComparer.Ordinal), symbols?.OrderBy(pair => pair.Key, StringComparer.Ordinal));
        }
        switch (test.Source.Id)
        {
            case "PARSER-AIDL":
                Assert.Equal("a.IFoo", observed.Records[0]["canonical"]!.GetValue<string>());
                Assert.True(observed.Records[0]["extras"]!["oneway"]!.GetValue<bool>());
                var members = observed.Records.Skip(1).ToArray(); Assert.Equal(3, members.Length);
                Assert.Equal(7, members[0]["extras"]!["declared_method_id"]!.GetValue<int>());
                Assert.Null(members[0]["extras"]!["wire_transaction_code"]);
                Assert.Equal("out", members[0]["parameters"]![1]!["direction"]!.GetValue<string>());
                Assert.NotNull(test.RetiredAssertion);
                // This is the final enriched inventory requirement, not a claim
                // that the explicitly retired unsupported-wrapper assertion passes.
                Assert.Equal("field", members[2]["kind"]!.GetValue<string>());
                Assert.Equal("3", members[2]["extras"]!["constant_raw"]!.GetValue<string>());
                Assert.Null(members[2]["extras"]!["wire_tag"]);
                break;
            case "PARSER-OVERLOADS":
                Assert.DoesNotContain(observed.Records, row => row["kind"]!.GetValue<string>() == "unsupported");
                Assert.Equal("annotation", observed.Records[1]["kind"]!.GetValue<string>());
                var methods = observed.Records.Where(row => row["name"]!.GetValue<string>() == "go").ToArray();
                Assert.NotEqual(methods[0]["canonical"]!.GetValue<string>(), methods[1]["canonical"]!.GetValue<string>());
                break;
            case "PARSER-PARAMETER": Assert.NotEmpty(observed.Records[2]["parameters"]![0]!["annotations"]!.AsArray()); break;
            case "PARSER-UNKNOWN": Assert.Equal("strange new declaration;", observed.Records[1]["raw"]!.GetValue<string>()); break;
            case "PARSER-OWNER": Assert.Contains("without owner", observed.Records[^1]["extras"]!["parse_error"]!.GetValue<string>()); break;
            case "PARSER-COMMENT": Assert.Contains("\"//x\"", observed.Records[1]["raw"]!.GetValue<string>()); break;
        }
    }

    public static IEnumerable<object[]> SourceMutations => All.SelectMany(test => test.Controls.SelectMany(control =>
        new[] { "omit", "wrong" }.Select(operation => new object[] { test.Source.Id, new SourceMutation(control.RecordIndex, control.Path, operation) })));
    public static IEnumerable<object[]> PermissionMutations => All.Where(test => test.Permission is not null)
        .SelectMany(test => SourceMutationCases.For(test.PermissionMutationCase()));
    public static IEnumerable<object[]> MetadataMutations => All.Where(test => test.Metadata is not null)
        .SelectMany(test => SourceMutationCases.For(test.MetadataMutationCase()));
}
