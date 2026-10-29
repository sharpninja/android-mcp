using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record SourceDispatcherCase(string Id, SourceExtractionProfile Profile, string? Kind, string? Scope, string[] Registered, string? ProviderKind,
    ExtractionResult Native, StructuralParseStatus Status, string? IssueCode = null, byte[]? Input = null, Exception? ProviderFault = null, bool InfrastructureFault = false, string? Platform = "37.0")
{
    public const string HashX = "2d711642b726b04401627ca9fbac32f5c8530fb1903cc4db02258717921a4881";
    public const string HashFf = "a8100ae6aa1940d0b663bb31cd466142ebbdbd5187131b92d93818987832eb89";
    public SourceDocument Document() => new("fixture/A.aidl", (Input ?? Encoding.UTF8.GetBytes("x")).ToArray(), new("fixture-repository", "fixture-revision", "fixture-snapshot", Platform));
    public JsonObject Identity() => new()
    {
        ["requested_kind"] = Kind, ["requested_scope"] = Scope, ["relative_path"] = "fixture/A.aidl", ["sha256"] = Input is null ? HashX : HashFf,
        ["repository"] = "fixture-repository", ["revision"] = "fixture-revision", ["snapshot_id"] = "fixture-snapshot", ["platform_version"] = Platform, ["profile_id"] = Profile.Id
    };
    public ExtractionResult Expected()
    {
        JsonObject metadata;
        if (IssueCode is not null)
            metadata = new() { ["raw_source_encoding"] = Input is null ? "utf-8" : "base64", ["raw_source"] = Input is null ? "x" : "/w==", ["normalizer_complete"] = false, ["compiler_validation"] = false };
        else
        {
            var original = Native.Metadata is null ? new JsonObject() : (JsonObject)Native.Metadata.DeepClone();
            metadata = original.ContainsKey("source_identity") ? new JsonObject { ["format_metadata"] = original } : original;
        }
        metadata["source_identity"] = Identity();
        return new(IssueCode is null ? Native.Records.Select(x => (JsonObject)x.DeepClone()).ToArray() : [],
            IssueCode is null ? Native.Issues.ToArray() : [new(IssueCode, ProviderFault?.Message ?? "Independent unsupported-route fixture reason", null, null)], Status, metadata);
    }
}

