using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record SoongLiteralCase(string Id, JsonObject Node, string? Property, JsonNode? Expected);
public sealed record SoongMatchCase(string Id, string Build, string[] Patterns, string[] Excluded, string[] Paths, string[]? Expected, string? Failure = null);
public sealed record SoongManifestPathCase(string Id, string Build, JsonObject Module, string? Expected);
public static class SoongAuxiliaryCases
{
    private static JsonObject Lit(JsonNode value) => SoongContractCases.Lit("fixture-literal", value);
    private static JsonObject Property(string key, JsonObject value) => SoongContractCases.Property(key, value);
    private static JsonObject Object(bool complete, params JsonObject[] properties) => SoongContractCases.Object("fixture-object", complete, properties);
    public static IReadOnlyList<SoongLiteralCase> Literals { get; } =
    [
        new("LIT-01", (JsonObject)SoongContractCases.Get("SOONG-01").Expected[0]!["properties"]!.DeepClone(), "name", JsonValue.Create("foo")),
        new("LIT-02", (JsonObject)SoongContractCases.Get("SOONG-01").Expected[0]!["properties"]!.DeepClone(), "frozen", JsonValue.Create(true)),
        new("LIT-03", (JsonObject)SoongContractCases.Get("SOONG-01").Expected[0]!["properties"]!.DeepClone(), "versions_with_info", new JsonArray(new JsonObject { ["version"] = "1", ["imports"] = new JsonArray("bar-V2") })),
        new("LIT-04", (JsonObject)SoongContractCases.Get("SOONG-02").Expected[0]!["properties"]!.DeepClone(), "srcs", null),
        new("LIT-05", (JsonObject)SoongContractCases.Get("SOONG-02").Expected[0]!["properties"]!.DeepClone(), "versions", null),
        new("LIT-06", (JsonObject)SoongContractCases.Get("SOONG-04").Expected[0]!["properties"]!.DeepClone(), "name", null),
        new("LIT-07", Object(true, Property("x", Lit(false))), "x", JsonValue.Create(false)),
        new("LIT-08", Object(true, Property("x", Lit(0))), "x", JsonValue.Create(0)),
        new("LIT-09", Object(false, Property("x", Lit("value"))), null, null),
        new("LIT-10", Object(true), null, new JsonObject()),
        new("LIT-11", Object(true, Property("x", Lit(1)), Property("x", Lit(2))), null, null),
        new("LIT-12", Object(true), "absent", null),
        new("LIT-13", new() { ["kind"] = "array", ["complete"] = true, ["items"] = new JsonArray(Lit("a"), new JsonObject { ["kind"] = "unresolved-expression", ["raw"] = "expression" }) }, null, null)
    ];
    public static IReadOnlyList<SoongMatchCase> Matches { get; } =
    [
        new("MATCH-01", "dir/Android.bp", ["p/**/*.aidl"], ["p/test/**"], ["dir/p/A.aidl", "dir/p/sub/B.aidl", "dir/p/test/T.aidl", "dir/aidl_api/x/1/p/A.aidl"], ["dir/p/A.aidl", "dir/p/sub/B.aidl"]),
        new("MATCH-02", "dir/Android.bp", ["../*.aidl"], [], ["outside.aidl"], null, "separate proof"),
        new("MATCH-03", "dir/Android.bp", [":filegroup"], [], ["dir/A.aidl"], null, "separate proof"),
        new("MATCH-04", "dir/Android.bp", ["/absolute.aidl"], [], ["dir/A.aidl"], null, "separate proof"),
        new("MATCH-05", "dir/Android.bp", ["p/*.aidl"], [], ["dir/p/sub/B.aidl", "dir/p/A.aidl", "other/p/B.aidl", "dir/p/.hidden.aidl", "dir/p/A.aidl"], ["dir/p/.hidden.aidl", "dir/p/A.aidl"]),
        new("MATCH-06", "Android.bp", [], [], ["A.aidl"], []),
        new("MATCH-07", "dir/Android.bp", ["p/*.aidl"], ["../escape"], ["dir/p/A.aidl"], null, "separate proof")
    ];
    public static IReadOnlyList<SoongManifestPathCase> Manifests { get; } =
    [
        new("PATH-01", "dir/Android.bp", (JsonObject)SoongContractCases.Get("SOONG-08").Expected[1]!.DeepClone(), null),
        new("PATH-02", "dir/Android.bp", (JsonObject)SoongContractCases.Get("SOONG-08").Expected[2]!.DeepClone(), "dir/bar.json"),
        new("PATH-03", "dir/Android.bp", (JsonObject)SoongContractCases.Get("SOONG-08").Expected[0]!.DeepClone(), "dir/m.json"),
        new("PATH-04", "Android.bp", (JsonObject)SoongContractCases.Get("SOONG-10").Expected[0]!.DeepClone(), null),
        new("PATH-05", "Android.bp", (JsonObject)SoongContractCases.Get("SOONG-11").Expected[0]!.DeepClone(), null),
        new("PATH-06", "Android.bp", (JsonObject)SoongContractCases.Get("SOONG-12").Expected[0]!.DeepClone(), null)
    ];
    public static IEnumerable<object[]> LiteralCases => Literals.Select(x => new object[] { x.Id });
    public static IEnumerable<object[]> MatchCases => Matches.Select(x => new object[] { x.Id });
    public static IEnumerable<object[]> ManifestCases => Manifests.Select(x => new object[] { x.Id });
    public static void AssertLiteral(SoongLiteralCase test, ISoongSourceFacts provider)
    {
        var node = (JsonObject)test.Node.DeepClone(); Assert.True(JsonNode.DeepEquals(test.Expected, provider.Literal(node, test.Property)), "Static literal differs: " + test.Id); Assert.True(JsonNode.DeepEquals(test.Node, node));
    }
    public static void AssertMatch(SoongMatchCase test, ISoongSourceFacts provider)
    {
        var patterns = test.Patterns.ToArray(); var excluded = test.Excluded.ToArray(); var paths = test.Paths.ToArray();
        if (test.Failure is null) Assert.Equal(test.Expected, provider.MatchSources(test.Build, patterns, excluded, paths));
        else Assert.Contains(test.Failure, Assert.Throws<ArgumentException>(() => provider.MatchSources(test.Build, patterns, excluded, paths)).Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(test.Patterns, patterns); Assert.Equal(test.Excluded, excluded); Assert.Equal(test.Paths, paths);
    }
    public static void AssertManifest(SoongManifestPathCase test, ISoongSourceFacts provider)
    {
        var module = (JsonObject)test.Module.DeepClone(); Assert.Equal(test.Expected, provider.ExplicitManifestPath(test.Build, module)); Assert.True(JsonNode.DeepEquals(test.Module, module));
    }
}
