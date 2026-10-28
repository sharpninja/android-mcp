using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

// FR-BUILD-004/TEST-BUILD-004, S-AC017. Pinned legacy expectations, authored
// before SDK production code. No parser is used to generate expected values.
public static class SdkXmlContractCases
{
    public static IReadOnlyList<SourceExtractionCase> All { get; } = Create();
    public static IEnumerable<object[]> Cases => All.Select(x => new object[] { x.Id });
    public static IEnumerable<object[]> Mutations => All.SelectMany(SourceMutationCases.For);
    public static SourceExtractionCase Get(string id) => All.Single(x => x.Id == id);

    public static void AssertContract(SourceExtractionCase test, ISourceExtractor provider)
    {
        ExtractionResult? observed = null;
        var recording = new ScriptedSourceExtractor(provider.Kind, source => observed = provider.Extract(source));
        SourceExtractionContractCases.AssertContract(test, recording);
        Assert.NotNull(observed);
        for (var i = 0; i < test.ExpectedRecords.Count; i++)
        {
            foreach (var name in new[] { "api_index_attributes", "api_lifecycle", "container_api_lifecycle" })
                Assert.True(JsonNode.DeepEquals(test.ExpectedRecords[i]["extras"]![name], observed.Records[i]["extras"]![name]),
                    $"Literal attribute set changed for {test.Id}/record{i}/{name}; absent attributes must remain absent.");
        }
    }

    private static JsonObject Attributes(params (string Name, string Value)[] items) => new(items.Select(x =>
        new KeyValuePair<string, JsonNode?>(x.Name, JsonValue.Create(x.Value))));

    private static JsonObject Row(string kind, string owner, string name, string canonical, string raw, int line,
        JsonObject literal, JsonObject ownerLiteral, string lifecycle = "current-at-target", string? returnType = null,
        string[]? parameters = null, int memberOrdinal = 0, JsonArray? extensions = null, JsonArray? supertypes = null,
        string? lifecycleReason = null, string? parseError = null)
    {
        var dotted = owner.Replace('/', '.').Replace('$', '.');
        var package = owner.Contains('/') ? owner[..owner.LastIndexOf('/')].Replace('/', '.') : "";
        var extra = new JsonObject
        {
            ["jvm_owner"] = owner, ["api_index_attributes"] = literal.DeepClone(), ["api_lifecycle"] = literal.DeepClone(),
            ["container_api_lifecycle"] = ownerLiteral.DeepClone(), ["sdk_extensions"] = extensions ?? new(),
            ["lifecycle_status"] = lifecycle,
            ["public_sdk_membership"] = lifecycle == "current-at-target" ? "declared-current-in-official-SDK-API-index" : "declared-in-official-SDK-API-index-not-current-at-target",
            ["target_platform_api"] = "37.2",
            ["source_kind_semantics"] = "full published API lifecycle inventory; not complete Java signature",
            ["index_ordinal"] = 1, ["member_ordinal"] = memberOrdinal
        };
        if (kind == "type")
        {
            extra["type_kind_status"] = "unknown";
            extra["type_kind_reason"] = "API index omits interface/class/annotation distinction";
            extra["supertypes"] = supertypes ?? new();
        }
        if (kind == "field") extra["field_type_status"] = "unknown";
        if (lifecycleReason is not null) extra["lifecycle_unknown_reason"] = lifecycleReason;
        if (parseError is not null) extra["parse_error"] = parseError;
        return new()
        {
            ["kind"] = kind, ["canonical"] = canonical, ["owner"] = dotted, ["package"] = package, ["name"] = name,
            ["return_type"] = returnType,
            ["parameters"] = new JsonArray((parameters ?? []).Select(type => (JsonNode)new JsonObject
                { ["type"] = type, ["name"] = null, ["direction"] = null, ["annotations"] = new JsonArray() }).ToArray()),
            ["annotations"] = new JsonArray(), ["all_annotations"] = new JsonArray(), ["raw"] = raw, ["line"] = line, ["extras"] = extra
        };
    }

