using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class RecordingSdkAnnotationArchive(bool partial) : ISdkAnnotationArchiveSourceFacts
{
    public JsonObject Output { get; } = (JsonObject)SdkAnnotationArchiveContractCases.Get(partial ? "ANN-Z04" : "ANN-Z01").Expected.DeepClone();
    public List<byte[]> Received { get; } = [];
    public JsonObject Parse(ReadOnlyMemory<byte> archive) { Received.Add(archive.ToArray()); return Output; }
}
public static class SourceRuntimeArchiveContractCases
{
    public static IEnumerable<object[]> Cases => new[] { "current-candidate", "release-011" }.SelectMany(profile => new[] { false, true }.SelectMany(java => new[] { false, true }.Select(partial => new object[] { profile, java, partial })));
    public static string[] ManagedKinds => SourcesRuntimeContractCases.ManagedKinds.Concat(["sdk-annotations"]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    public static void AssertContract(string profile, bool java, bool partial,
        Func<RecordingSourceJavaLoader, RecordingSdkAnnotationArchive, ISourcesRuntimeFactory> factory)
    {
        var loader = new RecordingSourceJavaLoader(); var archive = new RecordingSdkAnnotationArchive(partial); var original = archive.Output.DeepClone();
        var runtime = factory(loader, archive).Create(SourcesRuntimeContractCases.Profile(profile), java ? SourcesRuntimeContractCases.Admission : null);
        try
        {
            Assert.Equal(ManagedKinds.Concat(java ? ["java-source"] : Array.Empty<string>()).Order(StringComparer.Ordinal), runtime.Dispatcher.SupportedKinds);
            var cache = Assert.IsAssignableFrom<ISourcesCacheRuntime>(runtime);
            Assert.True(new HashSet<string>(java ? ["sdk-api-index", "java-source"] : ["sdk-api-index"], StringComparer.Ordinal).SetEquals(cache.CacheProducerKinds));
            var bytes = new byte[] { 0xff }; var document = new SourceDocument("fixture/no-extension", bytes, new("repo", "revision", "snapshot", null));
            var result = runtime.Dispatcher.Extract(document, "sdk-annotations");
            Assert.Empty(result.Records); Assert.Equal(partial ? StructuralParseStatus.Partial : StructuralParseStatus.Clean, result.StructuralStatus);
            Assert.True(JsonNode.DeepEquals(original, result.Metadata?["annotation_archive"]));
            Assert.Equal(profile, result.Metadata?["source_identity"]?["profile_id"]?.GetValue<string>());
            Assert.Equal(SourceDispatcherCase.HashFf, result.Metadata?["source_identity"]?["sha256"]?.GetValue<string>());
            Assert.Equal(archive.Output["issues"]!.AsArray().Select(issue => issue!["code"]!.GetValue<string>()), result.Issues.Select(issue => issue.Code));
            Assert.Equal(archive.Output["issues"]!.AsArray().Select(issue => issue!["reason"]!.GetValue<string>()), result.Issues.Select(issue => issue.Reason));
            Assert.Equal(new byte[] { 0xff }, bytes); Assert.Equal(new byte[] { 0xff }, Assert.Single(archive.Received));
            Assert.Equal(java ? new[] { SourcesRuntimeContractCases.Admission } : Array.Empty<SourceJavaAdmission>(), loader.Received); Assert.Equal(0, loader.DisposeCount);
            result.Metadata!["annotation_archive"]!["status"] = "caller-mutation";
            Assert.True(JsonNode.DeepEquals(original, archive.Output), "Returned archive concern must not mutate injected provider data");
        }
        finally { runtime.Dispose(); }
        runtime.Dispose(); Assert.Equal(java ? 1 : 0, loader.DisposeCount);
        Assert.Throws<ObjectDisposedException>(() => ((ISourcesCacheRuntime)runtime).CacheProducerKinds);
    }
}
