using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record HidlContractCase(SourceExtractionCase Source, JsonObject Metadata)
{
    public ExtractionResult CorrectResult() => Source.CorrectResult() with { Metadata = (JsonObject)Metadata.DeepClone() };
    public SourceExtractionCase MetadataCase() => Source with { ExpectedRecords = [Metadata] };
}

// FR-BUILD-004/TEST-BUILD-004, S-AC016. All14legacy HIDL test methods
// contribute literal input/assertion data; no Python parser is executed.
public static class HidlContractCases
{
    private const string Limitation = "No compiler validation, complete type resolution, implicit inherited method expansion, Android/module availability or wire transaction inference";
    public static IReadOnlyList<HidlContractCase> All { get; } = Create();
    public static IEnumerable<object[]> Cases => All.Select(x => new object[] { x.Source.Id });
    public static IEnumerable<object[]> RecordMutations => All.SelectMany(x => SourceMutationCases.For(x.Source));
    public static IEnumerable<object[]> MetadataMutations => All.SelectMany(x => SourceMutationCases.For(x.MetadataCase()));
    public static HidlContractCase Get(string id) => All.Single(x => x.Source.Id == id);

    private static JsonArray A(params JsonNode?[] values) => new(values);
    private static JsonObject Parameter(string type, string name, string raw) => new()
    { ["type"] = type, ["name"] = name, ["raw"] = raw, ["annotations"] = new JsonArray(), ["all_annotations"] = new JsonArray() };
    private static JsonObject Row(string kind, string name, string? owner, string package, string canonical, int line = 1,
        string? raw = null, string? returned = null, JsonArray? parameters = null, JsonObject? extra = null, JsonArray? annotations = null,
        int? start = null, int? end = null)
    {
        var extensions = new JsonObject
        {
            ["hidl_package_version"] = package.Contains('@') ? package[(package.IndexOf('@') + 1)..] : null,
            ["android_availability"] = "unknown", ["wire_transaction_id"] = null, ["wire_codec_status"] = "unknown",
            ["semantic_status"] = "Declared-source structure only; compiler validity, imports and runtime registration unverified"
        };
        if (extra is not null) foreach (var property in extra) extensions[property.Key] = property.Value?.DeepClone();
        if (start.HasValue) extensions["source_character_span"] = A(start.Value, end!.Value);
        var row = new JsonObject
        {
            ["kind"] = kind, ["name"] = name, ["owner"] = owner, ["package"] = package, ["canonical"] = canonical,
            ["line"] = line, ["parameters"] = parameters ?? new JsonArray(), ["return_type"] = returned,
            ["annotations"] = new JsonArray(), ["all_annotations"] = annotations ?? new JsonArray(), ["extras"] = extensions
        };
        if (raw is not null) row["raw"] = raw;
        return row;
    }
    private static JsonObject Package(string package = "p@1.0", int line = 1, string? raw = null) =>
        Row("package", package, null, package, package, line, raw ?? "package " + package + ";");
    private static JsonObject Type(string name, string full, string kind = "interface", string? enclosing = null, string package = "p@1.0", int line = 1,
        string? raw = null, JsonArray? annotations = null, string storage = "")
    {
        var extra = new JsonObject { ["hidl_declaration_kind"] = kind, ["enclosing_type"] = enclosing, ["hidl_qualified_name"] = full };
        if (kind == "interface") { extra["declared_extends"] = ""; extra["descriptor_source_name"] = full; }
        if (kind == "enum") extra["storage_type_raw"] = storage;
        return Row(kind == "interface" ? "interface" : kind == "enum" ? "enum" : "type", name, full, package, full, line, raw,
            extra: extra, annotations: annotations);
    }
    private static JsonObject Field(string name, string owner, string type, string package = "p@1.0", int line = 1, string? raw = null, string kind = "field", string? value = null) =>
        Row("field", name, owner, package, owner + "::" + name, line, raw, type,
            extra: kind == "enum-value" ? new() { ["hidl_declaration_kind"] = kind, ["constant_expression_raw"] = value } : new() { ["hidl_declaration_kind"] = kind });
    private static JsonObject Method(string name = "m", string owner = "p@1.0::I", string package = "p@1.0", int line = 1, string? raw = null,
        JsonArray? parameters = null, JsonArray? results = null, bool oneway = false, JsonArray? annotations = null)
    {
        parameters ??= new();
        return Row("method", name, owner, package, owner + "::" + name + "(" + string.Join(',', parameters.Select(x => x!["type"]!.GetValue<string>())) + ")", line,
            raw, parameters: parameters, extra: new() { ["hidl_declaration_kind"] = "method", ["results"] = results ?? new(), ["oneway"] = oneway }, annotations: annotations);
    }
    private static JsonObject Unsupported(string name, string? owner, string raw, string reason, string package = "p@1.0", int line = 1) =>
        Row("unsupported", name, owner, package, (owner ?? (package.Length > 0 ? package : "unresolved-package")) + "::" + name, line, raw,
            extra: new() { ["parse_error"] = reason });
    private static HidlContractCase Case(string id, string text, JsonObject[] records, string evidence,
        StructuralParseStatus status = StructuralParseStatus.Clean, JsonArray? imports = null, bool packageKnown = true)
    {
        var unsupported = records.Where(x => x["kind"]!.GetValue<string>() == "unsupported").ToArray();
        var metadata = new JsonObject
        {
            ["source_complete"] = false, ["imports"] = imports ?? new JsonArray(),
            ["unsupported"] = new JsonArray(unsupported.Select(x => (JsonNode)new JsonObject
                { ["line"] = x["line"]!.DeepClone(), ["raw"] = x["raw"]!.DeepClone(), ["reason"] = x["extras"]!["parse_error"]!.DeepClone() }).ToArray())
        };
        if (packageKnown) { metadata["structural_statement_parse_complete"] = unsupported.Length == 0; metadata["limitation"] = Limitation; }
        return new(new(id, "hidl-source", text, status, records, Enumerable.Repeat("hidl-unsupported", unsupported.Length).ToArray(), evidence), metadata);
    }

