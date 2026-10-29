using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using AndroidMcp.BuildTool.Sources.Java;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record SourceRuntimeRig(ISourcesRuntimeFactory Factory, RecordingSourceJavaLoader Java, RecordingPermissionReference Permission);
public static class SourcesRuntimeContractCases
{
    internal static readonly string[] ManagedKinds = ["aidl", "hidl-source", "manifest", "permission-reference", "sdk-annotations", "sdk-api-index", "signature"];
    internal static readonly SourceJavaAdmission Admission = new("fixtures/selected-native-source-lock.json");
    public static IEnumerable<object[]> Cases => new[] { "release-011", "current-candidate" }.SelectMany(profile => new[] { false, true }.Select(java => new object[] { profile, java }));
    public static SourceExtractionProfile Profile(string id) => id == "release-011" ? SourceExtractionProfile.RELEASE011 : SourceExtractionProfile.CurrentCandidate;
    internal static IReadOnlyList<(SourceExtractionCase Test, string Requested, string? Scope)> Routes =>
    [
        (SourceExtractionContractCases.All.First(x => x.Kind == "aidl"), "aidl", null),
        (SourceExtractionContractCases.All.First(x => x.Kind == "signature"), "sdk-signature", null),
        (SourceExtractionContractCases.All.First(x => x.Kind == "signature"), "source-signature", null),
        (HidlContractCases.All[0].Source, "evidence", "aosp-pinned-hidl-source-evidence"),
        (SdkXmlContractCases.All[0], "sdk-api-index", null),
        (ManifestContractCases.All[0].Source, "manifest", null)
    ];

    public static void AssertComposition(string profile, bool java, SourceRuntimeRig rig)
    {
        using var runtime = rig.Factory.Create(Profile(profile), java ? Admission : null);
        var expectedKinds = ManagedKinds.Concat(java ? ["java-source"] : Array.Empty<string>()).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(expectedKinds, runtime.Dispatcher.SupportedKinds);
        Assert.Equal(java ? new[] { Admission } : Array.Empty<SourceJavaAdmission>(), rig.Java.Received);
        Assert.Equal(0, rig.Java.DisposeCount);
        var concerns = runtime.Concerns;
        Assert.Same(concerns, runtime.Concerns);
        Assert.Same(concerns.Callbacks, concerns.Taxonomy);
        Assert.Same(concerns.Callbacks, concerns.Applicability);
        Assert.Same(concerns.AnnotationTargets, concerns.TypeHeaders);
        Assert.Same(rig.Permission, concerns.PermissionReference);
        Assert.NotNull(concerns.PermissionExpressions); Assert.NotNull(concerns.Soong);
        Assert.NotNull(concerns.Apex); Assert.NotNull(concerns.NativeAbi);
        Assert.NotNull(concerns.PublishedHashes); Assert.NotNull(concerns.Html);
        foreach (var route in Routes)
        {
            var provider = new ScriptedSourceExtractor(route.Test.Kind, document => runtime.Dispatcher.Extract(document, route.Requested, route.Scope));
            SourceExtractionContractCases.AssertContract(AidlProfileContractCases.ForProfile(route.Test, Profile(profile)), provider);
        }
        var reference = PermissionReferenceContractCases.All[0];
        var bytes = Encoding.UTF8.GetBytes(reference.Html);
        var original = bytes.ToArray();
        var document = new SourceDocument("fixtures/reference.html", bytes, new("repo", "rev", "snapshot", "37.2"));
        var evidence = runtime.Dispatcher.Extract(document, "evidence", "official-sdk-permission-reference");
        Assert.Empty(evidence.Records);
        Assert.Empty(evidence.Issues);
        Assert.Equal(StructuralParseStatus.Clean, evidence.StructuralStatus);
        Assert.True(JsonNode.DeepEquals(reference.Expected, evidence.Metadata?["permission_reference"]));
        Assert.Equal(profile, evidence.Metadata?["source_identity"]?["profile_id"]?.GetValue<string>());
        Assert.Single(rig.Permission.Received);
        Assert.Equal(original, rig.Permission.Received[0]); Assert.Equal(original, bytes);
        evidence.Metadata!["permission_reference"]!["known_values"] = -99;
        Assert.True(JsonNode.DeepEquals(reference.Expected, rig.Permission.Output));
        if (java)
        {
            var result = runtime.Dispatcher.Extract(document, "java-source");
            Assert.Single(result.Records);
            Assert.Equal("admitted-java-provider", result.Records[0]["canonical"]?.GetValue<string>());
            Assert.Equal(0, rig.Java.DisposeCount);
        }
        foreach (var scope in new[] { "aosp-native-binder-candidate-evidence", "aosp-native-binder-search-candidate-evidence", "aosp-interface-version-evidence" })
        {
            var raw = runtime.Dispatcher.Extract(document, "evidence", scope);
            Assert.Empty(raw.Records); Assert.Equal(StructuralParseStatus.Unsupported, raw.StructuralStatus);
            Assert.Equal(reference.Html, raw.Metadata?["raw_source"]?.GetValue<string>());
        }
    }

