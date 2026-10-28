using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record PermissionExpressionCase(string Id, JsonArray Annotations, JsonObject Expected,
    IReadOnlyDictionary<string, string>? Symbols = null, bool Aidl = false)
{
    public SourceExtractionCase MutationCase() => new(Id, "permission-expression", "fixture", StructuralParseStatus.Clean, [Expected], [], "parsers.permission/foundation_extract.permission_ast literal contracts");
}

public static class PermissionExpressionContractCases
{
    private static JsonObject Unknown(string reason, string? raw = null)
    { var result = new JsonObject { ["op"] = "unknown", ["reason"] = reason }; if (raw is not null) result["raw"] = raw; return result; }
    private static JsonObject Leaf(string value) => new() { ["op"] = "permission", ["name"] = value };
    private static JsonObject Group(string op, params JsonObject[] children) => new() { ["op"] = op, ["children"] = new JsonArray(children.Cast<JsonNode>().ToArray()) };
    private static JsonObject Annotation(string name, string args = "") => new() { ["name"] = name, ["arguments"] = args, ["raw"] = "@" + name + (args.Length == 0 ? "" : "(" + args + ")") };
    private static PermissionExpressionCase Case(string id, string name, string args, JsonObject expected, bool aidl = false) => new(id, new JsonArray(Annotation(name, args)), expected, Aidl: aidl);
    private const string Absent = "No declared recognized permission metadata; absence is not permission-free";
    private const string Symbolic = "Unresolved symbolic permission expression";
    public static IReadOnlyList<PermissionExpressionCase> All { get; } = Create();
    public static IEnumerable<object[]> Cases => All.Select(x => new object[] { x.Id });
    public static IEnumerable<object[]> Mutations => All.SelectMany(x => SourceMutationCases.For(x.MutationCase()));
    public static PermissionExpressionCase Get(string id) => All.Single(x => x.Id == id);
    private static IReadOnlyList<PermissionExpressionCase> Create()
    {
        var symbol = Leaf("android.permission.CAMERA"); symbol["x-symbol"] = "android.Manifest.permission.CAMERA";
        return
        [
            Case("PERM-01", "RequiresPermission", "value=\"example.permission.X\"", Leaf("example.permission.X")),
            Case("PERM-02", "RequiresPermission", "allOf={\"example.permission.A\",\"example.permission.B\"}", Group("all", Leaf("example.permission.A"), Leaf("example.permission.B"))),
            Case("PERM-03", "RequiresPermission", "anyOf={\"example.permission.A\",\"example.permission.B\"},conditional=true", new() { ["op"] = "conditional", ["condition"] = Unknown("Annotation marks conditional but does not identify runtime predicate"), ["requirement"] = Group("any", Leaf("example.permission.A"), Leaf("example.permission.B")) }),
            Case("PERM-04", "RequiresPermission.Read", "@RequiresPermission(\"example.permission.X\")", new() { ["op"] = "access", ["access"] = "read", ["requirement"] = Leaf("example.permission.X") }),
            Case("PERM-05", "PermissionManuallyEnforced", "", new() { ["op"] = "manual", ["reason"] = "Manual enforcement declared; backend checks unknown" }),
            Case("PERM-06", "RequiresNoPermission", "", new() { ["op"] = "explicit-none" }),
            new("PERM-07", new(), Unknown(Absent)),
            new("PERM-08", new(Annotation("RequiresPermission", "android.Manifest.permission.CAMERA")), symbol, new Dictionary<string,string> { ["android.Manifest.permission.CAMERA"] = "android.permission.CAMERA" }),
            Case("PERM-09", "EnforcePermission", "\"X\"", Unknown("Unqualified permission literal; namespace not proven", "X"), true),
            Case("PERM-10", "RequiresPermission", "\"carrier privileges\"", Unknown("Source annotation contains a symbolic reference or non-permission predicate, not a proven literal permission name", "carrier privileges")),
            Case("PERM-11", "RequiresPermission.Read", "", new() { ["op"] = "access", ["access"] = "read", ["requirement"] = Unknown("Unresolved nested annotation", "") }),
            Case("PERM-12", "Read", "\"example.permission.X\"", Unknown(Absent)),
            Case("PERM-13", "Nullable", "", Unknown(Absent)),
            Case("PERM-14", "EnforcePermission", "allOf=[\"example.permission.A\", \"example.permission.B\"]", Group("all", Leaf("example.permission.A"), Leaf("example.permission.B")), true),
            Case("PERM-15", "RequiresPermission", "CAMERA", Unknown(Symbolic, "CAMERA")),
            Case("PERM-16", "RequiresPermission", "\"Manifest.permission.CAMERA\"", Unknown("Source annotation contains a symbolic reference or non-permission predicate, not a proven literal permission name", "Manifest.permission.CAMERA")),
            Case("PERM-17", "RequiresPermission", "", Unknown("Permission expression absent or unsupported", "")),
            Case("PERM-18", "RequiresPermission", "allOf={}", Group("all")),
            Case("PERM-19", "RequiresPermission", "value=\"example.permission.X\",extra=true", Group("all", Leaf("example.permission.X"), Unknown("Unrecognized annotation attributes", "value=\"example.permission.X\",extra=true"))),
            Case("PERM-20", "RequiresPermission", "value=\"example.permission.X\",conditional=false", Leaf("example.permission.X")),
            Case("PERM-21", "RequiresPermission.Write", "@RequiresPermission(\"example.permission.X\")", new() { ["op"] = "access", ["access"] = "write", ["requirement"] = Leaf("example.permission.X") }),
            Case("PERM-22", "RequiresPermission", "value=\"example.permission.X\",anyOf={\"example.permission.A\"}", Group("all", Leaf("example.permission.X"), Group("any", Leaf("example.permission.A")))),
            Case("PERM-23", "RequiresPermission", "\"example.permission.\\u0041\"", Unknown(Symbolic, "\"example.permission.\\u0041\"")),
            new("PERM-24", new(Annotation("RequiresNoPermission"), Annotation("PermissionManuallyEnforced")), Group("all", new() { ["op"] = "explicit-none" }, new() { ["op"] = "manual", ["reason"] = "Manual enforcement declared; backend checks unknown" })),
            new("PERM-25", new(Annotation("RequiresPermission", "\"android.Manifest.permission.CAMERA\"")), (JsonObject)symbol.DeepClone(), new Dictionary<string,string> { ["android.Manifest.permission.CAMERA"] = "android.permission.CAMERA" })
        ];
    }

    public static void AssertContract(PermissionExpressionCase test, IPermissionExpressionSourceFacts provider)
    {
        var annotations = (JsonArray)test.Annotations.DeepClone();
        var symbols = test.Symbols?.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        var result = provider.Permission(annotations, symbols, test.Aidl);
        Assert.True(JsonNode.DeepEquals(test.Expected, result), $"Permission expression differs: {test.Id}");
        Assert.True(JsonNode.DeepEquals(test.Annotations, annotations), "Source annotations mutated");
        Assert.Equal(test.Symbols?.OrderBy(x => x.Key, StringComparer.Ordinal), symbols?.OrderBy(x => x.Key, StringComparer.Ordinal));
    }
}
