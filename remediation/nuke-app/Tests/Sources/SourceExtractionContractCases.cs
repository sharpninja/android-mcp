using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

// Requirements: FR-BUILD-004, TEST-BUILD-004; planning S-AC015 and the
// signature/annotation-scope subset of S-AC020. Expected data is authored
// from the retained legacy assertions, never obtained by executing a parser.
public sealed record SourceExtractionCase(
    string Id, string Kind, string Text, StructuralParseStatus Status,
    IReadOnlyList<JsonObject> ExpectedRecords, IReadOnlyList<string> IssueCodes,
    string Evidence)
{
    public SourceDocument Document() => new("fixtures/" + Id + (Kind == "aidl" ? ".aidl" : ".txt"),
        Encoding.UTF8.GetBytes(Text), new("fixture-repository", "pinned-fixture-revision", "fixture-snapshot", "37.2"));

    public ExtractionResult CorrectResult() => new(
        ExpectedRecords.Select(x => (JsonObject)x.DeepClone()).ToArray(),
        IssueCodes.Select(code => new SourceIssue(code, "Retained fixture issue", null, null)).ToArray(), Status);
}

public static class SourceExtractionContractCases
{
    public static IReadOnlyList<SourceExtractionCase> All { get; } = Create();
    public static IEnumerable<object[]> Cases => All.Select(x => new object[] { x.Id });
    public static IEnumerable<object[]> OrderCases => All.Where(x => x.ExpectedRecords.Count > 1).Select(x => new object[] { x.Id });
    public static SourceExtractionCase Get(string id) => All.Single(x => x.Id == id);

    private static JsonArray Array(params JsonNode?[] values) => new(values);
    private static JsonObject Annotation(string name, string arguments = "") => new() { ["name"] = name, ["arguments"] = arguments };
    private static JsonObject Parameter(string type, string? name = null, string? direction = null, params JsonNode?[] annotations) => new()
    {
        ["type"] = type, ["name"] = name, ["direction"] = direction, ["annotations"] = Array(annotations)
    };

    private static JsonObject Row(string kind, string canonical, string? owner, string name, string raw, int line,
        string? returnType = null, JsonArray? parameters = null, JsonArray? annotations = null, JsonObject? extras = null) => new()
    {
        ["kind"] = kind, ["canonical"] = canonical, ["owner"] = owner, ["package"] = "a", ["name"] = name,
        ["return_type"] = returnType, ["parameters"] = parameters ?? Array(), ["annotations"] = annotations ?? Array(),
        ["raw"] = raw, ["line"] = line, ["extras"] = extras ?? new JsonObject()
    };

    private static JsonObject Declaration(string kind, string descriptor, string raw, int line = 1, int ordinal = 0,
        string? parent = null, int depth = 0, bool oneway = false, string? status = null, JsonArray? annotations = null,
        JsonArray? imports = null) => Row("aidl-" + kind, descriptor, descriptor, descriptor[(descriptor.LastIndexOf('.') + 1)..], raw, line,
        annotations: annotations, extras: new()
        {
            ["aidl_kind"] = kind, ["imports"] = imports ?? Array(),
            ["parse_status"] = status ?? (kind == "interface" ? "methods-parsed" : "field-metadata-parsed"),
            ["oneway"] = oneway, ["declaration_ordinal"] = ordinal,
            ["parent_descriptor"] = parent, ["declaration_depth"] = depth
        });

    private static JsonObject Method(string owner, string name, string raw, int line = 1, string returnType = "void",
        JsonArray? parameters = null, bool oneway = false, bool interfaceOneway = false, int ordinal = 0,
        int? methodId = null, JsonArray? annotations = null, JsonArray? imports = null)
    {
        parameters ??= Array();
        var types = parameters.Select(x => x!["type"]!.GetValue<string>());
        return Row("method", owner + "#" + name + "(" + string.Join(',', types) + ")", owner, name, raw, line, returnType,
            parameters, annotations, new()
            {
                ["oneway"] = oneway, ["interface_oneway"] = interfaceOneway, ["declaration_ordinal"] = ordinal,
                ["declared_method_id"] = methodId, ["wire_transaction_code"] = null,
                ["wire_status"] = "unknown; generated Binder transaction constants and codecs not imported", ["imports"] = imports ?? Array()
            });
    }