public static class SourceDispatcherContractCases
{
    private static readonly string[] Registered = ["aidl", "signature", "java-source", "sdk-api-index", "manifest", "sdk-annotations", "hidl-source", "permission-reference", "native-abi", "format-from-registration"];
    private static ExtractionResult Native(bool concern = false, bool collision = false, bool partial = false) => new(
        concern ? [] : [new JsonObject { ["kind"] = "method", ["canonical"] = "fixture#call()", ["raw"] = "source-spelled-fixture", ["line"] = 1 }],
        partial ? [new("parser-fixture", "Original format diagnostic", 0, 1)] : [], partial ? StructuralParseStatus.Partial : StructuralParseStatus.Clean,
        collision ? new() { ["source_identity"] = new JsonObject { ["user"] = "retained" }, ["format_metadata"] = new JsonObject { ["also_user"] = true }, ["processor_identity"] = "old-processor-must-remain" }
            : new() { ["format_field"] = "unchanged", ["processor_identity"] = "old-processor-must-remain", ["wire_or_runtime_proven"] = false });
    public static IReadOnlyList<SourceDispatcherCase> All { get; } = Create();
    private static IReadOnlyList<SourceDispatcherCase> Create()
    {
        var cases = new List<SourceDispatcherCase>();
        foreach (var profile in new[] { SourceExtractionProfile.RELEASE011, SourceExtractionProfile.CurrentCandidate })
        {
            var prefix = profile == SourceExtractionProfile.RELEASE011 ? "R11" : "CUR";
            void Routed(string id, string kind, string provider, string? scope = null, bool concern = false, bool collision = false, bool partial = false, string? platform = "37.0") =>
                cases.Add(new(prefix + "-" + id, profile, kind, scope, Registered, provider, Native(concern, collision, partial), partial ? StructuralParseStatus.Partial : StructuralParseStatus.Clean, Platform: platform));
            void Retained(string id, string? kind, string? scope, string code = "source-format-unknown") =>
                cases.Add(new(prefix + "-" + id, profile, kind, scope, Registered, null, Native(), StructuralParseStatus.Unsupported, code));
            Routed("01", "sdk-signature", "signature"); Routed("02", "source-signature", "signature"); Routed("03", "aidl", "aidl");
            Routed("04", "java-source", "java-source"); Routed("05", "sdk-api-index", "sdk-api-index"); Routed("06", "manifest", "manifest", concern: true);
            Routed("07", "sdk-annotations", "sdk-annotations", concern: true);
            Routed("08", "evidence", "hidl-source", "aosp-pinned-hidl-source-evidence", platform: null);
            Routed("09", "evidence", "permission-reference", "official-sdk-permission-reference", concern: true);
            Retained("10", "sdk-history", null);
            Retained("11", "evidence", "aosp-native-binder-candidate-evidence"); Retained("12", "evidence", "aosp-native-binder-search-candidate-evidence"); Retained("13", "evidence", "aosp-interface-version-evidence");
            Retained("14", "evidence", "future-scope"); Retained("15", "not-a-known-kind", null, "source-kind-unsupported"); Retained("16", null, null, "source-kind-unsupported");
            Routed("17", "format-from-registration", "format-from-registration"); Routed("18", "signature", "signature", collision: true); Routed("19", "signature", "signature", partial: true);
            cases.Add(new(prefix + "-20", profile, "signature", null, Registered, null, Native(), StructuralParseStatus.Unsupported, "source-encoding-unsupported", [0xff]));
            cases.Add(new(prefix + "-21", profile, "source-signature", null, ["aidl"], null, Native(), StructuralParseStatus.Unsupported, "source-kind-unsupported"));
            cases.Add(new(prefix + "-22", profile, "signature", null, Registered, "signature", Native(), StructuralParseStatus.Unsupported, "source-extraction-failed", ProviderFault: new FormatException("Exact original semantic parse failure")));
            foreach (var (id, fault) in new (string, Exception)[] { ("23", new IOException("storage fault")), ("24", new OutOfMemoryException("allocation fault")), ("25", new DllNotFoundException("native asset fault")), ("26", new OperationCanceledException("cancelled")) })
                cases.Add(new(prefix + "-" + id, profile, "signature", null, Registered, "signature", Native(), StructuralParseStatus.Clean, ProviderFault: fault, InfrastructureFault: true));
            cases.Add(new(prefix + "-27", profile, "aidl", null, Registered, "aidl", Native() with { Metadata = null }, StructuralParseStatus.Clean));
            cases.Add(new(prefix + "-28", profile, "signature", null, Registered, "signature", Native(partial: true) with { StructuralStatus = StructuralParseStatus.Unsupported }, StructuralParseStatus.Unsupported));
        }
        return cases;
    }
    public static IEnumerable<object[]> Cases => All.Select(x => new object[] { x.Id });
    public static IEnumerable<object[]> ResultCases => All.Where(x => !x.InfrastructureFault).Select(x => new object[] { x.Id });
    public static IEnumerable<object[]> IdentityControls => All.Where(x => !x.InfrastructureFault).SelectMany(x => x.Identity().Select(property => new object[] { x.Id, property.Key }));
    public static SourceDispatcherCase Get(string id) => All.Single(x => x.Id == id);
    public static ExtractionResult Clone(ExtractionResult value) => value with { Records = value.Records.Select(x => (JsonObject)x.DeepClone()).ToArray(), Issues = value.Issues.ToArray(), Metadata = value.Metadata is null ? null : (JsonObject)value.Metadata.DeepClone() };