    private static JsonObject Type(string owner, JsonObject attributes, string raw, int line = 1, string lifecycle = "current-at-target",
        JsonArray? extensions = null, JsonArray? supertypes = null, string? lifecycleReason = null) =>
        Row("type", owner, owner.Replace('/', '.').Replace('$', '.').Split('.')[^1], owner, raw, line, attributes, attributes,
            lifecycle, extensions: extensions, supertypes: supertypes, lifecycleReason: lifecycleReason);

    private static JsonObject Member(string owner, string descriptor, JsonObject literal, JsonObject container, string raw,
        string kind = "method", string? returnType = "void", string[]? parameters = null, string lifecycle = "current-at-target",
        int ordinal = 1, int line = 1, JsonArray? extensions = null, string? lifecycleReason = null, string? parseError = null)
    {
        var name = kind is "method" or "constructor" ? descriptor[..descriptor.IndexOf('(')] : descriptor;
        return Row(kind, owner, name, owner + "#" + descriptor, raw, line, literal, container, lifecycle, returnType,
            parameters, ordinal, extensions, lifecycleReason: lifecycleReason, parseError: parseError);
    }

    private static SourceExtractionCase Case(string id, string text, JsonObject[] rows, string evidence,
        StructuralParseStatus status = StructuralParseStatus.Clean, params string[] issues) =>
        new(id, "sdk-api-index", text, status, rows, issues, evidence);