    private static JsonObject Field(string owner, string name, string type, string raw, int line = 1,
        string kind = "parcelable", int ordinal = 0, string? constant = null, bool isConstant = false,
        JsonArray? annotations = null) => Row("field", owner + "#" + name, owner, name, raw, line, type,
        annotations: annotations, extras: new()
        {
            ["aidl_declaration_kind"] = kind, ["enum_constant"] = kind == "enum", ["is_constant"] = isConstant,
            ["constant_raw"] = constant, ["declared_ordinal"] = ordinal, ["wire_tag"] = null,
            ["wire_layout_status"] = "unknown; declaration order/value is not verified wire metadata"
        });

    private static SourceExtractionCase Case(string id, string kind, string text, JsonObject[] records,
        string evidence, StructuralParseStatus status = StructuralParseStatus.Clean, params string[] issues) =>
        new(id, kind, text, status, records, issues, evidence);

    private static IReadOnlyList<SourceExtractionCase> Create()
    {
        var cases = new List<SourceExtractionCase>
        {
            Case("AIDL-01", "aidl", "package a; import b.B;\n@EnforcePermission(\"android.permission.X\") oneway interface IFoo {\n@RequiresNoPermission void ping(in B b, out int[] value) = 7;\n@PermissionManuallyEnforced oneway void send(in String s);\nconst int C=3;\n}",
            [
                Declaration("interface", "a.IFoo", "@EnforcePermission(\"android.permission.X\") oneway interface IFoo", 2,
                    oneway: true, annotations: Array(Annotation("EnforcePermission", "\"android.permission.X\"")), imports: Array("b.B")),
                Method("a.IFoo", "ping", "@RequiresNoPermission void ping(in B b, out int[] value) = 7", 3,
                    parameters: Array(Parameter("B", "b", "in"), Parameter("int[]", "value", "out")), interfaceOneway: true,
                    methodId: 7, annotations: Array(Annotation("RequiresNoPermission")), imports: Array("b.B")),
                Method("a.IFoo", "send", "@PermissionManuallyEnforced oneway void send(in String s)", 4,
                    parameters: Array(Parameter("String", "s", "in")), oneway: true, interfaceOneway: true, ordinal: 1,
                    annotations: Array(Annotation("PermissionManuallyEnforced")), imports: Array("b.B")),
                Field("a.IFoo", "C", "int", "const int C=3", 5, "interface", 2, "3", true)
            ], "test_parsers.py:test_aidl (enriched constant behavior from test_aidl_members.py)"),

            Case("AIDL-02", "aidl", "package a; parcelable P {\n @nullable String name;\n int[] values={1,2};\n const String S=\"a;b\";\n}",
            [ Declaration("parcelable", "a.P", "parcelable P"),
              Field("a.P", "name", "String", "@nullable String name", 2, annotations: Array(Annotation("nullable"))),
              Field("a.P", "values", "int[]", "int[] values={1,2}", 3, ordinal: 1, constant: "{1,2}"),
              Field("a.P", "S", "String", "const String S=\"a;b\"", 4, ordinal: 2, constant: "\"a;b\"", isConstant: true) ],
            "test_aidl_members.py:test_parcelable_fields_annotations_and_values"),

            Case("AIDL-03", "aidl", "package a; enum E { A=1<<0, B=1<<1, C, }",
            [ Declaration("enum", "a.E", "enum E"), Field("a.E", "A", "a.E", "A=1<<0", kind: "enum", constant: "1<<0"),
              Field("a.E", "B", "a.E", "B=1<<1", kind: "enum", ordinal: 1, constant: "1<<1"),
              Field("a.E", "C", "a.E", "C", kind: "enum", ordinal: 2) ],
            "test_aidl_members.py:test_enum_shifts_and_union_declaration_ordinals_not_tags"),

            Case("AIDL-04", "aidl", "package a; union U { int x; String y; }",
            [ Declaration("union", "a.U", "union U"), Field("a.U", "x", "int", "int x", kind: "union"),
              Field("a.U", "y", "String", "String y", kind: "union", ordinal: 1) ],
            "test_aidl_members.py:test_enum_shifts_and_union_declaration_ordinals_not_tags"),

            Case("AIDL-05", "aidl", "package a; parcelable Outer.Inner; parcelable Other cpp_header \"o.h\";",
            [ Declaration("parcelable", "a.Outer.Inner", "parcelable Outer.Inner;", status: "declaration-only"),
              Declaration("parcelable", "a.Other", "parcelable Other cpp_header \"o.h\";", ordinal: 1, status: "declaration-only") ],
            "test_round7.py:test_all_forward_declarations_keep_dotted_identity;test_multiple_aidl_declarations_are_published"),

            Case("AIDL-06", "aidl", "package a; parcelable P cpp_header \"x{y}.h\";",
            [ Declaration("parcelable", "a.P", "parcelable P cpp_header \"x{y}.h\";", status: "declaration-only") ],
            "test_round7.py:test_cpp_header_braces_are_not_a_declaration_body"),

            Case("AIDL-07", "aidl", "package a;\n@Note(\"interface First {}\") interface First { void f(); }\nparcelable Outer.Inner;\ninterface Second { void g(); }",
            [ Declaration("interface", "a.First", "@Note(\"interface First {}\") interface First", 2, annotations: Array(Annotation("Note", "\"interface First {}\""))),
              Method("a.First", "f", "void f()", 2),
              Declaration("parcelable", "a.Outer.Inner", "parcelable Outer.Inner;", 3, 1, status: "declaration-only"),
              Declaration("interface", "a.Second", "interface Second", 4, 2), Method("a.Second", "g", "void g()", 4) ],
            "test_round7.py:test_top_level_lexing_ignores_quoted_decoys_and_retains_lines"),

            Case("AIDL-08", "aidl", "package a; /* prior comment */\nparcelable Outer { parcelable Inner { int x; } int after; }",
            [ Declaration("parcelable", "a.Outer", "/* prior comment */\nparcelable Outer", 2), Field("a.Outer", "after", "int", "int after", 2),
              Declaration("parcelable", "a.Outer.Inner", "parcelable Inner", 2, 1, "a.Outer", 1),
              Field("a.Outer.Inner", "x", "int", "int x", 2) ],
            "test_round8.py:test_nested_structured_types_do_not_swallow_fields"),

            Case("AIDL-09", "aidl", "package a;\ninterface I {\n parcelable P { int x; }\n void f();\n enum E { A=1, B=2 }\n union U { P p; int n; }\n interface J { parcelable Q { int y; } void g(); }\n}",
            [ Declaration("interface", "a.I", "interface I", 2), Method("a.I", "f", "void f()", 4),
              Declaration("parcelable", "a.I.P", "parcelable P", 3, 1, "a.I", 1), Field("a.I.P", "x", "int", "int x", 3),
              Declaration("enum", "a.I.E", "enum E", 5, 2, "a.I", 1), Field("a.I.E", "A", "a.I.E", "A=1", 5, "enum", 0, "1"), Field("a.I.E", "B", "a.I.E", "B=2", 5, "enum", 1, "2"),
              Declaration("union", "a.I.U", "union U", 6, 3, "a.I", 1), Field("a.I.U", "p", "P", "P p", 6, "union"), Field("a.I.U", "n", "int", "int n", 6, "union", 1),
              Declaration("interface", "a.I.J", "interface J", 7, 4, "a.I", 1), Method("a.I.J", "g", "void g()", 7),
              Declaration("parcelable", "a.I.J.Q", "parcelable Q", 7, 5, "a.I.J", 2), Field("a.I.J.Q", "y", "int", "int y", 7) ],
            "test_round8.py:test_nested_aidl_types_and_following_methods_published"),

            Case("AIDL-10", "aidl", "package a; interface I { const int C=3; void x(); }",
            [ Declaration("interface", "a.I", "interface I"), Field("a.I", "C", "int", "const int C=3", kind: "interface", constant: "3", isConstant: true), Method("a.I", "x", "void x()", ordinal: 1) ],
            "test_aidl_members.py:test_interface_constant_is_metadata_and_repeated_lines_are_distinct"),

            Case("AIDL-11", "aidl", "package a; parcelable P {\n int x;\n int y;\n}",
            [ Declaration("parcelable", "a.P", "parcelable P"), Field("a.P", "x", "int", "int x", 2), Field("a.P", "y", "int", "int y", 3, ordinal: 1) ],
            "test_aidl_members.py:test_interface_constant_is_metadata_and_repeated_lines_are_distinct"),

            Case("AIDL-12", "aidl", "package a;\ninterface I {\n const String NOTE=\"oneway interface NotADeclaration\";\n @Marker(value=\"oneway\") void ordinary();\n oneway void explicitAsync();\n oneway interface Nested { void nestedAsync(); }\n interface Plain { void nestedOrdinary(); }\n}",
            [ Declaration("interface", "a.I", "interface I", 2),
              Field("a.I", "NOTE", "String", "const String NOTE=\"oneway interface NotADeclaration\"", 3, "interface", 0, "\"oneway interface NotADeclaration\"", true),
              Method("a.I", "ordinary", "@Marker(value=\"oneway\") void ordinary()", 4, ordinal: 1, annotations: Array(Annotation("Marker", "value=\"oneway\""))),
              Method("a.I", "explicitAsync", "oneway void explicitAsync()", 5, oneway: true, ordinal: 2),
              Declaration("interface", "a.I.Nested", "oneway interface Nested", 6, 1, "a.I", 1, true), Method("a.I.Nested", "nestedAsync", "void nestedAsync()", 6, interfaceOneway: true),
              Declaration("interface", "a.I.Plain", "interface Plain", 7, 2, "a.I", 1), Method("a.I.Plain", "nestedOrdinary", "void nestedOrdinary()", 7) ],
            "test_round11.py:test_builder_async_flags_are_scoped_and_ignore_quoted_or_nested_tokens")
        };

        cases.Add(Case("AIDL-19", "aidl", "package a; interface I { /* we're ignoring commas, and semicolons; */\n enum E { A, // ignored, comment;\n B, /* another, comment */ }\n const String X=\"semi;comma,\"; void f(); // trailing comment;\n } interface Empty { // comment-only body;\n }",
            [Declaration("interface", "a.I", "interface I"), Field("a.I", "X", "String", "const String X=\"semi;comma,\"", 4, "interface", 0, "\"semi;comma,\"", true),
             Method("a.I", "f", "void f()", 4, ordinal: 1), Declaration("enum", "a.I.E", "enum E", 2, 1, "a.I", 1),
             Field("a.I.E", "A", "a.I.E", "A", 2, "enum"), Field("a.I.E", "B", "a.I.E", "B", 3, "enum", 1),
             Declaration("interface", "a.Empty", "interface Empty", 5, 2)],
            "test_round8.py:test_aidl_reconciliation_comments_are_not_statements; independent3declarations+4statements=7records"));

        var outerUnknown = Unsupported("fixture", "a.I", "broken thing", 1, "AIDL non-method statement preserved but not interpreted");
        outerUnknown.Remove("canonical");
        cases.Add(Case("AIDL-20", "aidl", "package a; interface I { parcelable P { int x; } broken thing; void f(); }",
            [Declaration("interface", "a.I", "interface I", status: "partial"), outerUnknown, Method("a.I", "f", "void f()", ordinal: 1),
             Declaration("parcelable", "a.I.P", "parcelable P", ordinal: 1, parent: "a.I", depth: 1), Field("a.I.P", "x", "int", "int x")],
            "test_round8.py:test_nested_aidl_unknown_statement_remains_visible", StructuralParseStatus.Partial, "aidl-semantic-gap"));

        AddFailureCases(cases);
        AddSignatureCases(cases);
        return cases;
    }

