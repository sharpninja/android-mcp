using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record SdkAnnotationTargetCase(string Id, string Item, JsonObject Expected)
{
    public SourceExtractionCase MutationCase() => new(Id, "sdk-annotation-spelling", Item, StructuralParseStatus.Clean, [Expected], [], "build_catalog external item spelling and annotation_link raw type/root facts");
}
public static class SdkAnnotationTargetContractCases
{
    private static JsonArray Strings(string[]? values) => new((values ?? []).Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
    private static JsonObject Target(string item, string scope, string owner, string? canonical, string? shortened = null, string? package = null,
        string? declared = null, string? declaredShort = null, string[]? declaredRoots = null, string[]? parameterRoots = null, bool constructor = false) => new()
    {
        ["item_name"] = item, ["scope"] = scope, ["owner"] = owner, ["target_canonical"] = canonical,
        ["java_lang_shortened_candidate"] = shortened, ["package_candidate"] = package,
        ["declared_type"] = declared, ["declared_type_shortened"] = declaredShort,
        ["declared_type_java_lang_roots"] = Strings(declaredRoots), ["parameter_java_lang_roots"] = Strings(parameterRoots), ["is_constructor"] = constructor
    };
    public static IReadOnlyList<SdkAnnotationTargetCase> All { get; } = Create();
    private static IReadOnlyList<SdkAnnotationTargetCase> Create()
    {
        const string go = "a.A void go(java.lang.String)";
        const string generic = "a.A a.Result<java.lang.String> call(a.Callback<T>)";
        const string unknown = "a.A a.Result call(T)";
        const string ordinary = "a.A int A()";
        const string parameter = "a.A void go(java.lang.String) 0";
        const string constructor = "a.A A()";
        const string field = "a.A java.lang.String FIELD";
        const string many = "a.A void many(java.lang.String[],final java.util.Map<java.lang.String,java.lang.Integer>) 3";
        const string whitespace = "a.A  void go( java.lang.String ) 02";
        const string nested = "a.A java.util.List<java.lang.Thread.State> field";
        const string ctorParameter = "a.A A(java.lang.String[]) 1";
        return
        [
            new("ANN-T01", go, Target(go, "declaration", "a.A", "a.A#go(java.lang.String)", "a.A#go(String)", declared: "void", declaredShort: "void", parameterRoots: ["String"])),
            new("ANN-T02", generic, Target(generic, "declaration", "a.A", "a.A#call(a.Callback<T>)", declared: "a.Result<java.lang.String>", declaredShort: "a.Result<String>", declaredRoots: ["String"])),
            new("ANN-T03", unknown, Target(unknown, "declaration", "a.A", "a.A#call(T)", declared: "a.Result", declaredShort: "a.Result")),
            new("ANN-T04", ordinary, Target(ordinary, "declaration", "a.A", "a.A#A()", declared: "int", declaredShort: "int")),
            new("ANN-T05", parameter, Target(parameter, "parameter:0", "a.A", "a.A#go(java.lang.String)", "a.A#go(String)", declared: "void", declaredShort: "void", parameterRoots: ["String"])),
            new("ANN-T06", constructor, Target(constructor, "declaration", "a.A", "a.A#<init>()", constructor: true)),
            new("ANN-T07", field, Target(field, "declaration", "a.A", "a.A#FIELD", declared: "java.lang.String", declaredShort: "String", declaredRoots: ["String"])),
            new("ANN-T08", "a.A", Target("a.A", "declaration", "a.A", "a.A", package: "package:a.A")),
            new("ANN-T09", many, Target(many, "parameter:3", "a.A", "a.A#many(java.lang.String[],java.util.Map<java.lang.String,java.lang.Integer>)", "a.A#many(String[],java.util.Map<String,Integer>)", declared: "void", declaredShort: "void", parameterRoots: ["Integer", "String"])),
            new("ANN-T10", "a.A T field", Target("a.A T field", "declaration", "a.A", "a.A#field", declared: "T", declaredShort: "T")),
            new("ANN-T11", "java.lang.A int field", Target("java.lang.A int field", "declaration", "java.lang.A", "java.lang.A#field", declared: "int", declaredShort: "int", parameterRoots: ["A"])),
            new("ANN-T12", whitespace, Target(whitespace, "parameter:02", "a.A", "a.A#go(java.lang.String)", "a.A#go(String)", declared: "void", declaredShort: "void", parameterRoots: ["String"])),
            new("ANN-T13", nested, Target(nested, "declaration", "a.A", "a.A#field", declared: "java.util.List<java.lang.Thread.State>", declaredShort: "java.util.List<Thread.State>", declaredRoots: ["Thread"])),
            new("ANN-T14", ctorParameter, Target(ctorParameter, "parameter:1", "a.A", "a.A#<init>(java.lang.String[])", "a.A#<init>(String[])", parameterRoots: ["String"], constructor: true))
        ];
    }
    public static IEnumerable<object[]> Cases => All.Select(test => new object[] { test.Id });
    public static IEnumerable<object[]> Mutations => All.SelectMany(test => SourceMutationCases.For(test.MutationCase()));
    public static SdkAnnotationTargetCase Get(string id) => All.Single(test => test.Id == id);
    public static void AssertContract(SdkAnnotationTargetCase test, IAnnotationTargetSpellingSourceFacts provider)
    { var observed = provider.NormalizeTarget(test.Item); Assert.True(JsonNode.DeepEquals(test.Expected, observed), "XML target spelling facts differ: " + test.Id); }
}
