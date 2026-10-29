using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record AnnotationArchiveMember(string Name, byte[] Bytes);
public sealed record SdkAnnotationArchiveCase(string Id, AnnotationArchiveMember[] Members, JsonObject Expected)
{
    public byte[] Input() => SdkAnnotationArchiveContractCases.Zip(Members);
    public SourceExtractionCase MutationCase() => new(Id, "sdk-annotation-archive", "binary-fixture", StructuralParseStatus.Clean, [Expected], [], "External annotation archive raw evidence, deterministic member/item/annotation order and exact source spelling facts");
}

public static class SdkAnnotationArchiveContractCases
{
    public static byte[] Zip(IEnumerable<AnnotationArchiveMember> members)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            foreach (var member in members)
            {
                var entry = zip.CreateEntry(member.Name, CompressionLevel.NoCompression);
                entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                using var content = entry.Open(); content.Write(member.Bytes);
            }
        return stream.ToArray();
    }
    private static AnnotationArchiveMember Member(string name, string xml) => new(name, Encoding.UTF8.GetBytes(xml));
    private static JsonObject Annotation() => new()
    {
        ["annotation_ordinal"] = 0, ["name"] = "android.annotation.RequiresPermission", ["arguments"] = "value=\"example.permission.P\"",
        ["raw"] = "<annotation name=\"android.annotation.RequiresPermission\"><val name=\"value\" val=\"&quot;example.permission.P&quot;\" /></annotation>",
        ["values"] = new JsonArray(new JsonObject { ["name"] = "value", ["val"] = "\"example.permission.P\"" })
    };
    private static JsonObject Item(string member, int ordinal, string fixtureId, params JsonObject[] annotations)
    {
        var item = (JsonObject)SdkAnnotationTargetContractCases.Get(fixtureId).Expected.DeepClone();
        item["archive_member"] = member; item["item_ordinal"] = ordinal;
        item["annotations"] = new JsonArray(annotations.Select(value => (JsonNode)value.DeepClone()).ToArray());
        return item;
    }
    private static JsonObject Issue(string code, string reason, string member, string raw, string encoding = "utf-8") => new()
    { ["code"] = code, ["reason"] = reason, ["archive_member"] = member, ["raw"] = raw, ["raw_encoding"] = encoding };
    private static JsonObject Report(string status, string[] members, JsonObject[] items, int documents, int annotations, params JsonObject[] issues) => new()
    {
        ["status"] = status, ["members"] = new JsonArray(members.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
        ["items"] = new JsonArray(items.Select(value => (JsonNode)value.DeepClone()).ToArray()),
        ["counts"] = new JsonObject { ["xml_documents"] = documents, ["items"] = items.Length, ["annotations"] = annotations },
        ["issues"] = new JsonArray(issues.Select(value => (JsonNode)value.DeepClone()).ToArray())
    };
    private const string PermissionXml = "<root><item name=\"a.A void go(java.lang.String)\"><annotation name=\"android.annotation.RequiresPermission\"><val name=\"value\" val=\"&quot;example.permission.P&quot;\"/></annotation></item></root>";
    private const string ParameterXml = "<root><item name=\"a.A void go(java.lang.String) 0\"/></root>";
    public static IReadOnlyList<SdkAnnotationArchiveCase> All { get; } =
    [
        new("ANN-Z01", [Member("a.xml", PermissionXml)], Report("parsed", ["a.xml"], [Item("a.xml", 0, "ANN-T01", Annotation())], 1, 1)),
        new("ANN-Z02", [Member("z.xml", ParameterXml), Member("a.xml", PermissionXml)], Report("parsed", ["a.xml", "z.xml"], [Item("a.xml", 0, "ANN-T01", Annotation()), Item("z.xml", 0, "ANN-T05")], 2, 1)),
        new("ANN-Z03", [Member("note.txt", "retained"), Member("a.xml", ParameterXml)], Report("parsed", ["a.xml", "note.txt"], [Item("a.xml", 0, "ANN-T05")], 1, 0,
            Issue("unsupported", "Unrecognized annotation archive member", "note.txt", "note.txt"))),
        new("ANN-Z04", [Member("bad.xml", "<root>"), Member("good.xml", ParameterXml)], Report("partial", ["bad.xml", "good.xml"], [Item("good.xml", 0, "ANN-T05")], 1, 0,
            Issue("annotation-xml", "Malformed external annotation XML", "bad.xml", "<root>"))),
        new("ANN-Z05", [], Report("parsed", [], [], 0, 0)),
        new("ANN-Z06", [new("bad.xml", [0xff]), Member("good.xml", ParameterXml)], Report("partial", ["bad.xml", "good.xml"], [Item("good.xml", 0, "ANN-T05")], 1, 0,
            Issue("annotation-xml", "Non-UTF8 external annotation XML retained losslessly", "bad.xml", "/w==", "base64"))),
        // Legacy namelist preserves duplicate names, but archive.read(name)
        // selects the LAST duplicate. Each sorted occurrence is processed.
        new("ANN-Z07", [Member("a.xml", PermissionXml), Member("a.xml", ParameterXml)], Report("parsed", ["a.xml", "a.xml"], [Item("a.xml", 0, "ANN-T05"), Item("a.xml", 0, "ANN-T05")], 2, 0)),
        new("ANN-Z08", [Member("a.xml", "<root><item name=\"a.A\"/><wrapper><item name=\"ignored.Nested\"/></wrapper><item xmlns=\"urn:ignored\" name=\"ignored.Namespaced\"/></root>")],
            Report("parsed", ["a.xml"], [Item("a.xml", 0, "ANN-T08")], 1, 0)),
        new("ANN-Z09", [Member("a.xml", "<root><item name=\"a.A\"/><item name=\"a.A T field\"/></root>")], Report("parsed", ["a.xml"], [Item("a.xml", 0, "ANN-T08"), Item("a.xml", 1, "ANN-T10")], 1, 0))
    ];
    public static IEnumerable<object[]> Cases => All.Select(test => new object[] { test.Id });
    public static IEnumerable<object[]> Mutations => All.SelectMany(test => SourceMutationCases.For(test.MutationCase()));
    public static SdkAnnotationArchiveCase Get(string id) => All.Single(test => test.Id == id);
    public static void AssertContract(SdkAnnotationArchiveCase test, ISdkAnnotationArchiveSourceFacts provider)
    {
        var input = test.Input(); var original = input.ToArray();
        var observed = provider.Parse(input);
        Assert.True(JsonNode.DeepEquals(test.Expected, observed), "Archive concern evidence differs: " + test.Id);
        Assert.Equal(original, input);
    }
    public static IEnumerable<object[]> Failures => new[] { new object[] { "ANN-Z-invalid-empty", Array.Empty<byte>() }, new object[] { "ANN-Z-invalid-header", new byte[] { 0x50, 0x4b, 0xff } } };
    public static void AssertFailure(byte[] input, ISdkAnnotationArchiveSourceFacts provider) => Assert.Throws<InvalidDataException>(() => provider.Parse(input));
}
