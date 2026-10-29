using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class RecordingBinarySourceExtractor(string kind, Func<SourceDocument, ExtractionResult> response) : IBinarySourceExtractor
{
    public string Kind { get; } = kind;
    public List<SourceDocument> Received { get; } = [];
    public ExtractionResult Extract(SourceDocument source) { Received.Add(source); return response(source); }
}
public sealed record SourceBinaryDispatcherCase(string Id, SourceExtractionProfile Profile, string Requested, string Registered, bool Binary, bool Called, Exception? Fault = null)
{
    public bool Infrastructure => Fault is IOException;
    public ExtractionResult Native() => new([], [], StructuralParseStatus.Clean, new JsonObject { ["annotation_archive"] = new JsonObject { ["status"] = "fixture-concern", ["wire_proven"] = false } });
    public SourceDispatcherCase OriginalCase() => new(Id, Profile, Requested, null, [Registered], Called ? Registered : null, Native(),
        Called && Fault is null ? StructuralParseStatus.Clean : StructuralParseStatus.Unsupported,
        Fault is FormatException ? "source-extraction-failed" : Called ? null : "source-encoding-unsupported", [0xff], Fault, Infrastructure);
    public SourceExtractionCase MutationCase() => new(Id, "binary-source-dispatch", "base64:/w==", StructuralParseStatus.Clean, [OriginalCase().Expected().Metadata!], [], "Declared binary provider admission precedes text decoding, without filename guesses or source identity loss");
}
public static class SourceBinaryDispatcherContractCases
{
    public static IReadOnlyList<SourceBinaryDispatcherCase> All { get; } = Create();
    private static IReadOnlyList<SourceBinaryDispatcherCase> Create()
    {
        var result = new List<SourceBinaryDispatcherCase>();
        foreach (var profile in new[] { SourceExtractionProfile.RELEASE011, SourceExtractionProfile.CurrentCandidate })
        {
            var prefix = profile.Id + "-BIN";
            result.Add(new(prefix + "01", profile, "sdk-annotations", "sdk-annotations", true, true));
            result.Add(new(prefix + "02", profile, "binary-from-registration", "binary-from-registration", true, true));
            result.Add(new(prefix + "03", profile, "sdk-annotations", "sdk-annotations", false, false));
            result.Add(new(prefix + "04", profile, "unknown-kind", "sdk-annotations", true, false));
            result.Add(new(prefix + "05", profile, "sdk-annotations", "sdk-annotations", true, true, new FormatException("Original binary semantic failure")));
            result.Add(new(prefix + "06", profile, "sdk-annotations", "sdk-annotations", true, true, new IOException("Native archive infrastructure fault")));
        }
        return result;
    }
    public static IEnumerable<object[]> Cases => All.Select(test => new object[] { test.Id });
    public static IEnumerable<object[]> Mutations => All.Where(test => !test.Infrastructure).SelectMany(test => SourceMutationCases.For(test.MutationCase()));
    public static SourceBinaryDispatcherCase Get(string id) => All.Single(test => test.Id == id);
    public static void AssertContract(SourceBinaryDispatcherCase test, Func<ISourceExtractor, SourceExtractionProfile, ISourcesExtractionDispatcher> factory)
    {
        var calls = 0;
        ExtractionResult Response(SourceDocument source)
        {
            calls++; Assert.Equal(new byte[] { 0xff }, source.Bytes.ToArray()); Assert.Equal(test.OriginalCase().Document().Context, source.Context);
            if (test.Fault is not null) throw test.Fault;
            return test.Native();
        }
        ISourceExtractor provider = test.Binary ? new RecordingBinarySourceExtractor(test.Registered, Response) : new ScriptedSourceExtractor(test.Registered, Response);
        var dispatcher = factory(provider, test.Profile); var source = test.OriginalCase().Document(); var original = source.Bytes.ToArray();
        Assert.Equal(new[] { test.Registered }, dispatcher.SupportedKinds);
        if (test.Infrastructure) Assert.Same(test.Fault, Record.Exception(() => dispatcher.Extract(source, test.Requested)));
        else
        {
            var actual = dispatcher.Extract(source, test.Requested); var expected = test.OriginalCase().Expected();
            Assert.Empty(actual.Records); Assert.Equal(expected.StructuralStatus, actual.StructuralStatus); Assert.True(JsonNode.DeepEquals(expected.Metadata, actual.Metadata));
            Assert.Equal(expected.Issues.Select(issue => issue.Code), actual.Issues.Select(issue => issue.Code));
            foreach (var issue in actual.Issues) Assert.False(string.IsNullOrWhiteSpace(issue.Reason));
            if (test.Fault is not null) Assert.Equal(test.Fault.Message, Assert.Single(actual.Issues).Reason);
        }
        Assert.Equal(test.Called ? 1 : 0, calls); Assert.Equal(original, source.Bytes.ToArray());
    }
    public static void MutateInput(SourceDocument source)
    { Assert.True(MemoryMarshal.TryGetArray(source.Bytes, out var input)); input.Array![input.Offset] ^= 1; }
}
