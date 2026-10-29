using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record SoongModuleCase(string Id, string Input, SoongModuleFamily Family, JsonArray Expected)
{
    public SourceExtractionCase MutationCase() => new(Id, "soong", Input, StructuralParseStatus.Clean, Expected.OfType<JsonObject>().ToArray(), [], "interface_module_facts/apex_manifest_facts legacy static module fixtures read as text");
}
public static class SoongContractCases
{
    internal static JsonObject Lit(string raw, JsonNode value) => new() { ["kind"] = "literal", ["value"] = value, ["raw"] = raw };
    internal static JsonObject Property(string name, JsonObject value) => new() { ["name"] = name, ["value"] = value };
    internal static JsonObject Object(string raw, bool complete, params JsonObject[] properties) => new() { ["kind"] = "object", ["items"] = new JsonArray(properties.Cast<JsonNode>().ToArray()), ["complete"] = complete, ["raw"] = raw };
    private static JsonObject Array(string raw, params JsonObject[] values) => new() { ["kind"] = "array", ["items"] = new JsonArray(values.Cast<JsonNode>().ToArray()), ["complete"] = true, ["raw"] = raw };
    private static JsonObject Unknown(string raw) => new() { ["kind"] = "unresolved-expression", ["raw"] = raw };
    private static JsonObject Module(string kind, string raw, JsonObject properties, int line = 1) => new()
    {
        ["kind"] = kind, ["line"] = line, ["properties"] = properties, ["raw"] = raw,
        ["status"] = properties["complete"]!.GetValue<bool>() ? "static-properties-parsed" : "unclosed-module-block",
        ["runtime_module_identity_proven"] = false, ["android_applicability_proven"] = false
    };
    private static JsonObject Apex(string kind, string raw, JsonObject properties, JsonObject literal) => new()
    {
        ["kind"] = kind, ["line"] = 1, ["properties"] = properties, ["literal_properties"] = literal, ["raw"] = raw, ["installed_module_version_proven"] = false
    };
    private static JsonObject ApexLiterals(string name, JsonNode? manifest = null, JsonNode? defaults = null) => new()
    { ["name"] = name, ["manifest"] = manifest, ["defaults"] = defaults, ["apex_name"] = null, ["version"] = null, ["min_sdk_version"] = null, ["updatable"] = null, ["package_name"] = null };
    public static IReadOnlyList<SoongModuleCase> All { get; } = Create();
    private static IReadOnlyList<SoongModuleCase> Create()
    {
        const string first = "aidl_interface { name:\"foo\", srcs:[\"p/*.aidl\"], stability:\"vintf\", versions_with_info:[{version:\"1\",imports:[\"bar-V2\"]}], frozen:true, }";
        var firstProperties = Object(first[first.IndexOf('{')..], true,
            Property("name", Lit("\"foo\"", "foo")), Property("srcs", Array("[\"p/*.aidl\"]", Lit("\"p/*.aidl\"", "p/*.aidl"))), Property("stability", Lit("\"vintf\"", "vintf")),
            Property("versions_with_info", Array("[{version:\"1\",imports:[\"bar-V2\"]}]", Object("{version:\"1\",imports:[\"bar-V2\"]}", true,
                Property("version", Lit("\"1\"", "1")), Property("imports", Array("[\"bar-V2\"]", Lit("\"bar-V2\"", "bar-V2")))))), Property("frozen", Lit("true", true)));
        const string expression = "aidl_interface { name:\"foo\", srcs: common + [\"x.aidl\"], versions:VERSIONS, }";
        const string real = "hidl_interface { name:\"real\", }";
        const string duplicate = "aidl_interface { name:\"one\", name:\"two\", }";
        const string unclosed = "aidl_interface { name:\"bad\",";
        const string left = "aidl_interface { name:\"a\", }"; const string right = "hidl_interface { name:\"b\", }";
        const string missing = "aidl_interface { name:";
        const string defaults = "apex_defaults {name:\"d\",manifest:\"m.json\"}";
        const string inherited = "apex {name:\"foo\",defaults:[\"d\"]}";
        const string explicitManifest = "apex {name:\"bar\",manifest:\"bar.json\"}";
        var result = new List<SoongModuleCase>
        {
            new("SOONG-01", first, SoongModuleFamily.Interfaces, new(Module("aidl_interface", first, firstProperties))),
            new("SOONG-02", expression, SoongModuleFamily.Interfaces, new(Module("aidl_interface", expression, Object(expression[expression.IndexOf('{')..], true,
                Property("name", Lit("\"foo\"", "foo")), Property("srcs", Unknown("common + [\"x.aidl\"]")), Property("versions", Unknown("VERSIONS")))))),
            new("SOONG-03", "// aidl_interface { name:\"fake\" }\n x=\"hidl_interface { fake }\"; " + real, SoongModuleFamily.Interfaces,
                new(Module("hidl_interface", real, Object("{ name:\"real\", }", true, Property("name", Lit("\"real\"", "real"))), 2))),
            new("SOONG-04", duplicate, SoongModuleFamily.Interfaces, new(Module("aidl_interface", duplicate, Object("{ name:\"one\", name:\"two\", }", true, Property("name", Lit("\"one\"", "one")), Property("name", Lit("\"two\"", "two")))))),
            new("SOONG-05", unclosed, SoongModuleFamily.Interfaces, new(Module("aidl_interface", unclosed, Object("{ name:\"bad\",", false, Property("name", Lit("\"bad\"", "bad")))))),
            new("SOONG-06", left + " " + right + " cc_library { name:\"unrelated\", }", SoongModuleFamily.Interfaces,
                new(Module("aidl_interface", left, Object("{ name:\"a\", }", true, Property("name", Lit("\"a\"", "a")))), Module("hidl_interface", right, Object("{ name:\"b\", }", true, Property("name", Lit("\"b\"", "b")))))),
            new("SOONG-07", missing, SoongModuleFamily.Interfaces, new(Module("aidl_interface", missing, Object("{ name:", false, Property("name", new() { ["kind"] = "missing-value", ["raw"] = "" }))))),
            new("SOONG-08", defaults + " " + inherited + " " + explicitManifest, SoongModuleFamily.Apex,
                new(Apex("apex_defaults", defaults, Object("{name:\"d\",manifest:\"m.json\"}", true, Property("name", Lit("\"d\"", "d")), Property("manifest", Lit("\"m.json\"", "m.json"))), ApexLiterals("d", "m.json")),
                    Apex("apex", inherited, Object("{name:\"foo\",defaults:[\"d\"]}", true, Property("name", Lit("\"foo\"", "foo")), Property("defaults", Array("[\"d\"]", Lit("\"d\"", "d")))), ApexLiterals("foo", defaults: new JsonArray("d"))),
                    Apex("apex", explicitManifest, Object("{name:\"bar\",manifest:\"bar.json\"}", true, Property("name", Lit("\"bar\"", "bar")), Property("manifest", Lit("\"bar.json\"", "bar.json"))), ApexLiterals("bar", "bar.json")))),
            new("SOONG-09", "cc_library { name:\"x\" }", SoongModuleFamily.Interfaces, new())
        };
        foreach (var (id, value, literal) in new[] { ("SOONG-10", "manifest_name", (string?)null), ("SOONG-11", "\":filegroup\"", ":filegroup"), ("SOONG-12", "\"../m.json\"", "../m.json") })
        {
            var input = "apex {name:\"foo\",manifest:" + value + "}";
            result.Add(new(id, input, SoongModuleFamily.Apex, new(Apex("apex", input, Object(input[input.IndexOf('{')..], true,
                Property("name", Lit("\"foo\"", "foo")), Property("manifest", literal is null ? Unknown(value) : Lit(value, literal))), ApexLiterals("foo", literal)))));
        }
        return result;
    }
    public static IEnumerable<object[]> Cases => All.Select(x => new object[] { x.Id });
    public static IEnumerable<object[]> Mutations => All.SelectMany(x => SourceMutationCases.For(x.MutationCase()));
    public static SoongModuleCase Get(string id) => All.Single(x => x.Id == id);
    public static void AssertContract(SoongModuleCase test, ISoongSourceFacts provider) => Assert.True(JsonNode.DeepEquals(test.Expected, provider.Modules(test.Input, test.Family)), "Static build facts differ: " + test.Id);
}
