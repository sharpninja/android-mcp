using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;
using static AndroidMcp.BuildTool.Tests.Sources.SourceExtractionContractCases;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

// FR-BUILD-004 / TEST-BUILD-004. Expectations transcribed from retained
// test_java_source.py, not produced by running the old or new extractor.
public sealed record JavaCase(string Id, string Text, JsonObject[] Rows, JsonObject Metadata,
    bool HasErrors = false, int Repetitions = 1, bool MissingToken = false)
{
    public SourceDocument Input() => new(Id switch { "JAVA-01"=>"Fixture.java","JAVA-02"=>"C.java","JAVA-03"=>"I.java","JAVA-04"=>"X.java","JAVA-05"=>"E.java","JAVA-06"=>"Large.java","JAVA-07"=>"Comments.java","JAVA-08"=>"Bits.java",_=>Id+".java" }, Encoding.UTF8.GetBytes(Text), new("fixture", "pinned", "java", "37.2"));
    public ExtractionResult Correct() => new(Rows.Select(x => (JsonObject)x.DeepClone()).ToArray(),
        HasErrors ? [new("java-ast-error", MissingToken ? "Java AST missing token" : "Java AST ERROR", 0, Encoding.UTF8.GetByteCount(Text))] : [],
        HasErrors ? StructuralParseStatus.Malformed : StructuralParseStatus.Clean, (JsonObject)Metadata.DeepClone());
}

public static class JavaContractCases
{
    public static JsonObject Object(params (string Key, object? Value)[] fields)
    {
        var result = new JsonObject();
        foreach (var (key, value) in fields) result[key] = value is JsonNode node ? node.DeepClone() : System.Text.Json.JsonSerializer.SerializeToNode(value);
        return result;
    }
    public static JsonArray Array(params JsonNode?[] nodes) => new(nodes.Select(x => x?.DeepClone()).ToArray());
    private static JsonObject Row(string kind, string canonical, string name, JsonObject? extras = null) =>
        Object(("kind", kind), ("canonical", canonical), ("name", name), ("extras", extras ?? new()));
    private static JsonObject Parameter(string type, string name, JsonArray? annotations = null) =>
        Object(("type", type), ("name", name), ("direction", null), ("annotations", annotations ?? new()));
    private static JsonObject Annotation(string name, string arguments, string resolution, string spelling) =>
        Object(("name", name), ("arguments", arguments), ("name_resolution", resolution), ("source_spelling", spelling));
    private static JsonObject Metadata(string package = "a", JsonObject? imports = null, JsonArray? wildcards = null) =>
        Object(("package", package), ("imports", imports ?? new()), ("wildcard_imports", wildcards ?? new()));
    private static JsonObject With(JsonObject row, params (string Key, object? Value)[] fields)
    {
        foreach (var field in Object(fields)) row[field.Key] = field.Value?.DeepClone();
        return row;
    }
    private static JsonObject Pkg() => Row("package", "a", "a");
    private static JsonObject Type(string kind, string canonical, string name, string binary) => Row(kind, canonical, name, Object(("owner_binary_name", binary)));
    private static JsonObject Method(string canonical, string name, string? returnType, JsonArray? parameters = null) =>
        With(Row("method", canonical, name), ("return_type", returnType), ("parameters", parameters ?? new()));
    private static JsonObject Field(string canonical, string name, string type, bool? enumConstant = null) =>
        With(Row("field", canonical, name, enumConstant.HasValue ? Object(("enum_constant", enumConstant)) : null), ("return_type", type));