    private static JsonObject Unsupported(string canonical, string? owner, string raw, int line, string reason,
        JsonObject? extras = null)
    {
        extras ??= new(); extras["parse_error"] = reason;
        return Row("unsupported", canonical, owner, "unparsed", raw, line, extras: extras);
    }

    private static void AddFailureCases(List<SourceExtractionCase> cases)
    {
        // Canonical hashes themselves will be checked in the hash/serialization
        // increment. This slice checks exact preserved raw/ownership/issue data.
        var unknown = Row("unsupported", "a.P#unparsed:fixture-unknown", "a.P", "unparsed", "new_syntax", 1,
            extras: new() { ["parse_error"] = "Unresolved AIDL field declaration", ["declared_ordinal"] = 0 });
        unknown.Remove("canonical");
        cases.Add(Case("AIDL-13", "aidl", "package a; parcelable P { new_syntax; int good; }",
            [Declaration("parcelable", "a.P", "parcelable P", status: "field-metadata-partial"), unknown, Field("a.P", "good", "int", "int good", ordinal: 1)],
            "test_aidl_members.py:test_external_parcelable_and_unknown_nested_schema_stay_visible", StructuralParseStatus.Partial, "aidl-semantic-gap"));
        foreach (var (id, text) in new[]
        {
            ("AIDL-14", "package a; interface I { void x();"),
            ("AIDL-15", "package a; interface I { parcelable P { int x;")
        })
        {
            var declaration = Declaration("interface", "a.I", "interface I", status: "unsupported");
            declaration["kind"] = "unsupported";
            declaration["extras"]!["parse_error"] = "Unbalanced declaration body";
            cases.Add(Case(id, "aidl", text, [declaration],
                "test_round5.py:test_unbalanced_interface_is_retained_as_failed_schema;test_round8.py:test_malformed_nested_aidl_keeps_full_source_and_reason",
                StructuralParseStatus.Malformed, "aidl-semantic-gap", "aidl-reconciliation"));
        }

        var unrecognized = Unsupported("unparsed-source:fixture", null, "unsupported source", 1, "No supported top-level AIDL declaration");
        unrecognized.Remove("canonical"); unrecognized["package"] = ""; unrecognized["name"] = "AIDL-16.aidl";
        cases.Add(Case("AIDL-16", "aidl", "unsupported source", [unrecognized], "parsers.py:aidl no declaration branch",
            StructuralParseStatus.Unsupported, "aidl-semantic-gap"));

        // Fragment lacks statement separation; it must never become a callable
        // with a fabricated return type. Exact drop accounting is independent.
        cases.Add(Case("AIDL-17", "aidl", "package a; interface I { parcelable P { int x; } void f(); }",
            [Declaration("interface", "a.I", "interface I"), Method("a.I", "f", "void f()"),
             Declaration("parcelable", "a.I.P", "parcelable P", ordinal: 1, parent: "a.I", depth: 1), Field("a.I.P", "x", "int", "int x")],
            "test_round8.py:test_aidl_merged_head_rejected_instead_of_fabricated_type; nested masking prevents merged callable"));

        cases.Add(Case("AIDL-18", "aidl", "package a; interface I;",
            [Declaration("interface", "a.I", "interface I;", status: "declaration-only")],
            "test_round5.py:test_interface_failure_status_is_not_promoted"));
    }