    private static IReadOnlyList<HidlContractCase> Create()
    {
        const string package = "android.hardware.test@1.0";
        var rows = new List<HidlContractCase>
        {
            Case("HIDL-01", "package android.hardware.test@1.0; interface IFoo { struct Point { int32_t x; }; get(vec<Point> points) generates (bool ok, Point[3] triangle); };",
            [Package(package), Type("IFoo", package + "::IFoo", package: package, raw: " interface IFoo { struct Point { int32_t x; }; get(vec<Point> points) generates (bool ok, Point[3] triangle); }"),
             Type("Point", package + "::IFoo.Point", "struct", package + "::IFoo", package, raw: " struct Point { int32_t x; }"),
             Field("x", package + "::IFoo.Point", "int32_t", package, raw: " int32_t x"),
             Method("get", package + "::IFoo", package, raw: " get(vec<Point> points) generates (bool ok, Point[3] triangle)", parameters: A(Parameter("vec<Point>", "points", "vec<Point> points")),
                 results: A(Parameter("bool", "ok", "bool ok"), Parameter("Point[3]", "triangle", " Point[3] triangle")))],
            "test_interface_nested_type_and_following_method; test_type_canonical_does_not_repeat_own_name"),

            Case("HIDL-02", "package p@1.0; interface I { enum Flags : uint32_t { A=1<<2, B=A|2, C }; ping(); };",
            [Package(), Type("I", "p@1.0::I"), Type("Flags", "p@1.0::I.Flags", "enum", "p@1.0::I", storage: ": uint32_t"),
             Field("A", "p@1.0::I.Flags", "p@1.0::I.Flags", kind: "enum-value", value: "1<<2"),
             Field("B", "p@1.0::I.Flags", "p@1.0::I.Flags", kind: "enum-value", value: "A|2"),
             Field("C", "p@1.0::I.Flags", "p@1.0::I.Flags", kind: "enum-value"), Method("ping")],
            "test_shifts_dont_consume_enum_values_or_following_method"),

            Case("HIDL-03", "package p@1.0; interface I { m(vec<vec<int32_t>> a, bitfield<Flags> b); };",
            [Package(), Type("I", "p@1.0::I"), Method(parameters: A(Parameter("vec<vec<int32_t>>", "a", "vec<vec<int32_t>> a"), Parameter("bitfield<Flags>", "b", " bitfield<Flags> b")))],
            "test_nested_template_comma_separation"),

            Case("HIDL-04", "package p@1.0; @Callflow(next={\"a;\",\"b\"}) interface I { @entry m(); };",
            [Package(), Type("I", "p@1.0::I", annotations: A(new JsonObject { ["name"] = "Callflow", ["arguments"] = "next={\"a;\",\"b\"}", ["raw"] = "@Callflow(next={\"a;\",\"b\"})", ["semantics"] = "unknown HIDL source annotation" })),
             Method(annotations: A(new JsonObject { ["name"] = "entry", ["arguments"] = "", ["raw"] = "@entry ", ["semantics"] = "unknown HIDL source annotation" }))],
            "test_annotations_with_braces_and_semicolon_string_are_not_bodies"),

            Case("HIDL-06", "package p@1.0; mystery ?;", [Package(), Unsupported("unresolved-hidl-statement", null, " mystery ?", "Unsupported HIDL declaration retained verbatim")],
                "test_unknown_statement_and_missing_package_retained (unknown statement)", StructuralParseStatus.Partial),
            Case("HIDL-07", "interface I {};", [Unsupported("unresolved-hidl-source", null, "interface I {};", "Required HIDL package/version declaration absent or malformed", "")],
                "test_unknown_statement_and_missing_package_retained (missing package)", StructuralParseStatus.Unsupported, packageKnown: false),
            Case("HIDL-08", "package p@1.0; interface I { oneway m() generates (bool ok); };",
            [Package(), Type("I", "p@1.0::I"), Unsupported("m", "p@1.0::I", " oneway m() generates (bool ok)", "HIDL oneway method cannot declare generated results")],
                "test_oneway_with_results_not_silently_accepted", StructuralParseStatus.Partial),

            Case("HIDL-09", "package p@2.7; interface I {};", [Package("p@2.7"), Type("I", "p@2.7::I", package: "p@2.7")],
                "test_source_hidl_version_is_not_android_availability"),
            Case("HIDL-10", "package p@1.0; typedef vec<int32_t> Values; safe_union Value { int32_t number; string text; };",
            [Package(), Row("type", "Values", "p@1.0::Values", "p@1.0", "p@1.0::Values", extra: new() { ["hidl_declaration_kind"] = "typedef", ["target_type_raw"] = "vec<int32_t>" }),
             Type("Value", "p@1.0::Value", "safe_union"), Field("number", "p@1.0::Value", "int32_t"), Field("text", "p@1.0::Value", "string")],
                "test_typedef_and_safe_union"),
            Case("HIDL-11", "package p@1.0;\ninterface I {\n\n m();\n};", [Package(), Type("I", "p@1.0::I", line: 2), Method(line: 4)],
                "test_line_of_method_after_leading_blank_lines"),
            Case("HIDL-12", "package p@1.0; interface I {}", [Package(), Unsupported("unterminated-hidl-statement", null, " interface I {}", "Required statement semicolon absent; raw syntax retained")],
                "test_missing_semicolon_and_nested_interface_retained_unknown (semicolon)", StructuralParseStatus.Malformed),
            Case("HIDL-13", "package p@1.0; interface I { interface INested {}; };",
            [Package(), Type("I", "p@1.0::I"), Unsupported("INested", "p@1.0::I", " interface INested {}", "HIDL interfaces require global scope and I-prefixed name")],
                "test_missing_semicolon_and_nested_interface_retained_unknown (global scope)", StructuralParseStatus.Partial),
            Case("HIDL-14", "package p@1.0; struct Holder { interface instance; };",
            [Package(), Type("Holder", "p@1.0::Holder", "struct"), Field("instance", "p@1.0::Holder", "interface")],
                "test_interface_special_type_is_a_field_not_nested_declaration"),
            Case("HIDL-15", "package p@1.0; interface I { m(vec<uint8_t>input) generates (uint32_t[3]output); };",
            [Package(), Type("I", "p@1.0::I"), Method(parameters: A(Parameter("vec<uint8_t>", "input", "vec<uint8_t>input")), results: A(Parameter("uint32_t[3]", "output", "uint32_t[3]output")))],
                "test_type_delimiter_can_touch_variable_name"),
            Case("HIDL-16", "package p@1.0; import q@2.0::IBar; interface I {};", [Package(), Type("I", "p@1.0::I")],
                "hidl_source.py:literal imports retained as source facts", imports: A(new JsonObject { ["raw"] = " import q@2.0::IBar", ["target"] = "q@2.0::IBar", ["line"] = 1 })),
            Case("HIDL-17", "package p@1.0; interface I { m() generates (); };",
            [Package(), Type("I", "p@1.0::I"), Unsupported("m", "p@1.0::I", " m() generates ()", "Empty HIDL generates clause")],
                "hidl_source.py:empty generates clause remains unsupported", StructuralParseStatus.Partial),
            Case("HIDL-18", "package p@1.0; interface I { m(int32_t x, string x); };",
            [Package(), Type("I", "p@1.0::I"), Unsupported("m", "p@1.0::I", " m(int32_t x, string x)", "Duplicate typed variable names")],
                "hidl_source.py:duplicate typed variable names retained unsupported", StructuralParseStatus.Partial)
        };
        var leading = new string('\n', 300) + "// interface IFake {};\n";
        var locatedPackage = Package(line: 302); locatedPackage.Remove("raw");
        rows.Add(Case("HIDL-05", leading + "package p@1.0; interface I {};", [locatedPackage, Type("I", "p@1.0::I", line: 302)], "test_comments_and_line_offsets"));
        var spanPackage = Package(); spanPackage["extras"]!["source_character_span"] = A(0, 14);
        var spanInterface = Type("I", "p@1.0::I", raw: " interface I {}"); spanInterface["extras"]!["source_character_span"] = A(14, 29);
        rows.Add(Case("HIDL-19", "package p@1.0; interface I {};", [spanPackage, spanInterface], "Independent literal source span boundary vector"));
        rows.Add(Case("HIDL-20", "package p@1.0; interface I { struct Point { int32_t x; }; };",
            [Package(), Type("I", "p@1.0::I"), Type("Point", "p@1.0::I.Point", "struct", "p@1.0::I"), Field("x", "p@1.0::I.Point", "int32_t")],
            "test_type_canonical_does_not_repeat_own_name (exact legacy input)"));
        rows.Add(Case("HIDL-21", "package p@1.0;", [Package()], "Empty valid package scope is distinct from missing source/complete global API"));
        var extended = Type("I", "p@1.0::I"); extended["extras"]!["declared_extends"] = "extends q@2.0::IBase"; extended["extras"]!["descriptor_source_name"] = "p@1.0::I";
        rows.Add(Case("HIDL-22", "package p@1.0; interface I extends q@2.0::IBase {};", [Package(), extended], "Declared extends spelling retained without inherited method expansion"));
        var unicodePackage = Package(line: 2, raw: "// 😀\npackage p@1.0;"); unicodePackage["extras"]!["source_character_span"] = A(0, 19);
        var unicodeInterface = Type("I", "p@1.0::I", line: 2, raw: " interface I {}"); unicodeInterface["extras"]!["source_character_span"] = A(19, 34);
        rows.Add(Case("HIDL-23", "// 😀\npackage p@1.0; interface I {};", [unicodePackage, unicodeInterface], "Pinned Unicode code-point source locators differ from UTF16 offsets"));
        rows.Add(Case("HIDL-24", "package p@1.0; interface Foo {};", [Package(), Unsupported("Foo", null, " interface Foo {}", "HIDL interfaces require global scope and I-prefixed name")],
            "hidl_source.py rejects non-I-prefixed interfaces explicitly", StructuralParseStatus.Partial));
        rows.Add(Case("HIDL-25", "package p@1.0; struct S;", [Package(), Unsupported("S", null, " struct S", "Missing HIDL declaration body")],
            "hidl_source.py retains declaration without body", StructuralParseStatus.Partial));
        rows.Add(Case("HIDL-26", "package p@1.0; struct S { struct N { int32_t x; } field; };",
            [Package(), Type("S", "p@1.0::S", "struct"), Type("N", "p@1.0::S.N", "struct", "p@1.0::S"), Field("x", "p@1.0::S.N", "int32_t"),
             Field("field", "p@1.0::S", "p@1.0::S.N", raw: " field", kind: "inline-compound-field")],
            "hidl_source.py inline compound field suffix is retained separately"));
        rows.Add(Case("HIDL-27", "package p@1.0; interface I {} extra;",
            [Package(), Type("I", "p@1.0::I"), Unsupported("trailing-declaration-syntax", null, " extra", "Unrecognized suffix after HIDL type body")],
            "hidl_source.py does not silently drop unknown trailing type syntax", StructuralParseStatus.Partial));
        rows.Add(Case("HIDL-28", "package p@1.0; enum E { ??? };",
            [Package(), Type("E", "p@1.0::E", "enum"), Unsupported("unresolved-enum-value", "p@1.0::E", " ??? ", "Unknown HIDL enum value")],
            "hidl_source.py unknown enum expression retained verbatim", StructuralParseStatus.Partial));
        rows.Add(Case("HIDL-29", "package p@1.0; interface I { m(int32_t); };",
            [Package(), Type("I", "p@1.0::I"), Unsupported("m", "p@1.0::I", " m(int32_t)", "Unrecognized typed variable int32_t")],
            "hidl_source.py malformed typed variable retained as unsupported method", StructuralParseStatus.Partial));
        return rows;
    }

    public static void AssertContract(HidlContractCase test, ISourceExtractor provider)
    {
        Assert.Equal(test.Source.Kind, provider.Kind);
        ExtractionResult? observed = null;
        var recording = new ScriptedSourceExtractor("hidl-source", source => observed = provider.Extract(source));
        SourceExtractionContractCases.AssertContract(test.Source, recording);
        Assert.NotNull(observed);
        SourceExtractionContractCases.AssertSubset(test.Metadata, observed.Metadata, "hidl-metadata");
    }
}
