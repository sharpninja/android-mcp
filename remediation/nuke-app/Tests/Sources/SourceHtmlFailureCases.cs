using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record HtmlEntityAuditFailure(string Id, byte[] Official, string Legacy, string Message);
public static class SourceHtmlFailureCases
{
    private static byte[] Valid => JsonSerializer.SerializeToUtf8Bytes(new JsonObject { ["&amp;"] = new JsonObject { ["codepoints"] = new JsonArray(38), ["characters"] = "&" } });
    private const string Legacy = "html5 = {\n 'amp;': '&',\n}\n";
    private static byte[] Duplicate()
    {
        using var bytes = new MemoryStream(); using (var writer = new Utf8JsonWriter(bytes))
        { writer.WriteStartObject(); writer.WriteString("&amp;", "first"); writer.WriteString("&amp;", "second"); writer.WriteEndObject(); }
        return bytes.ToArray();
    }
    public static IReadOnlyList<HtmlEntityAuditFailure> All { get; } =
    [
        new("DATA-ERR01", System.Text.Encoding.UTF8.GetBytes("invalid"), Legacy, "entity JSON"),
        new("DATA-ERR02", JsonSerializer.SerializeToUtf8Bytes(new JsonArray()), Legacy, "entity JSON"),
        new("DATA-ERR03", Duplicate(), Legacy, "entity JSON"),
        new("DATA-ERR04", JsonSerializer.SerializeToUtf8Bytes(new JsonObject { ["amp;"] = new JsonObject { ["codepoints"] = new JsonArray(38), ["characters"] = "&" } }), Legacy, "entity JSON"),
        new("DATA-ERR05", JsonSerializer.SerializeToUtf8Bytes(new JsonObject { ["&amp;"] = new JsonObject { ["codepoints"] = new JsonArray(65), ["characters"] = "&" } }), Legacy, "entity JSON"),
        new("DATA-ERR06", JsonSerializer.SerializeToUtf8Bytes(new JsonObject { ["&amp;"] = new JsonObject { ["codepoints"] = new JsonArray(0xd800), ["characters"] = "x" } }), Legacy, "entity JSON"),
        new("DATA-ERR07", Valid, "other = {}\n", "legacy table"),
        new("DATA-ERR08", Valid, "html5 = {\n 'amp;': '&',\n", "legacy table"),
        new("DATA-ERR09", Valid, "html5 = {\n 'amp;': '&',\n 'amp;': 'x',\n}\n", "legacy table"),
        new("DATA-ERR10", Valid, "html5 = {\n 'amp;': dangerous(),\n}\n", "legacy table"),
        new("DATA-ERR11", Valid, "html5 = {\n 'amp;': '\\N{AMPERSAND}',\n}\n", "legacy table"),
        new("DATA-ERR12", Valid, "html5 = {\n 'amp;': '&',\n}\nhtml5 = {\n 'amp;': '&',\n}\n", "legacy table")
    ];
    public static IEnumerable<object[]> Cases => All.Select(x => new object[] { x.Id });
    public static HtmlEntityAuditFailure Get(string id) => All.Single(x => x.Id == id);
    public static void AssertContract(HtmlEntityAuditFailure test, IHtmlEntityDataAudit provider)
    {
        var bytes = test.Official.ToArray(); var error = Assert.Throws<FormatException>(() => provider.Compare(bytes, test.Legacy));
        Assert.Contains(test.Message, error.Message, StringComparison.OrdinalIgnoreCase); Assert.Equal(test.Official, bytes);
    }
}