    private static IReadOnlyList<SourceExtractionCase> Create()
    {
        var owner = Attributes(("name", "a/Outer$Inner"), ("since", "37.1"));
        var plain = Attributes(("name", "a/C"));
        var result = new List<SourceExtractionCase>
        {
            Case("SDK-01", "<api version=\"4\"><class name=\"a/Outer$Inner\" since=\"37.1\"><method name=\"&lt;init>()V\"/><method name=\"f([Ljava/lang/String;I)J\" since=\"37.2\"/><field name=\"OLD\" removed=\"37.2\"/><field name=\"FUTURE\" since=\"37.3\"/></class></api>",
            [Type("a/Outer$Inner", owner, "<class name=\"a/Outer$Inner\" since=\"37.1\" />"),
             Member("a/Outer$Inner", "<init>()V", Attributes(("name", "<init>()V")), owner, "<method name=\"&lt;init&gt;()V\" />", "constructor"),
             Member("a/Outer$Inner", "f([Ljava/lang/String;I)J", Attributes(("name", "f([Ljava/lang/String;I)J"), ("since", "37.2")), owner,
                 "<method name=\"f([Ljava/lang/String;I)J\" since=\"37.2\" />", returnType: "long", parameters: ["java.lang.String[]", "int"], ordinal: 2),
             Member("a/Outer$Inner", "OLD", Attributes(("name", "OLD"), ("removed", "37.2")), owner, "<field name=\"OLD\" removed=\"37.2\" />", "field", null, lifecycle: "not-current-at-target", ordinal: 3),
             Member("a/Outer$Inner", "FUTURE", Attributes(("name", "FUTURE"), ("since", "37.3")), owner, "<field name=\"FUTURE\" since=\"37.3\" />", "field", null, lifecycle: "not-current-at-target", ordinal: 4)],
            "test_sdk_api_index.py:test_minor_lifecycle_and_removed_members"),

            Case("SDK-02", "<api version=\"4\"><class name=\"android/Manifest$permission\"><field name=\"CAMERA\"/></class></api>",
            [Type("android/Manifest$permission", Attributes(("name", "android/Manifest$permission")), "<class name=\"android/Manifest$permission\" />"),
             Member("android/Manifest$permission", "CAMERA", Attributes(("name", "CAMERA")), Attributes(("name", "android/Manifest$permission")), "<field name=\"CAMERA\" />", "field", null)],
            "test_sdk_api_index.py:test_field_type_and_type_kind_are_not_invented"),

            Case("SDK-03", "<api version=\"4\"><class name=\"a/C\"><method name=\"broken(wat)\"/><method name=\"ok()V\"/></class></api>",
            [Type("a/C", plain, "<class name=\"a/C\" />"),
             Member("a/C", "broken(wat)", Attributes(("name", "broken(wat)")), plain, "<method name=\"broken(wat)\" />", "unsupported", null, parseError: "Unsupported JVM descriptor wat)"),
             Member("a/C", "ok()V", Attributes(("name", "ok()V")), plain, "<method name=\"ok()V\" />", ordinal: 2)],
            "test_sdk_api_index.py:test_invalid_method_descriptor_retained_unknown plus following-row no-drop control", StructuralParseStatus.Partial, "sdk-descriptor"),

            Case("SDK-04", "<api version=\"4\"><class name=\"a/C\" since=\"38\"><field name=\"f\"/></class></api>",
            [Type("a/C", Attributes(("name", "a/C"), ("since", "38")), "<class name=\"a/C\" since=\"38\" />", lifecycle: "not-current-at-target"),
             Member("a/C", "f", Attributes(("name", "f")), Attributes(("name", "a/C"), ("since", "38")), "<field name=\"f\" />", "field", null, lifecycle: "not-current-at-target")],
            "test_sdk_availability_evidence.py:test_absence_remains_absent_and_future_history_not_filtered (declaration view)"),

            Case("SDK-05", "<api version=\"4\"><class name=\"a/C\" since=\"37.2\" future=\"raw\"/></api>",
            [Type("a/C", Attributes(("name", "a/C"), ("since", "37.2"), ("future", "raw")), "<class name=\"a/C\" since=\"37.2\" future=\"raw\" />")],
            "test_sdk_availability_evidence.py:test_new_attributes_preserved"),

            Case("SDK-06", "<api version=\"4\">\n<class name=\"a/C\">\n<field name=\"X\"/>\n<field name=\"X\"/>\n</class></api>",
            [Type("a/C", plain, "<class name=\"a/C\" />", 2), Member("a/C", "X", Attributes(("name", "X")), plain, "<field name=\"X\" />", "field", null, line: 3),
             Member("a/C", "X", Attributes(("name", "X")), plain, "<field name=\"X\" />", "field", null, ordinal: 2, line: 4)],
            "sdk_api_index.py:physical XML locator alignment; repeated-member boundary"),

            Case("SDK-07", "<api version=\"4\"><class name=\"a/C\"><extends name=\"a/Base\" since=\"30\"/><implements name=\"a/I\"/></class></api>",
            [Type("a/C", plain, "<class name=\"a/C\" />", supertypes: new JsonArray(
                new JsonObject { ["name"] = "a/Base", ["since"] = "30", ["relation"] = "extends" },
                new JsonObject { ["name"] = "a/I", ["relation"] = "implements" }))],
            "sdk_api_index.py:type supertypes preserved without inferred Java kind"),

            Case("SDK-08", "<api version=\"4\"><class name=\"a/C\"><method name=\"p(BCDFIJSZ[[Ljava/lang/String;)V\"/></class></api>",
            [Type("a/C", plain, "<class name=\"a/C\" />"), Member("a/C", "p(BCDFIJSZ[[Ljava/lang/String;)V", Attributes(("name", "p(BCDFIJSZ[[Ljava/lang/String;)V")), plain,
                "<method name=\"p(BCDFIJSZ[[Ljava/lang/String;)V\" />", parameters: ["byte", "char", "double", "float", "int", "long", "short", "boolean", "java.lang.String[][]"])],
            "sdk_api_index.py:descriptor_type primitive/object/multidimensional-array semantics"),

            Case("SDK-09", "<api version=\"4\"><class name=\"a/C\" since=\"37.2\"><field name=\"NOW\"/><field name=\"OLD\" removed=\"37.2\"/><field name=\"LATER\" since=\"37.3\"/></class></api>",
            [Type("a/C", Attributes(("name", "a/C"), ("since", "37.2")), "<class name=\"a/C\" since=\"37.2\" />"),
             Member("a/C", "NOW", Attributes(("name", "NOW")), Attributes(("name", "a/C"), ("since", "37.2")), "<field name=\"NOW\" />", "field", null),
             Member("a/C", "OLD", Attributes(("name", "OLD"), ("removed", "37.2")), Attributes(("name", "a/C"), ("since", "37.2")), "<field name=\"OLD\" removed=\"37.2\" />", "field", null, lifecycle: "unknown", ordinal: 2),
             Member("a/C", "LATER", Attributes(("name", "LATER"), ("since", "37.3")), Attributes(("name", "a/C"), ("since", "37.2")), "<field name=\"LATER\" since=\"37.3\" />", "field", null, lifecycle: "not-current-at-target", ordinal: 3)],
            "sdk_api_index.py:active interval since/removed boundary and invalid equal interval"),

            Case("SDK-10", "<api version=\"4\"><sdk id=\"30\" name=\"R\"/><class name=\"a/C\" sdks=\"30:4,0:37.2\"><method name=\"m()V\"/></class></api>",
            [Type("a/C", Attributes(("name", "a/C"), ("sdks", "30:4,0:37.2")), "<class name=\"a/C\" sdks=\"30:4,0:37.2\" />", extensions: Extensions()),
             Member("a/C", "m()V", Attributes(("name", "m()V")), Attributes(("name", "a/C"), ("sdks", "30:4,0:37.2")), "<method name=\"m()V\" />", extensions: Extensions())],
            "sdk_api_index.py:SDK extension declaration and inherited raw lifecycle"),

            Case("SDK-11", "<api version=\"4\"><class name=\"a/C\" sdks=\"99:1\"/></api>",
            [Type("a/C", Attributes(("name", "a/C"), ("sdks", "99:1")), "<class name=\"a/C\" sdks=\"99:1\" />", lifecycle: "unknown", lifecycleReason: "Unrecognized or undeclared SDK extension lifecycle token")],
            "sdk_api_index.py:undeclared extension is retained unknown"),

            Case("SDK-12", "<api version=\"4\">\n<!-- 😀 non-ASCII prefix -->\n<class name=\"a/Ç\">\n<method name=\"méthode()V\"/>\n</class></api>",
            [Type("a/Ç", Attributes(("name", "a/Ç")), "<class name=\"a/Ç\" />", 3),
             Member("a/Ç", "méthode()V", Attributes(("name", "méthode()V")), Attributes(("name", "a/Ç")), "<method name=\"méthode()V\" />", line: 4)],
            "Physical XML line locator with non-ASCII and surrogate-pair source prefix"),

            Case("SDK-13", "<api version=\"4\"/>", [], "sdk_api_index.py:empty valid index retains empty inventory")
        };
        AddBadDescriptorCases(result, plain);
        return result;
    }

