using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record AidlProfileCase(string Id, SourceExtractionCase Source, SourceExtractionProfile Profile, string? PinnedSha256 = null);
public static class AidlProfileContractCases
{
    public static SourceExtractionCase ForProfile(SourceExtractionCase source, SourceExtractionProfile profile)
    {
        if (source.Kind != "aidl") return source;
        var records = source.ExpectedRecords.Select(row => (JsonObject)row.DeepClone()).ToArray();
        var declarations = records.Where(row => row["extras"] is JsonObject extras && extras.ContainsKey("aidl_kind"))
            .ToDictionary(row => row["canonical"]!.GetValue<string>(), row => row["extras"]!["oneway"]!.GetValue<bool>(), StringComparer.Ordinal);
        foreach (var row in records)
        {
            if (row["extras"] is not JsonObject extras || extras.ContainsKey("aidl_kind") || row["owner"] is not JsonValue owner || !owner.TryGetValue<string>(out var descriptor) || !declarations.TryGetValue(descriptor, out var inherited)) continue;
            if (profile == SourceExtractionProfile.RELEASE011) extras.Remove("interface_oneway"); else extras["interface_oneway"] = inherited;
        }
        return source with { ExpectedRecords = records };
    }
    private static SourceExtractionCase Minimal(string id, string text, bool declarationOneway, bool methodOneway)
    {
        var original = SourceExtractionContractCases.Get("AIDL-10");
        var declaration = (JsonObject)original.ExpectedRecords[0].DeepClone(); var method = (JsonObject)original.ExpectedRecords[2].DeepClone();
        declaration["raw"] = declarationOneway ? "oneway interface I" : "interface I"; declaration["extras"]!["oneway"] = declarationOneway;
        method["canonical"] = "a.I#ping()"; method["name"] = "ping"; method["raw"] = methodOneway ? "oneway void ping()" : "void ping()";
        method["extras"]!["oneway"] = methodOneway; method["extras"]!["interface_oneway"] = declarationOneway; method["extras"]!["declaration_ordinal"] = 0;
        return original with { Id = id, Text = text, ExpectedRecords = [declaration, method], Evidence = "Pinned current35a2/release5194 build_catalog member loop and independently retained parser/member golden fields" };
    }
    public static IReadOnlyList<AidlProfileCase> All { get; } = Create();
    private static IReadOnlyList<AidlProfileCase> Create()
    {
        var inputs = new (SourceExtractionCase Source, string? Hash)[]
        {
            (Minimal("AIDL-PROFILE-INTERFACE", "package a; oneway interface I { void ping(); }", true, false), "d2f1352586f57ff0f412b8b6ef87c2b65691199d8bdc6603bcd4e02f34115bd6"),
            (Minimal("AIDL-PROFILE-METHOD", "package a; interface I { oneway void ping(); }", false, true), "96e0f8b1f7fc73b9810c795d75b592c987f01a97c3977fd06f12a0e8891f6e72"),
            (Minimal("AIDL-PROFILE-ORDINARY", "package a; interface I { void ping(); }", false, false), "2e9b854e0eb0d67fc15d4bcdda1c7897cca9cd11ce4dbdbe9d6982a27024c2c4")
        }.Concat(SourceExtractionContractCases.All.Where(test => test.Kind == "aidl").Select(test => (Source: test, Hash: (string?)null))).ToArray();
        return new[] { SourceExtractionProfile.CurrentCandidate, SourceExtractionProfile.RELEASE011 }
            .SelectMany(profile => inputs.Select(input => new AidlProfileCase(profile.Id + ":" + input.Source.Id, ForProfile(input.Source, profile), profile, input.Hash))).ToArray();
    }
    public static IEnumerable<object[]> Cases => All.Select(test => new object[] { test.Id });
    public static IEnumerable<object[]> CurrentCases => All.Where(test => test.Profile == SourceExtractionProfile.CurrentCandidate).Select(test => new object[] { test.Id });
    public static AidlProfileCase Get(string id) => All.Single(test => test.Id == id);
    public static JsonObject Identity(AidlProfileCase test)
    {
        var source = test.Source.Document();
        return new JsonObject { ["requested_kind"] = "aidl", ["requested_scope"] = null, ["relative_path"] = source.RelativePath,
            ["sha256"] = test.PinnedSha256 ?? ContentHash.Sha256(source.Bytes.Span), ["repository"] = source.Context.Repository,
            ["revision"] = source.Context.Revision, ["snapshot_id"] = source.Context.SnapshotId, ["platform_version"] = source.Context.PlatformVersion, ["profile_id"] = test.Profile.Id };
    }
    public static ExtractionResult Correct(AidlProfileCase test, bool dispatched)
    {
        var result = test.Source.CorrectResult();
        var metadata = new JsonObject { ["aidl_schema"] = new JsonObject { ["raw_schema"] = test.Source.Text } };
        if (dispatched) metadata["source_identity"] = Identity(test);
        return result with { Metadata = metadata };
    }
    public static void AssertContract(AidlProfileCase test, Func<SourceDocument, SourceExtractionProfile, ExtractionResult> run, bool dispatched)
    {
        ExtractionResult? result = null; var calls = new List<(SourceDocument Source, SourceExtractionProfile Profile)>();
        var adapter = new ScriptedSourceExtractor("aidl", source => { calls.Add((source, test.Profile)); return result = run(source, test.Profile); });
        SourceExtractionContractCases.AssertContract(test.Source, adapter);
        var call = Assert.Single(calls); Assert.Equal(test.Profile, call.Profile); Assert.Equal(test.Source.Document().Context, call.Source.Context);
        Assert.NotNull(result); Assert.NotNull(result.Metadata); Assert.Equal(test.Source.Text, result.Metadata["aidl_schema"]?["raw_schema"]?.GetValue<string>());
        if (dispatched) Assert.True(JsonNode.DeepEquals(Identity(test), result.Metadata["source_identity"]), "Exact selected profile/source fingerprint differs");
        foreach (var (expected, index) in test.Source.ExpectedRecords.Select((row, index) => (row, index)))
        {
            if (expected["extras"] is not JsonObject extras || extras.ContainsKey("aidl_kind")) continue;
            var isMember = expected["owner"] is JsonValue owner && owner.TryGetValue<string>(out var descriptor) && test.Source.ExpectedRecords.Any(row => row["extras"]?["aidl_kind"] is not null && row["canonical"]?.GetValue<string>() == descriptor);
            if (!isMember) continue;
            var actual = result.Records[index]["extras"]!.AsObject();
            Assert.Equal(test.Profile != SourceExtractionProfile.RELEASE011, actual.ContainsKey("interface_oneway"));
            if (test.Profile != SourceExtractionProfile.RELEASE011) Assert.True(JsonNode.DeepEquals(extras["interface_oneway"], actual["interface_oneway"]), "Current inherited flag must be exact boolean, including false");
        }
    }
    public static void AssertOnlyProfileDelta(AidlProfileCase current, Func<SourceDocument, SourceExtractionProfile, ExtractionResult> run)
    {
        var release = All.Single(test => test.Source.Id == current.Source.Id && test.Profile == SourceExtractionProfile.RELEASE011);
        var currentResult = run(current.Source.Document(), current.Profile); var releaseResult = run(release.Source.Document(), release.Profile);
        var currentRows = currentResult.Records.Select(row => (JsonObject)row.DeepClone()).ToArray();
        foreach (var row in currentRows) if (row["extras"] is JsonObject extras) extras.Remove("interface_oneway");
        Assert.True(JsonNode.DeepEquals(new JsonArray(currentRows.Cast<JsonNode>().ToArray()), new JsonArray(releaseResult.Records.Select(row => row.DeepClone()).ToArray())), "Profile changed another retained record field");
        Assert.Equal(currentResult.StructuralStatus, releaseResult.StructuralStatus); Assert.Equal(currentResult.Issues, releaseResult.Issues);
        var metadata = (JsonObject?)currentResult.Metadata?.DeepClone();
        if (metadata?["source_identity"] is JsonObject identity) identity["profile_id"] = SourceExtractionProfile.RELEASE011.Id;
        Assert.True(JsonNode.DeepEquals(metadata, releaseResult.Metadata), "Profile changed other format metadata or source identity fields");
    }
}
