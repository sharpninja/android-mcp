using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record NativeDescriptorValueCase(string Id, JsonNode? Text, JsonNode? Language, JsonObject? Expected, string? Error = null);
public static class NativeDescriptorValueContractCases
{
    public static IReadOnlyList<NativeDescriptorValueCase> All { get; } = Create();
    private static IReadOnlyList<NativeDescriptorValueCase> Create()
    {
        var result = new List<NativeDescriptorValueCase>();
        foreach (var language in new[] { "cpp", "rust" })
        {
            foreach (var text in new JsonNode[] { new JsonArray(), new JsonObject(), JsonValue.Create("")! })
                result.Add(new("ND-VALUE-EMPTY-" + result.Count, text, JsonValue.Create(language), new JsonObject { ["facts"] = new JsonArray(), ["lexical_issues"] = new JsonArray(), ["compiled_or_runtime_proven"] = false }));
            foreach (var text in new JsonNode?[] { null, JsonValue.Create(true), JsonValue.Create(7), new JsonArray(1), new JsonObject { ["x"] = 1 } })
                result.Add(new("ND-VALUE-INVALID-TEXT-" + result.Count, text, JsonValue.Create(language), null, "text"));
        }
        foreach (var language in new JsonNode?[] { null, JsonValue.Create(1), JsonValue.Create(true), new JsonArray(), new JsonObject(), JsonValue.Create("CPP"), JsonValue.Create("") })
            result.Add(new("ND-VALUE-LANGUAGE-FIRST-" + result.Count, null, language, null, "language"));
        foreach (var original in NativeDescriptorContractCases.All)
            result.Add(new("ND-VALUE-" + original.Id, JsonValue.Create(original.Text), JsonValue.Create(original.Language), (JsonObject)original.Expected.DeepClone()));
        return result;
    }
    public static IEnumerable<object[]> Cases => All.Select(x => new object[] { x.Id });
    public static NativeDescriptorValueCase Get(string id) => All.Single(x => x.Id == id);
    public static void AssertContract(NativeDescriptorValueCase test, INativeDescriptorValueSourceFacts provider)
    {
        var beforeText = test.Text?.DeepClone(); var beforeLanguage = test.Language?.DeepClone();
        if (test.Error is null) Assert.True(JsonNode.DeepEquals(test.Expected, provider.ParseValue(test.Text, test.Language)), test.Id);
        else Assert.Contains(test.Error, Assert.Throws<ArgumentException>(() => provider.ParseValue(test.Text, test.Language)).Message, StringComparison.Ordinal);
        Assert.True(JsonNode.DeepEquals(beforeText, test.Text)); Assert.True(JsonNode.DeepEquals(beforeLanguage, test.Language));
    }
}
public sealed class ScriptedDescriptorValue(NativeDescriptorValueCase test, bool wrong = false, bool mutate = false) : INativeDescriptorValueSourceFacts
{
    public JsonObject ParseValue(JsonNode? text, JsonNode? language)
    {
        if (mutate && text is JsonArray array) array.Add("changed");
        if (mutate && text is JsonObject value) value["changed"] = true;
        if (test.Error is not null && !wrong) throw new ArgumentException(test.Error);
        return wrong ? new JsonObject { ["wrong"] = true } : (JsonObject)test.Expected!.DeepClone();
    }
}