    private static void AddSignatureCases(List<SourceExtractionCase> cases)
    {
        var package = Row("package", "a", null, "a", "package a {", 1);
        var annotation = Row("annotation", "a.Ann", "a.Ann", "Ann", "public @interface Ann {", 2);
        var value = Row("method", "a.Ann#value()", "a.Ann", "value", "method public abstract String value();", 3, "String");
        var type = Row("class", "a.Outer.Inner", "a.Outer.Inner", "Outer.Inner", "public class Outer.Inner<T> {", 5);
        var ctor = Row("constructor", "a.Outer.Inner#<init>()", "a.Outer.Inner", "Inner", "ctor public Outer.Inner();", 6);
        var method = Row("method", "a.Outer.Inner#go(java.util.Map<String,java.util.List<T>>,int)", "a.Outer.Inner", "go",
            "method @RequiresPermission(\"example.permission.P\") public <T extends java.lang.Object> java.util.List<T> go(@Nullable java.util.Map<String,java.util.List<T>>, int);", 7,
            "java.util.List<T>", Array(Parameter("java.util.Map<String,java.util.List<T>>", annotations: [Annotation("Nullable")] ), Parameter("int")),
            Array(Annotation("RequiresPermission", "\"example.permission.P\"")));
        var overload = Row("method", "a.Outer.Inner#go(int)", "a.Outer.Inner", "go", "method public void go(int);", 8, "void", Array(Parameter("int")));
        var field = Row("field", "a.Outer.Inner#S", "a.Outer.Inner", "S", "field public static final String S = \"hello // no comment\";", 9, "String",
            extras: new() { ["constant_raw"] = "\"hello // no comment\"", ["enum_constant"] = false });
        cases.Add(Case("SIG-01", "signature", "package a {\n public @interface Ann {\n method public abstract String value();\n }\n public class Outer.Inner<T> {\n ctor public Outer.Inner();\n method @RequiresPermission(\"example.permission.P\") public <T extends java.lang.Object> java.util.List<T> go(@Nullable java.util.Map<String,java.util.List<T>>, int);\n method public void go(int);\n field public static final String S = \"hello // no comment\";\n }\n }",
            [package, annotation, value, type, ctor, method, overload, field], "test_parsers.py:test_overloads_and_annotations"));

        var unknown = Unsupported("unparsed:fixture", null, "strange new declaration;", 2, "Member without owner"); unknown.Remove("canonical");
        cases.Add(Case("SIG-02", "signature", "package a {\n strange new declaration;\n}",
            [(JsonObject)package.DeepClone(), unknown], "test_parsers.py:test_source_unknown_retained", StructuralParseStatus.Partial, "signature-unsupported"));

        var orphan = Unsupported("unparsed:fixture", null, "method public void orphan();", 5, "Member without owner"); orphan.Remove("canonical");
        cases.Add(Case("SIG-03", "signature", "package a {\n public class A {\n method public void x();\n }\n method public void orphan();\n}",
            [(JsonObject)package.DeepClone(), Row("class", "a.A", "a.A", "A", "public class A {", 2),
             Row("method", "a.A#x()", "a.A", "x", "method public void x();", 3, "void"), orphan],
            "test_parsers.py:test_owner_reset_after_type", StructuralParseStatus.Partial, "signature-unsupported"));

        cases.Add(Case("SIG-04", "signature", "package a {\n public class A {\n method public void x(@RequiresPermission(\"example.permission.P\") String);\n }\n}",
            [(JsonObject)package.DeepClone(), Row("class", "a.A", "a.A", "A", "public class A {", 2),
             Row("method", "a.A#x(String)", "a.A", "x", "method public void x(@RequiresPermission(\"example.permission.P\") String);", 3,
                 "void", Array(Parameter("String", annotations: [Annotation("RequiresPermission", "\"example.permission.P\"")])) )],
            "test_parsers.py:test_parameter_permission_not_callable (annotation scope; permission AST separate pending slice)"));
    }