    public static void AssertContract(SourceDispatcherCase test, Func<IReadOnlyList<ISourceExtractor>, SourceExtractionProfile, ISourcesExtractionDispatcher> factory)
    {
        var originalNative = Clone(test.Native); var nativeForProvider = Clone(test.Native); var expected = test.Expected();
        var providers = test.Registered.Select(kind => new ScriptedSourceExtractor(kind, document =>
        {
            Assert.Equal("fixture/A.aidl", document.RelativePath); Assert.Equal(test.Document().Bytes.ToArray(), document.Bytes.ToArray()); Assert.Equal(test.Document().Context, document.Context);
            if (test.ProviderFault is not null) throw test.ProviderFault;
            return nativeForProvider;
        })).ToArray();
        var provider = factory(providers, test.Profile);
        Assert.Equal(test.Registered.Order(StringComparer.Ordinal), provider.SupportedKinds);
        var source = test.Document(); var original = source.Bytes.ToArray();
        if (test.InfrastructureFault) Assert.Same(test.ProviderFault, Record.Exception(() => provider.Extract(source, test.Kind, test.Scope)));
        else
        {
            var actual = provider.Extract(source, test.Kind, test.Scope);
            Assert.Equal(expected.StructuralStatus, actual.StructuralStatus);
            Assert.True(JsonNode.DeepEquals(new JsonArray(expected.Records.Cast<JsonNode>().ToArray()), new JsonArray(actual.Records.Select(x => (JsonNode)x.DeepClone()).ToArray())));
            Assert.True(JsonNode.DeepEquals(expected.Metadata, actual.Metadata));
            Assert.Equal(expected.Issues.Select(x => x.Code), actual.Issues.Select(x => x.Code));
            for (var index = 0; index < actual.Issues.Count; index++)
            {
                Assert.False(string.IsNullOrWhiteSpace(actual.Issues[index].Reason));
                if (test.IssueCode is null || test.ProviderFault is not null) Assert.Equal(expected.Issues[index].Reason, actual.Issues[index].Reason);
                Assert.Equal(expected.Issues[index].StartByte, actual.Issues[index].StartByte); Assert.Equal(expected.Issues[index].EndByte, actual.Issues[index].EndByte);
            }
        }
        foreach (var registered in providers) Assert.Equal(registered.Kind == test.ProviderKind ? 1 : 0, registered.Received.Count);
        Assert.Equal(original, source.Bytes.ToArray());
        Assert.True(JsonNode.DeepEquals(originalNative.Metadata, nativeForProvider.Metadata), "Dispatcher must preserve provider-owned metadata, including both reserved-field collision values");
        Assert.True(JsonNode.DeepEquals(new JsonArray(originalNative.Records.Cast<JsonNode>().ToArray()), new JsonArray(nativeForProvider.Records.Select(x => (JsonNode)x.DeepClone()).ToArray())));
    }

    public static SourceExtractionCase MetadataMutationCase(SourceDispatcherCase test) => new(test.Id, "source-dispatch-metadata", "x", test.Status,
        [test.Expected().Metadata!], [], "Dispatcher identity, raw retention and exact provider metadata contract");
    public static SourceExtractionCase RecordMutationCase(SourceDispatcherCase test) => new(test.Id, "source-dispatch-records", "x", test.Status,
        test.Expected().Records, [], "Dispatcher retains the exact native record payload");
    public static IEnumerable<object[]> MetadataMutations => All.Where(x => !x.InfrastructureFault).SelectMany(x => SourceMutationCases.For(MetadataMutationCase(x)));
    public static IEnumerable<object[]> RecordMutations => All.Where(x => !x.InfrastructureFault).SelectMany(x => SourceMutationCases.For(RecordMutationCase(x)));

    public static void AssertRegistrationFailure(string id, Func<IReadOnlyList<ISourceExtractor>, SourceExtractionProfile, ISourcesExtractionDispatcher> factory)
    {
        var names = id == "REG-duplicate" ? new[] { "signature", "signature" } : id == "REG-empty" ? new[] { " " } : new[] { "signature" };
        var providers = names.Select(name => (ISourceExtractor)new ScriptedSourceExtractor(name, _ => throw new IOException("Constructor cannot extract source"))).ToArray();
        if (id == "REG-null-profile") Assert.Throws<ArgumentNullException>(() => factory(providers, null!));
        else Assert.Throws<ArgumentException>(() => factory(providers, SourceExtractionProfile.RELEASE011));
    }
}