    public static readonly IReadOnlyList<JavaCase> All = Create();
    public static IEnumerable<object[]> Cases => All.Select(x => new object[] { x.Id });
    public static JavaCase Get(string id) => All.Single(x => x.Id == id);
    private static IReadOnlyList<JavaCase> Create()
    {
        var cases = new List<JavaCase>();
        var go = Method("a.Outer#go(java.util.List<String>,int...)", "go", "java.util.List<String>");
        // Parameter String remains source spelling; canonical uses parameter types.
        go["canonical"] = "a.Outer#go(String,int...)";
        go["parameters"] = Array(Parameter("String", "x", Array(Annotation("java.lang.Deprecated", "", "java-lang", "Deprecated"))), Parameter("int...", "ns"));
        go["annotations"] = Array(Annotation("android.annotation.RequiresPermission", "value=\"android.permission.CAMERA\",conditional=true", "explicit-import", "RequiresPermission"));
        cases.Add(new("JAVA-01", """
package a; import java.util.List; import android.annotation.RequiresPermission;
  public class Outer<T> {
   public static final String P="android.permission.CAMERA", Q="android.permission.RECORD_AUDIO";
   public Outer() {}
   @RequiresPermission(value="android.permission.CAMERA",conditional=true)
   public List<String> go(@Deprecated final String x, int... ns) { return null; }
   public void go(int n) {}
   public static class Inner { public int value; }
   public @interface A { String value() default ""; }
  }
""", [Pkg(), Type("class", "a.Outer", "Outer", "a/Outer"), Field("a.Outer#P", "P", "String"), Field("a.Outer#Q", "Q", "String"),
            With(Row("constructor", "a.Outer#<init>()", "Outer"), ("return_type", null), ("parameters", new JsonArray())), go,
            Method("a.Outer#go(int)", "go", "void", Array(Parameter("int", "n"))), Type("class", "a.Outer.Inner", "Inner", "a/Outer$Inner"),
            Field("a.Outer.Inner#value", "value", "int"), Type("annotation", "a.Outer.A", "A", "a/Outer$A"),
            With(Method("a.Outer.A#value()", "value", "String"), ("extras", Object(("effective_visibility", "public"), ("default_value_raw", "\"\""))))],
            Metadata(imports: Object(("List", "java.util.List"), ("RequiresPermission", "android.annotation.RequiresPermission")))));
        cases.Add(new("JAVA-02", "package a; public class C { public void x(){ class Local { int y; } } }",
            [Pkg(), Type("class", "a.C", "C", "a/C"), Method("a.C#x()", "x", "void")], Metadata()));
        cases.Add(new("JAVA-03", "package a; public interface I { String NAME=\"p\"; void f(String p); }",
            [Pkg(), Type("interface", "a.I", "I", "a/I"), With(Field("a.I#NAME", "NAME", "String"), ("extras", Object(("effective_visibility", "public")))),
             With(Method("a.I#f(String)", "f", "void", Array(Parameter("String", "p"))), ("extras", Object(("effective_visibility", "public"))))], Metadata()));
        cases.Add(new("JAVA-04", "package a; public class X { public void broken( }",
            [Pkg(), Type("class", "a.X", "X", "a/X"), Row("unsupported", "UNSPECIFIED", "unparsed")], Metadata(), true));
        cases.Add(new("JAVA-05", "package a; public enum E { RED,BLUE; public int a[],b; }",
            [Pkg(), Type("enum", "a.E", "E", "a/E"), Field("a.E#RED", "RED", "a.E", true), Field("a.E#BLUE", "BLUE", "a.E", true),
             Field("a.E#a", "a", "int[]"), Field("a.E#b", "b", "int")], Metadata()));
        cases.Add(new("JAVA-06", new string('\n', 300) + "package a;\npublic class C {\n public int x;\n public void f(int n){}\n}",
            [Pkg(), Type("class", "a.C", "C", "a/C"), With(Field("a.C#x", "x", "int"), ("line", 303)), Method("a.C#f(int)", "f", "void", Array(Parameter("int", "n")))], Metadata(), Repetitions: 100));
        cases.Add(new("JAVA-07", "package a; public /* @NotAnAnnotation( */ class C { public /* @other( */ int x; }",
            [With(Pkg(), ("annotations", new JsonArray())), With(Type("class", "a.C", "C", "a/C"), ("annotations", new JsonArray())), With(Field("a.C#x", "x", "int"), ("annotations", new JsonArray()))], Metadata()));
        cases.Add(new("JAVA-08", "package a; @LongDef(value={1, // because it'd wrap\n 2}) public @interface Bits {}",
            [Pkg(), With(Type("annotation", "a.Bits", "Bits", "a/Bits"), ("annotations", Array(Annotation("a.LongDef", "value={1, " + new string(' ', "// because it'd wrap".Length) + "\n 2}", "package-candidate; unresolved wildcard imports retained", "LongDef"))))], Metadata()));
        cases.Add(new("JAVA-09", "package a; import android.annotation.*; import x.Permissions; @RequiresPermission(Permissions.CAMERA) class C {}",
            [Pkg(), With(Type("class", "a.C", "C", "a/C"), ("annotations", Array(Annotation("android.annotation.RequiresPermission", "x.Permissions.CAMERA", "known-annotation-wildcard", "RequiresPermission"))))],
            Metadata(imports: Object(("Permissions", "x.Permissions")), wildcards: Array(JsonValue.Create("android.annotation")))));
        cases.Add(new("JAVA-10", "package a; class C { int x = new Object(){ int hidden; }.hashCode(); static { class Hidden {} } record R(int x) { R {} } }",
            [Pkg(), Type("class", "a.C", "C", "a/C"), Field("a.C#x", "x", "int"), Type("class", "a.C.R", "R", "a/C$R"), With(Field("a.C.R#x", "x", "int"), ("annotations", new JsonArray()), ("extras", Object(("owner_kind", "class"), ("record_component", true), ("record_component_annotations", new JsonArray()), ("annotation_scope", "component; target propagation unresolved"), ("effective_visibility", "unknown; source record component metadata")))), With(Row("constructor", "a.C.R#<init>(int)", "R"), ("parameters", Array(Parameter("int", "x"))))], Metadata()));
        cases.Add(new("JAVA-11", "package a; class C { void f(C this, int[] x, String... words) {} }",
            [Pkg(), Type("class", "a.C", "C", "a/C"), With(Method("a.C#f(int[],String...)", "f", "void", Array(Parameter("int[]", "x"), Parameter("String...", "words"))), ("extras", Object(("receiver_parameters", Array(JsonValue.Create("C this"))))))], Metadata()));
        cases.Add(new("JAVA-12", "package a; // π😀\nclass C { int value; }",
            [Pkg(), With(Type("class", "a.C", "C", "a/C"), ("line", 2), ("extras", Object(("owner_binary_name", "a/C"), ("source_byte_range", Array(JsonValue.Create(21), JsonValue.Create(43)))))), With(Field("a.C#value", "value", "int"), ("line", 2))], Metadata()));
        cases.Add(new("JAVA-13", "package a; class C { int x }", [Pkg(), Type("class", "a.C", "C", "a/C"), Row("unsupported", "UNSPECIFIED", "unparsed")], Metadata(), true, MissingToken:true));
        return cases;
    }