    public static void AssertContract(SourceExtractionCase test, ISourceExtractor provider)
    {
        var document = test.Document();
        var originalBytes = document.Bytes.ToArray();
        Assert.Equal(test.Kind, provider.Kind);
        var result = provider.Extract(document);
        Assert.Equal(originalBytes, document.Bytes.ToArray());
        Assert.Equal(test.Status, result.StructuralStatus);
        Assert.Equal(test.ExpectedRecords.Count, result.Records.Count);
        for (var i = 0; i < test.ExpectedRecords.Count; i++) AssertNode(test.ExpectedRecords[i], result.Records[i], $"records[{i}]");
        Assert.Equal(test.IssueCodes.Order(StringComparer.Ordinal), result.Issues.Select(x => x.Code).Order(StringComparer.Ordinal));
        Assert.All(result.Issues, issue =>
        {
            Assert.False(string.IsNullOrWhiteSpace(issue.Reason));
            Assert.Equal(issue.StartByte.HasValue, issue.EndByte.HasValue);
            if (issue.StartByte.HasValue)
            {
                Assert.InRange(issue.StartByte.Value, 0, originalBytes.Length);
                Assert.InRange(issue.EndByte!.Value, issue.StartByte.Value, originalBytes.Length);
            }
        });
    }

    private static void AssertNode(JsonNode? expected, JsonNode? actual, string path)
    {
        if (expected is JsonObject expectedObject)
        {
            var actualObject = Assert.IsType<JsonObject>(actual);
            foreach (var item in expectedObject)
            {
                Assert.True(actualObject.ContainsKey(item.Key), $"Missing {path}.{item.Key}");
                AssertNode(item.Value, actualObject[item.Key], path + "." + item.Key);
            }
        }
        else if (expected is JsonArray expectedArray)
        {
            var actualArray = Assert.IsType<JsonArray>(actual); Assert.Equal(expectedArray.Count, actualArray.Count);
            for (var i = 0; i < expectedArray.Count; i++) AssertNode(expectedArray[i], actualArray[i], path + $"[{i}]");
        }
        else Assert.True(JsonNode.DeepEquals(expected, actual), $"Value differs at {path}: expected {expected}, actual {actual}");
    }
}