    private static JsonArray Extensions() => new(new JsonObject { ["sdk_id"] = "30", ["minimum_version"] = "4" },
        new JsonObject { ["sdk_id"] = "0", ["minimum_version"] = "37.2" });

    private static void AddBadDescriptorCases(List<SourceExtractionCase> cases, JsonObject owner)
    {
        foreach (var (id, descriptor, reason) in new[]
        {
            ("SDK-BAD01", "bad", "Missing method JVM descriptor"),
            ("SDK-BAD02", "m([)V", "Unsupported JVM descriptor [)V"),
            ("SDK-BAD03", "m(Ljava/lang/String)V", "Unterminated object JVM descriptor"),
            ("SDK-BAD04", "m(I", "Unclosed method JVM descriptor"),
            ("SDK-BAD05", "m(V)V", "Illegal void JVM descriptor"),
            ("SDK-BAD06", "m([V)V", "Illegal void JVM descriptor"),
            ("SDK-BAD07", "m()Vjunk", "Trailing method JVM descriptor")
        })
        {
            var literal = Attributes(("name", descriptor));
            var raw = "<method name=\"" + descriptor + "\" />";
            var unsupported = Member("a/C", descriptor, literal, owner, raw, "unsupported", null, parseError: reason);
            cases.Add(Case(id, "<api version=\"4\"><class name=\"a/C\"><method name=\"" + descriptor + "\"/></class></api>",
                [Type("a/C", owner, "<class name=\"a/C\" />"), unsupported], "sdk_api_index.py:method_descriptor explicit failure branch", StructuralParseStatus.Partial, "sdk-descriptor"));
        }
    }
}