    public static void AssertLifecycle(bool java, SourceRuntimeRig rig)
    {
        var runtime = rig.Factory.Create(SourceExtractionProfile.RELEASE011, java ? Admission : null);
        Assert.NotNull(runtime.Dispatcher); Assert.NotNull(runtime.Concerns);
        runtime.Dispose(); runtime.Dispose();
        Assert.Equal(java ? 1 : 0, rig.Java.DisposeCount);
        Assert.Throws<ObjectDisposedException>(() => runtime.Dispatcher);
        Assert.Throws<ObjectDisposedException>(() => runtime.Concerns);
    }
    public static void AssertNullProfile(SourceRuntimeRig rig)
    {
        Assert.Throws<ArgumentNullException>(() => rig.Factory.Create(null!, Admission));
        Assert.Empty(rig.Java.Received); Assert.Equal(0, rig.Java.DisposeCount);
    }
    public static void AssertAdmissionFailure(SourceRuntimeRig rig, Exception failure)
    {
        rig.Java.Failure = failure;
        var actual = Record.Exception(() => rig.Factory.Create(SourceExtractionProfile.RELEASE011, Admission));
        Assert.Same(failure, actual); Assert.Single(rig.Java.Received); Assert.Equal(0, rig.Java.DisposeCount);
    }
    public static void AssertWrongJavaKind(SourceRuntimeRig rig, string kind)
    {
        rig.Java.Kind = kind;
        Assert.Throws<ArgumentException>(() => rig.Factory.Create(SourceExtractionProfile.RELEASE011, Admission));
        Assert.Single(rig.Java.Received); Assert.Equal(1, rig.Java.DisposeCount);
    }
}

public sealed class RecordingPermissionReference : IPermissionReferenceSourceFacts
{
    public readonly List<byte[]> Received = [];
    public JsonObject Output { get; } = (JsonObject)PermissionReferenceContractCases.All[0].Expected.DeepClone();
    public JsonObject Parse(ReadOnlyMemory<byte> html) { Received.Add(html.ToArray()); return Output; }
}
public sealed class RecordingSourceJavaLoader : IJavaSourceRuntimeLoader
{
    public readonly List<SourceJavaAdmission> Received = [];
    public int DisposeCount { get; private set; }
    public string Kind { get; set; } = "java-source";
    public Exception? Failure { get; set; }
    public IJavaSourceRuntime Open(SourceJavaAdmission admission)
    {
        Received.Add(admission);
        if (Failure is not null) throw Failure;
        return new Resource(this);
    }
    private sealed class Resource(RecordingSourceJavaLoader owner) : IJavaSourceRuntime
    {
        public ISourceExtractor Extractor { get; } = new ScriptedSourceExtractor(owner.Kind, _ =>
            new ExtractionResult([new JsonObject { ["canonical"] = "admitted-java-provider" }], [], StructuralParseStatus.Clean));
        public void Dispose() { owner.DisposeCount++; }
    }
}