    public static void AssertContract(JavaCase test, ISourceExtractor provider)
    {
        Assert.Equal("java-source", provider.Kind);
        for (var repetition = 0; repetition < test.Repetitions; repetition++)
        {
            var input = test.Input(); var original = input.Bytes.ToArray(); var result = provider.Extract(input);
            Assert.Equal(original, input.Bytes.ToArray()); AssertSubset(test.Metadata, result.Metadata);
            if (test.HasErrors)
            {
                Assert.NotEqual(StructuralParseStatus.Clean, result.StructuralStatus); Assert.NotEmpty(result.Issues);
                if(test.MissingToken) Assert.Contains(result.Issues, issue=>issue.Reason.Contains("missing token",StringComparison.Ordinal));
                Assert.Contains(result.Records, x => x["kind"]?.GetValue<string>() == "unsupported");
                foreach (var issue in result.Issues) { Assert.NotEmpty(issue.Reason); Assert.InRange(issue.StartByte!.Value, 0, original.Length); Assert.InRange(issue.EndByte!.Value, issue.StartByte.Value, original.Length); }
                return;
            }
            Assert.Equal(StructuralParseStatus.Clean, result.StructuralStatus); Assert.Empty(result.Issues); Assert.Equal(test.Rows.Length, result.Records.Count);
            for (var index = 0; index < test.Rows.Length; index++) AssertSubset(test.Rows[index], result.Records[index]);
        }
    }
}
