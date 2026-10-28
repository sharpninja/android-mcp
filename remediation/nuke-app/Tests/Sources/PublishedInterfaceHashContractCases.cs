using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record PublishedHashCase(string Id, string Format, string Input, JsonObject Expected)
{
    public SourceExtractionCase MutationCase() => new(Id, "published-hash", Input, StructuralParseStatus.Clean, [Expected], [], "interface_hash_facts four legacy test methods plus pinned lexical boundaries");
}
public static class PublishedInterfaceHashContractCases
{
    private static JsonObject Aidl(int line, string value, string? raw = null) => new() { ["line"] = line, ["literal"] = value, ["format"] = "published-aidl-hash-hex40", ["raw"] = raw ?? value };
    private static JsonObject Hidl(int line, string value, string identifier, string? raw = null) => new() { ["line"] = line, ["literal"] = value, ["declared_identifier"] = identifier, ["format"] = "published-hidl-hash-hex64-and-identifier", ["raw"] = raw ?? value + " " + identifier };
    private static JsonObject Result(string format, JsonObject[] facts, JsonObject[]? unknown = null) => new()
    {
        ["facts"] = new JsonArray(facts.Cast<JsonNode>().ToArray()), ["unknown_lines"] = new JsonArray((unknown ?? []).Cast<JsonNode>().ToArray()),
        ["compiled_runtime_hash_proven"] = false, ["android_applicability_proven"] = false,
        ["semantic_note"] = format == "aidl" ? "All literal lines retained; selecting a generated interface hash requires compiler/build/version linkage" : "Repeated identifiers and compatibility hash lines preserved; HIDL package versions are not Android API versions"
    };
    private static JsonObject Unknown(int line, string raw) => new() { ["line"] = line, ["raw"] = raw };
    private static readonly string A40 = new('a', 40), B40 = new('b', 40), A64 = new('a', 64), B64 = new('b', 64);
    public static IReadOnlyList<PublishedHashCase> All { get; } =
    [
        new("HASH-01", "aidl", "# source comment\n" + A40 + "\n" + B40 + "\n", Result("aidl", [Aidl(2, A40), Aidl(3, B40)])),
        new("HASH-02", "aidl", "not-frozen\n", Result("aidl", [], [Unknown(1, "not-frozen")])),
        new("HASH-03", "aidl", A40 + " # trailing\r\n", Result("aidl", [Aidl(1, A40, A40 + " # trailing")])),
        new("HASH-04", "aidl", new string('A', 40), Result("aidl", [Aidl(1, new string('A', 40))])),
        new("HASH-05", "aidl", "", Result("aidl", [])),
        new("HASH-06", "hidl", A64 + " android.hardware.foo@1.0::IFoo\n" + B64 + " android.hardware.foo@1.0::IFoo # old compatibility hash\n",
            Result("hidl", [Hidl(1, A64, "android.hardware.foo@1.0::IFoo"), Hidl(2, B64, "android.hardware.foo@1.0::IFoo", B64 + " android.hardware.foo@1.0::IFoo # old compatibility hash")])),
        new("HASH-07", "hidl", "opaque declaration\n", Result("hidl", [], [Unknown(1, "opaque declaration")])),
        new("HASH-08", "hidl", A64 + " android.hardware.foo@2.7::IFoo.Inner", Result("hidl", [Hidl(1, A64, "android.hardware.foo@2.7::IFoo.Inner")])),
        new("HASH-09", "hidl", A40 + " android.hardware.foo@1.0::IFoo", Result("hidl", [], [Unknown(1, A40 + " android.hardware.foo@1.0::IFoo")])),
        new("HASH-10", "hidl", "# comment\n\n", Result("hidl", [])),
        new("HASH-11", "aidl", A40 + "\u0085" + B40 + "\u2028", Result("aidl", [Aidl(1, A40), Aidl(2, B40)])),
        new("HASH-12", "aidl", "a\rb\r\nc\vd\fe\u001cf\u001dg\u001e", Result("aidl", [], [Unknown(1, "a"), Unknown(2, "b"), Unknown(3, "c"), Unknown(4, "d"), Unknown(5, "e"), Unknown(6, "f"), Unknown(7, "g")]))
    ];
    public static IEnumerable<object[]> Cases => All.Select(x => new object[] { x.Id });
    public static IEnumerable<object[]> Mutations => All.SelectMany(x => SourceMutationCases.For(x.MutationCase()));
    public static PublishedHashCase Get(string id) => All.Single(x => x.Id == id);
    public static void AssertContract(PublishedHashCase test, IPublishedInterfaceHashSourceFacts provider) =>
        Assert.True(JsonNode.DeepEquals(test.Expected, test.Format == "aidl" ? provider.AidlHashes(test.Input) : provider.HidlHashes(test.Input)), "Published source hash facts differ: " + test.Id);
}
