using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record NativeDescriptorCase(string Id, string Text, string Language, JsonObject Expected)
{
    public SourceExtractionCase MutationCase() => new(Id, "native-descriptor", Text, StructuralParseStatus.Clean, [Expected], [],
        "tools/source_facts/native_descriptor_facts.py parse and eight original tests, read without executing Python");
}

public static class NativeDescriptorContractCases
{
    private static JsonObject Fact(string family, string raw, string? symbol, string? expression, string? literal, int start = 0, int line = 1) => new()
    {
        ["family"] = family, ["line"] = line, ["character_span"] = new JsonArray(start, start + raw.EnumerateRunes().Count()),
        ["declared_symbol_token"] = symbol, ["descriptor_expression"] = expression, ["descriptor_literal"] = literal,
        ["status"] = literal is null ? "computed-or-unresolved-argument" : "literal-argument-candidate", ["raw"] = raw,
        ["compiled_endpoint_proven"] = false, ["wire_transaction_schema"] = null,
        ["preprocessor_or_build_conditions"] = "unresolved", ["runtime_registration"] = "unknown"
    };
    private static JsonObject Report(JsonObject[]? facts = null, JsonObject[]? issues = null) => new()
    {
        ["facts"] = new JsonArray((facts ?? []).Select(row => (JsonNode)row.DeepClone()).ToArray()),
        ["lexical_issues"] = new JsonArray((issues ?? []).Select(row => (JsonNode)row.DeepClone()).ToArray()), ["compiled_or_runtime_proven"] = false
    };
    private static NativeDescriptorCase Call(string id, string language, string text, string family, string raw, string? symbol, string? expression, string? literal,
        int start = 0, int line = 1) => new(id, text, language, Report([Fact(family, raw, symbol, expression, literal, start, line)]));
    public static IReadOnlyList<NativeDescriptorCase> All { get; } = Create();
    private static IReadOnlyList<NativeDescriptorCase> Create()
    {
        const string rust = "binder::declare_binder_interface! { IFoo[\"android.foo.IFoo\"] {native: BnFoo(on_transact),proxy:BpFoo,} }";
        const string rawRust = "declare_binder_interface! { IFoo[r#\"android.foo.IFoo\"#] {} }";
        const string computed = "AIBinder_Class_define(IFoo::descriptor,create,destroy,transact)";
        const string macro = "IMPLEMENT_META_INTERFACE(Foo,\"android.foo.IFoo\")";
        const string nested = "AIBinder_Class_define(\"android.X\",factory(a,b),destroy,transact)";
        const string unclosed = "AIBinder_Class_define(\"name\",create,";
        const string unicode = "// 😀\nIMPLEMENT_HYBRID_META_INTERFACE(Foo,u8\"android.\\u0058\")";
        var delimiter = string.Concat(Enumerable.Repeat("😀", 16));
        var unicodeLiteral = "R\"" + delimiter + "(android.Foo)" + delimiter + "\"";
        var excessiveLiteral = "R\"" + delimiter + "😀(android.Foo)" + delimiter + "😀\"";
        var unicodeMacro = "IMPLEMENT_META_INTERFACE(Foo," + unicodeLiteral + ")";
        var excessiveMacro = "IMPLEMENT_META_INTERFACE(Foo," + excessiveLiteral + ")";
        return
        [
            Call("ND-01", "rust", rust, "declare_binder_interface", rust[8..], "IFoo", "\"android.foo.IFoo\"", "android.foo.IFoo", 8),
            Call("ND-02", "rust", rawRust, "declare_binder_interface", rawRust, "IFoo", "r#\"android.foo.IFoo\"#", "android.foo.IFoo"),
            new("ND-03", "// AIBinder_Class_define(\"fake\",a,b,c);\nauto s=R\"tag(AIBinder_Class_define(\"fake\",a,b,c))tag\";", "cpp", Report()),
            new("ND-04", "/* outer /* inner */ declare_binder_interface! {Fake[\"fake\"]{}} */ let s=r##\"declare_binder_interface! {Fake[\"fake\"]{}}\"##;", "rust", Report()),
            Call("ND-05", "cpp", computed + ";", "AIBinder_Class_define", computed, null, "IFoo::descriptor", null),
            new("ND-06", macro + "; " + nested + ";", "cpp", Report([Fact("IMPLEMENT_META_INTERFACE", macro, "Foo", "\"android.foo.IFoo\"", "android.foo.IFoo"),
                Fact("AIBinder_Class_define", nested, null, "\"android.X\"", "android.X", macro.Length + 2)])),
            new("ND-07a", "#define IMPLEMENT_META_INTERFACE(Foo,Name) body", "cpp", Report()),
            new("ND-07b", "macro_rules! declare_binder_interface {($x:tt)=>{}}", "rust", Report()),
            new("ND-08", unclosed, "cpp", Report([new JsonObject { ["family"] = "AIBinder_Class_define", ["line"] = 1, ["status"] = "unclosed-token-form",
                ["raw"] = unclosed, ["descriptor_literal"] = null, ["compiled_endpoint_proven"] = false }])),
            Call("ND-09", "cpp", unicode, "IMPLEMENT_HYBRID_META_INTERFACE", unicode[6..], "Foo", "u8\"android.\\u0058\"", "android.X", 5, 2),
            Call("ND-10", "cpp", "IMPLEMENT_META_NESTED_INTERFACE(Outer,Foo,R\"x(android.Foo)x\")", "IMPLEMENT_META_NESTED_INTERFACE",
                "IMPLEMENT_META_NESTED_INTERFACE(Outer,Foo,R\"x(android.Foo)x\")", "Outer", "R\"x(android.Foo)x\"", "android.Foo"),
            Call("ND-11", "rust", "declare_binder_interface!(r#IFoo[descriptor()] {})", "declare_binder_interface",
                "declare_binder_interface!(r#IFoo[descriptor()] {})", "r#IFoo", "descriptor()", null),
            Call("ND-12", "cpp", "AIBinder_Class_define( /*c*/ \"a\" \"b\", x, y, z)", "AIBinder_Class_define",
                "AIBinder_Class_define( /*c*/ \"a\" \"b\", x, y, z)", null, "\"a\" \"b\"", null),
            new("ND-13", "// continued\\\r\nAIBinder_Class_define(\"fake\",a,b,c);", "cpp", Report()),
            new("ND-14", "/* unclosed", "rust", Report(issues: [new JsonObject { ["position"] = 0, ["reason"] = "Unclosed block comment" }])),
            new("ND-15", "r###\"unclosed", "rust", Report(issues: [new JsonObject { ["position"] = 0, ["reason"] = "Unclosed raw literal" }])),
            new("ND-16", "\"unclosed", "cpp", Report(issues: [new JsonObject { ["position"] = 0, ["reason"] = "Unclosed string literal" }])),
            new("ND-17", "char c='('; auto s=\"AIBinder_Class_define(\";", "cpp", Report()),
            Call("ND-18", "cpp", "AIBinder_Class_define(\"bad\\x41\",a,b,c)", "AIBinder_Class_define", "AIBinder_Class_define(\"bad\\x41\",a,b,c)", null, "\"bad\\x41\"", null),
            new("ND-19", "declare_binder_interface! { IFoo[\"x\"] [) }", "rust", Report([new JsonObject { ["family"] = "declare_binder_interface", ["line"] = 1,
                ["status"] = "unclosed-token-form", ["raw"] = "declare_binder_interface! { IFoo[\"x\"] [) }", ["descriptor_literal"] = null, ["compiled_endpoint_proven"] = false }])),
            Call("ND-20", "cpp", unicodeMacro, "IMPLEMENT_META_INTERFACE", unicodeMacro, "Foo", unicodeLiteral, "android.Foo"),
            Call("ND-21", "cpp", excessiveMacro, "IMPLEMENT_META_INTERFACE", excessiveMacro, "Foo", excessiveLiteral, null),
            new("ND-22", "auto s=R\"" + delimiter + "(AIBinder_Class_define(\"fake\",a,b,c))" + delimiter + "\";", "cpp", Report())
        ];
    }
    public static IEnumerable<object[]> Cases => All.Select(test => new object[] { test.Id });
    public static IEnumerable<object[]> Mutations => All.SelectMany(test => SourceMutationCases.For(test.MutationCase()));
    public static NativeDescriptorCase Get(string id) => All.Single(test => test.Id == id);
    public static void AssertContract(NativeDescriptorCase test, INativeDescriptorSourceFacts provider) =>
        Assert.True(JsonNode.DeepEquals(test.Expected, provider.Parse(test.Text, test.Language)), "Descriptor token evidence differs: " + test.Id);
    public static void AssertCapability(Func<INativeDescriptorSourceFacts?> capability)
    {
        var provider = capability(); Assert.NotNull(provider);
        AssertContract(Get("ND-01"), provider);
    }
    public static void AssertInvalid(string language, INativeDescriptorSourceFacts provider)
    {
        var error = Assert.Throws<ArgumentException>(() => provider.Parse("AIBinder_Class_define(\"x\",a,b,c)", language));
        Assert.Contains("Explicit source language required", error.Message, StringComparison.Ordinal);
    }
}
