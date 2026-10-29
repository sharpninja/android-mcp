using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record XmlAnnotationKeyCase(string Id, string Item, string? Expected);
public sealed record FormalNamesCase(string Id, string Raw, string[] Names, bool MalformedAnnotation = false);
public sealed record StructuralHeaderCase(string Id, JsonObject Extensions, JsonNode? Parameters, JsonObject[] Supertypes);
public static class SourceTypeFactsContractCases
{
    public static IReadOnlyDictionary<string, string> Owners { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    { ["a.A"] = "a/A", ["a.Callback"] = "a/Callback", ["java.lang.String"] = "java/lang/String", ["a.Result"] = "a/Result", ["a.A.Inner"] = "a/A$Inner" };
    public static IReadOnlyList<XmlAnnotationKeyCase> Xml { get; } =
    [
        new("XMLKEY-01", "a.A a.Result<java.lang.String> call(a.Callback<a.Result>, java.lang.String)", "a/A#call(La/Callback;Ljava/lang/String;)La/Result;"),
        new("XMLKEY-02", "a.A void call(T)", null), new("XMLKEY-03", "a.A T call()", null),
        new("XMLKEY-04", "a.A void call(String)", null), new("XMLKEY-05", "a.A void call(a.Callback<T>[]) 0", "a/A#call([La/Callback;)V"),
        new("XMLKEY-06", "a.A void call(a.Callback<T)", null), new("XMLKEY-07", "a.A void call(void)", null),
        new("XMLKEY-08", "a.A A()", "a/A#<init>()V"), new("XMLKEY-09", "a.A int A()", "a/A#A()I"),
        new("XMLKEY-10", "a.A a.A.Inner call()", "a/A#call()La/A$Inner;"),
        new("XMLKEY-11", "a.Unknown void call()", null), new("XMLKEY-12", "a.A", null), new("XMLKEY-13", "a.A void call() tail", null)
    ];
    public static IReadOnlyList<FormalNamesCase> Formals { get; } =
    [
        new("FORMAL-01", "public class A<P> {", ["P"]),
        new("FORMAL-02", "method public <T extends java.util.Map<String,java.util.List<U>>, U> T go(T);", ["T", "U"]),
        new("FORMAL-03", "@Note(value=\"class Fake<F>\") public class A<T extends java.lang.Object> {", ["T"]),
        new("FORMAL-04", "method public java.util.List<T> go();", []),
        new("FORMAL-05", "public class A<T", []),
        new("FORMAL-06", "/* @Broken( */ public class A<T> {", [], true),
        new("FORMAL-07", "@Broken( public class A<T> {", [], true)
    ];
    private static JsonObject Super(string relation, string text) => new() { ["relation"] = relation, ["text"] = text };
    public static IReadOnlyList<StructuralHeaderCase> Headers { get; } =
    [
        new("HEADER-01", new() { ["type_header"] = "public class A<T> extends p.P implements p.I,p.J {" }, JsonValue.Create("<T>"), [Super("extends", "p.P"), Super("implements", "p.I"), Super("implements", "p.J")]),
        new("HEADER-02", new() { ["superclass"] = "extends p.P", ["interfaces"] = "implements p.J,p.I", ["supertypes"] = new JsonArray(new JsonObject { ["name"] = "p/P", ["relation"] = "extends" }, new JsonObject { ["name"] = "p/J", ["relation"] = "implements" }) }, null,
            [Super("extends", "p.P"), Super("implements", "p.I"), Super("implements", "p.J")]),
        new("HEADER-03", new() { ["language"] = "hidl", ["declared_extends"] = "extends p@1.0::IBase", ["target_type_raw"] = "vec<int32_t>" }, null,
            [Super("hidl-declared-extends", "extends p@1.0::IBase"), Super("hidl-typedef-target", "vec<int32_t>")]),
        new("HEADER-04", new() { ["supertypes"] = new JsonArray(new JsonObject { ["name"] = "p/Outer$Inner" }, new JsonObject()) }, null, [Super("unknown", "p.Outer.Inner")]),
        new("HEADER-05", new() { ["type_parameters"] = "existing", ["type_header"] = "public class A<T" }, JsonValue.Create("existing"), []),
        new("HEADER-06", new() { ["supertypes"] = new JsonArray(new JsonObject { ["name"] = "𐀀", ["relation"] = "extends" }, new JsonObject { ["name"] = "", ["relation"] = "extends" }) }, null, [Super("extends", ""), Super("extends", "𐀀")]),
        new("HEADER-07", new(), null, [])
    ];
    public static IEnumerable<object[]> XmlCases => Xml.Select(x => new object[] { x.Id });
    public static IEnumerable<object[]> FormalCases => Formals.Select(x => new object[] { x.Id });
    public static IEnumerable<object[]> HeaderCases => Headers.Select(x => new object[] { x.Id });
    public static IEnumerable<object[]> HeaderFieldControls => Headers.SelectMany(x => x.Supertypes.SelectMany((row, index) => row.Select(property => new object[] { x.Id, index, property.Key })));
    public static void AssertXml(XmlAnnotationKeyCase test, IAnnotationTargetSourceFacts provider) => Assert.Equal(test.Expected, provider.XmlJvmKey(test.Item, Owners));
    public static void AssertFormals(FormalNamesCase test, IAnnotationTargetSourceFacts provider)
    {
        if (test.MalformedAnnotation) Assert.Contains("Unclosed annotation", Assert.Throws<FormatException>(() => provider.FormalNames(test.Raw)).Message, StringComparison.OrdinalIgnoreCase);
        else Assert.True(new HashSet<string>(test.Names, StringComparer.Ordinal).SetEquals(provider.FormalNames(test.Raw)), "Formal names differ: " + test.Id);
    }
    public static void AssertHeader(StructuralHeaderCase test, IStructuralTypeHeaderSourceFacts provider)
    {
        var document = new JsonObject { ["extensions"] = test.Extensions.DeepClone() }; var original = document.DeepClone(); var result = provider.StructuralHeader(document);
        Assert.True(JsonNode.DeepEquals(test.Parameters, result.TypeParameters), "Formal parameter text differs: " + test.Id);
        Assert.True(JsonNode.DeepEquals(new JsonArray(test.Supertypes.Select(x => (JsonNode)x.DeepClone()).ToArray()), new JsonArray(result.Supertypes.Select(x => (JsonNode)x.DeepClone()).ToArray())), "Structural supertypes differ: " + test.Id);
        Assert.True(JsonNode.DeepEquals(original, document));
    }
}
