using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record HashMainCase(string Id, string Variant, string? Error = null);
public sealed record HashMainObservation(JsonObject? Report, JsonObject? Summary, IReadOnlyDictionary<string, byte[]> Files,
    IReadOnlyList<string> Reads, IReadOnlyList<string> Parses, IReadOnlyList<string> Writes, string? Error,
    bool InputsRetained, bool ExistingRetained, bool TokensForwarded, IReadOnlyList<string> Profiles);
public interface IHashMainProbe { Task<HashMainObservation> RunAsync(HashMainFixture fixture); }
public sealed class HashMainFixture
{
    public const string InventoryPath = "outside-corpus-audit/source-inventory-batchinterface-metadata-v1-acquired002.json";
    public const string RawPath = "raw/source.hash";
    public const string Generation = "interface-hash-facts-v1";
    public const string AidlReason = "stable AIDL version hash", HidlReason = "HIDL ancestor hash-index candidate";
    public static readonly DateTimeOffset At = new(2026, 10, 29, 3, 0, 0, TimeSpan.FromHours(-5));
    public static readonly SourceExtractProducerIdentity Producer = new("fixture-native-source", new Dictionary<string, string> { ["native/assembly"] = new('e', 64) }, "fixture-runtime", "fixture-parser", "fixture-context");
    public required HashMainCase Case { get; init; }
    public Dictionary<string, byte[]> Inputs { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, byte[]> Existing { get; } = new(StringComparer.Ordinal);
    public List<JsonObject> Facts { get; } = [];
    public List<JsonObject> Reports { get; } = [];
    public JsonObject Counts { get; } = new();
    public List<string> ExpectedParses { get; } = [];
    public bool FactoryReached { get; private set; }
    public List<string> InitialReads { get; } = [InventoryPath];
    public PublishedInterfaceHashWorkflowRequest Request { get; private set; } = null!;
    public string Profile => Case.Variant == "release" ? "release-011" : "current-candidate";
    public string? Error => Case.Error;
    public bool ExistingDirectory => Case.Variant is "empty-dir" or "unrelated-dir";
    public bool PartialOutputs => Case.Variant is "write-facts" or "write-report" or "write-index" or "bad-receipt" or "change-after-write" or "cancel-write";
    public JsonObject ExpectedReport => new()
    {
        ["at"] = At.ToString("O"), ["counts"] = Counts.DeepClone(), ["files"] = new JsonArray(Reports.Select(row => (JsonNode)row.DeepClone()).ToArray()),
        ["code_sha256"] = new string('e', 64), ["native_producer"] = JsonSerializer.SerializeToNode(Producer), ["historical_python_identity"] = false,
        ["runtime_observations"] = false, ["metadata_cannot_grant_os_or_client_authority"] = true, ["complete"] = false
    };
    public byte[] ExpectedFactBytes => Facts.SelectMany(row => Serialize(row).Concat(new byte[] { 10 })).ToArray();
    public byte[] ExpectedReportBytes => Serialize(ExpectedReport);
    public static byte[] Serialize(JsonNode value) => new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(value), JsonSerializationProfile.LegacyAsciiIdentity);
    public static StoredArtifact Pin(string path, byte[] bytes, string media = "application/json") => new(path, ContentHash.Sha256(bytes), bytes.Length, media);
    private static JsonObject Row(byte[] raw, string format = "aidl") => new()
    {
        ["selection_reasons"] = new JsonArray(format == "aidl" ? AidlReason : HidlReason), ["local_path"] = RawPath,
        ["repository"] = "fixture-repository", ["revision"] = "pinned-revision", ["path"] = "p/aidl_api/foo/007/.hash",
        ["sha256"] = ContentHash.Sha256(raw), ["retrieved_at"] = "retained-source-time"
    };
    public static HashMainFixture Create(HashMainCase test)
    {
        var f = new HashMainFixture { Case = test }; var variant = test.Variant;
        var hidl = variant == "hidl"; var raw = Encoding.UTF8.GetBytes(hidl ? new string('a', 64) + " a.b@1.0::IFoo\n" + new string('b', 64) + " a.b@1.0::IFoo # compatibility\nopaque\n" : new string('a', 40) + "\n");
        var row = Row(raw, hidl ? "hidl" : "aidl"); var sources = new JsonArray(row); JsonNode? root = new JsonObject { ["sources"] = sources };
        if (variant is "empty" or "empty-object" or "empty-string") ((JsonObject)root)["sources"] = variant == "empty" ? new JsonArray() : variant == "empty-object" ? new JsonObject() : JsonValue.Create("");
        if (variant == "skip") { row.Clear(); row["selection_reasons"] = new JsonArray("unrelated"); }
        if (variant is "empty-reasons-array" or "empty-reasons-object" or "empty-reasons-string") row["selection_reasons"] = variant.EndsWith("array", StringComparison.Ordinal) ? new JsonArray() : variant.EndsWith("object", StringComparison.Ordinal) ? new JsonObject() : JsonValue.Create("");
        if (variant == "both") row["selection_reasons"] = new JsonArray(HidlReason, AidlReason);
        if (variant == "reason-map") row["selection_reasons"] = new JsonObject { [AidlReason] = new JsonArray(1, true) };
        if (variant == "reason-string") row["selection_reasons"] = "prefix " + AidlReason + " suffix";
        if (variant == "missing-sources") root = new JsonObject();
        if (variant == "root-null") root = null;
        if (variant is "sources-null" or "sources-number" or "sources-bool" or "sources-map" or "sources-string") ((JsonObject)root!)["sources"] = variant switch { "sources-null" => null, "sources-number" => JsonValue.Create(1), "sources-bool" => JsonValue.Create(true), "sources-map" => new JsonObject { ["row"] = 1 }, _ => JsonValue.Create("row") };
        if (variant == "missing-reasons") row.Remove("selection_reasons");
        if (variant is "reasons-null" or "reasons-number" or "reasons-bool") row["selection_reasons"] = variant == "reasons-null" ? null : variant == "reasons-number" ? JsonValue.Create(1) : JsonValue.Create(true);
        if (variant.StartsWith("identity-", StringComparison.Ordinal)) row["repository"] = variant switch { "identity-null" => null, "identity-list" => new JsonArray(1, true), "identity-object" => new JsonObject { ["raw"] = 7 }, "identity-number" => JsonValue.Create(7), _ => JsonValue.Create(true) };
        if (variant == "unknown-scope") row["path"] = "unmatched/.hash";
        if (variant == "current-scope") row["path"] = "aidl_api/foo/current/.hash";
        if (variant == "aidl-null-path") row["path"] = null;
        if (variant == "hidl-null-path") { row["selection_reasons"] = new JsonArray(HidlReason); row["path"] = null; hidl = true; }
        if (variant is "missing-identity" or "parser-before-identity") row.Remove("repository");
        if (variant == "unsafe-local") row["local_path"] = "../escape";
        if (variant is "empty-source" or "comment-only") { raw = Encoding.UTF8.GetBytes(variant == "empty-source" ? "" : "# comment\n"); row["sha256"] = ContentHash.Sha256(raw); }
        if (variant is "invalid-utf8" or "digest-before-utf8") { raw = [0xff]; row["sha256"] = variant == "invalid-utf8" ? ContentHash.Sha256(raw) : new string('0', 64); }
        if (variant == "digest") row["sha256"] = new string('0', 64);
        f.Inputs[RawPath] = raw;
        if (variant == "missing-raw") f.Inputs.Remove(RawPath);
        if (variant == "duplicate") { var duplicate = (JsonObject)row.DeepClone(); duplicate["revision"] = "second-context"; sources.Add(duplicate); }
        if (variant == "late-error") sources.Add(new JsonObject { ["selection_reasons"] = null });
        var selected = variant is not ("empty" or "empty-object" or "empty-string" or "skip" or "empty-reasons-array" or "empty-reasons-object" or "empty-reasons-string" or "missing-sources" or "root-null" or "sources-null" or "sources-number" or "sources-bool" or "sources-map" or "sources-string" or "missing-reasons" or "reasons-null" or "reasons-number" or "reasons-bool");
        var reachesRaw = selected && variant is not ("unsafe-local" or "unsafe-generation" or "inventory-pin" or "inventory-length" or "invalid-json" or "cancel-before");
        if (reachesRaw) f.InitialReads.Add(RawPath);
        if (variant == "duplicate") f.InitialReads.Add(RawPath);
        f.FactoryReached = reachesRaw && variant is not ("missing-raw" or "digest" or "invalid-utf8" or "digest-before-utf8" or "cancel-read");
        var parses = f.FactoryReached && variant != "runtime";
        if (parses) f.ExpectedParses.Add(hidl ? "hidl" : "aidl");
        if (variant == "duplicate") f.ExpectedParses.Add("aidl");
        if (parses && test.Error is null || f.PartialOutputs || variant is "empty-dir" or "unrelated-dir" or "occupied" or "change-before-generation")
        {
            foreach (var logical in sources.OfType<JsonObject>().Where(value => value.ContainsKey("repository")))
            {
                var format = hidl ? "hidl" : "aidl"; var identity = new JsonObject();
                foreach (var key in new[] { "repository", "revision", "path", "sha256", "retrieved_at" }) identity[key] = logical[key]?.DeepClone();
                var scope = hidl ? new JsonObject { ["path_scope"] = "HIDL ancestor hash index; individual identifiers retain package versions" } : variant == "unknown-scope" ? new JsonObject { ["path_scope"] = "unknown" } : new JsonObject { ["interface_module_name_from_path"] = "foo", ["frozen_dump_version_from_path"] = variant == "current-scope" ? "current" : "007" };
                var facts = new JsonArray(); var unknown = new JsonArray();
                if (variant is not ("empty-source" or "comment-only"))
                {
                    if (hidl && variant != "hidl-null-path")
                    {
                        foreach (var (letter, line, suffix) in new[] { ('a', 1, ""), ('b', 2, " # compatibility") }) facts.Add(new JsonObject { ["line"] = line, ["literal"] = new string(letter, 64), ["declared_identifier"] = "a.b@1.0::IFoo", ["format"] = "published-hidl-hash-hex64-and-identifier", ["raw"] = new string(letter, 64) + " a.b@1.0::IFoo" + suffix });
                        unknown.Add(new JsonObject { ["line"] = 3, ["raw"] = "opaque" });
                    }
                    else if (hidl) unknown.Add(new JsonObject { ["line"] = 1, ["raw"] = new string('a', 40) });
                    else facts.Add(new JsonObject { ["line"] = 1, ["literal"] = new string('a', 40), ["format"] = "published-aidl-hash-hex40", ["raw"] = new string('a', 40) });
                }
                foreach (var fact in facts.OfType<JsonObject>()) { var full = (JsonObject)fact.DeepClone(); full["source"] = identity.DeepClone(); full["scope"] = scope.DeepClone(); f.Facts.Add(full); f.Counts[format + ":hash-facts"] = (f.Counts[format + ":hash-facts"]?.GetValue<int>() ?? 0) + 1; }
                f.Reports.Add(new JsonObject { ["source"] = identity, ["scope"] = scope, ["format"] = format, ["facts"] = facts.Count, ["unknown_lines"] = unknown, ["compiled_runtime_hash_proven"] = false });
                f.Counts[format + ":files"] = (f.Counts[format + ":files"]?.GetValue<int>() ?? 0) + 1; f.Counts[format + ":unknown-lines"] = (f.Counts[format + ":unknown-lines"]?.GetValue<int>() ?? 0) + unknown.Count;
            }
        }
        f.Inputs[InventoryPath] = new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(root));
        var inventoryPin = Pin(InventoryPath, f.Inputs[InventoryPath]);
        if (variant == "inventory-pin") inventoryPin = inventoryPin with { Sha256 = new string('0', 64) };
        if (variant == "inventory-length") inventoryPin = inventoryPin with { Length = inventoryPin.Length + 1 };
        if (variant == "invalid-json") f.Inputs[InventoryPath] = Encoding.UTF8.GetBytes("{");
        if (variant == "invalid-json") inventoryPin = Pin(InventoryPath, f.Inputs[InventoryPath]);
        f.Request = new(inventoryPin, variant == "unsafe-generation" ? "../escape" : Generation, SourceExtractionProfile.Resolve(f.Profile), Producer);
        if (variant == "producer") f.Request = f.Request with { Producer = Producer with { ProducerFiles = new Dictionary<string, string>() } };
        if (variant == "occupied") f.Existing[Generation + "/facts.jsonl"] = Encoding.UTF8.GetBytes("original");
        if (variant == "unrelated-dir") f.Existing[Generation + "/unrelated.txt"] = Encoding.UTF8.GetBytes("original");
        return f;
    }
    public IReadOnlyList<string> ExpectedReads
    {
        get
        {
            if (Case.Variant is "unsafe-generation" or "cancel-before") return [];
            if (Case.Variant is "inventory-pin" or "inventory-length" or "invalid-json") return [InventoryPath];
            var result = InitialReads.ToList();
            if (Error is null || PartialOutputs || Case.Variant is "empty-dir" or "unrelated-dir" or "occupied" or "change-before-generation")
            {
                var physical = new[] { InventoryPath }.Concat(ExpectedParses.Count > 0 ? new[] { RawPath } : Array.Empty<string>());
                result.AddRange(physical);
                if (Error is null || Case.Variant is "change-after-write" or "write-index") result.AddRange(physical);
            }
            return result;
        }
    }
    public IReadOnlyList<string> ExpectedWrites => Case.Variant switch
    {
        "write-facts" or "cancel-write" => [Generation + "/facts.jsonl"],
        "write-report" or "bad-receipt" or "change-after-write" => [Generation + "/facts.jsonl", Generation + "/report.json"],
        "write-index" => [Generation + "/facts.jsonl", Generation + "/report.json", Generation + "/index.json"],
        _ => Error is null ? [Generation + "/facts.jsonl", Generation + "/report.json", Generation + "/index.json"] : []
    };
}
public static class PublishedHashMainContractCases
{
    public static IReadOnlyList<HashMainCase> All { get; } = Create();
    private static IReadOnlyList<HashMainCase> Create()
    {
        var rows = new List<HashMainCase>();
        foreach (var variant in new[] { "aidl", "hidl", "both", "skip", "empty", "empty-object", "empty-string", "empty-reasons-array", "empty-reasons-object", "empty-reasons-string", "reason-map", "reason-string", "identity-null", "identity-list", "identity-object", "identity-number", "identity-bool", "unknown-scope", "current-scope", "hidl-null-path", "empty-source", "comment-only", "duplicate", "release" }) rows.Add(new("HM-" + rows.Count, variant));
        foreach (var variant in new[] { "missing-sources", "root-null", "sources-null", "sources-number", "sources-bool", "sources-map", "sources-string", "missing-reasons", "reasons-null", "reasons-number", "reasons-bool", "missing-identity", "aidl-null-path", "late-error", "unsafe-local", "unsafe-generation" }) rows.Add(new("HM-" + rows.Count, variant, "ARGUMENT"));
        foreach (var (variant, error) in new[] { ("missing-raw", "MISSING"), ("digest", "INTERFACE_HASH_DIGEST"), ("digest-before-utf8", "INTERFACE_HASH_DIGEST"), ("invalid-utf8", "UTF8"), ("parser-before-identity", "IO"), ("runtime", "RUNTIME"), ("producer", "ARGUMENT"), ("empty-dir", "INTERFACE_HASH_OUTPUT_EXISTS"), ("unrelated-dir", "INTERFACE_HASH_OUTPUT_EXISTS"), ("occupied", "OUTPUT_EXISTS"), ("change-before-generation", "INPUT_PIN_MISMATCH"), ("change-after-write", "INPUT_CHANGED"), ("write-facts", "WRITE_FAILED"), ("write-report", "WRITE_FAILED"), ("write-index", "WRITE_FAILED"), ("bad-receipt", "STORED_RECEIPT_MISMATCH"), ("inventory-pin", "ARTIFACT_PIN_HASH"), ("inventory-length", "ARTIFACT_PIN_LENGTH"), ("invalid-json", "INVALID_JSON"), ("cancel-before", "CANCEL"), ("cancel-read", "CANCEL"), ("cancel-write", "CANCEL") }) rows.Add(new("HM-" + rows.Count, variant, error));
        return rows;
    }
    public static IEnumerable<object[]> Cases => All.Select(test => new object[] { test.Id });
    public static HashMainCase Get(string id) => All.Single(test => test.Id == id);
    public static async Task AssertCase(HashMainFixture f, IHashMainProbe probe)
    {
        var actual = await probe.RunAsync(f);
        Assert.Equal(f.Error, actual.Error); Assert.True(actual.InputsRetained); Assert.True(actual.ExistingRetained); Assert.True(actual.TokensForwarded);
        Assert.Equal(f.ExpectedReads, actual.Reads); Assert.Equal(f.ExpectedParses, actual.Parses); Assert.Equal(f.ExpectedWrites, actual.Writes);
        Assert.Equal(f.FactoryReached ? new[] { f.Profile } : Array.Empty<string>(), actual.Profiles);
        foreach (var pair in f.Existing) Assert.Equal(pair.Value, actual.Files[pair.Key]);
        if (f.Error is null)
        {
            Assert.NotNull(actual.Report); Assert.True(JsonNode.DeepEquals(f.ExpectedReport, actual.Report));
            Assert.Equal(f.ExpectedFactBytes, actual.Files[HashMainFixture.Generation + "/facts.jsonl"]);
            Assert.Equal(f.ExpectedReportBytes, actual.Files[HashMainFixture.Generation + "/report.json"]);
            Assert.Equal(3, actual.Files.Count); Assert.NotNull(actual.Summary); Assert.False(actual.Summary.ContainsKey("files"));
            var expectedSummary = (JsonObject)f.ExpectedReport.DeepClone(); expectedSummary.Remove("files");
            foreach (var key in expectedSummary.Select(pair => pair.Key)) Assert.True(JsonNode.DeepEquals(expectedSummary[key], actual.Summary[key]));
            var native = actual.Summary["native_generation"]!.AsObject();
            foreach (var (key, file) in new[] { ("Facts", "facts.jsonl"), ("Report", "report.json"), ("Index", "index.json") })
            { var pin = native[key]!.Deserialize<StoredArtifact>()!; var path = HashMainFixture.Generation + "/" + file; Assert.Equal(path, pin.RelativePath); Assert.Equal(actual.Files[path].Length, pin.Length); Assert.Equal(ContentHash.Sha256(actual.Files[path]), pin.Sha256); }
            var commit = JsonNode.Parse(actual.Files[HashMainFixture.Generation + "/index.json"])!.AsObject();
            Assert.Equal("androidmcp-native-interface-hash-generation-v1", commit["Schema"]!.GetValue<string>()); Assert.Equal(HashMainFixture.Generation, commit["GenerationId"]!.GetValue<string>());
            Assert.Equal(f.Profile, commit["ProfileId"]!.GetValue<string>()); Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(HashMainFixture.Producer), commit["Producer"]));
            Assert.Equal(2, commit["Outputs"]!.AsArray().Count); Assert.Equal(f.ExpectedParses.Count > 0 ? 2 : 1, commit["Inputs"]!.AsArray().Count);
        }
        else
        {
            Assert.Null(actual.Report); Assert.Null(actual.Summary); Assert.False(actual.Files.ContainsKey(HashMainFixture.Generation + "/index.json"));
            if (f.PartialOutputs)
            {
                Assert.True(actual.Files.ContainsKey(HashMainFixture.Generation + "/facts.jsonl"));
                if (f.Case.Variant is not ("write-facts" or "cancel-write")) Assert.Equal(f.ExpectedFactBytes, actual.Files[HashMainFixture.Generation + "/facts.jsonl"]);
                if (f.Case.Variant is "write-index" or "change-after-write" or "bad-receipt") Assert.Equal(f.ExpectedReportBytes, actual.Files[HashMainFixture.Generation + "/report.json"]);
            }
            else Assert.Equal(f.Existing.Count, actual.Files.Count);
        }
    }
}
