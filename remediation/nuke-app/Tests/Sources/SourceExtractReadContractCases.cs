using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record SourceExtractReadCase(string Id, string? Error = null, bool Raw = false, bool Diagnostic = false,
    StructuralParseStatus Status = StructuralParseStatus.Clean, bool Repeat = false, bool Noncache = false, bool NoIndex = false, bool DirectResolve = false);
public sealed record SourceExtractReadObservation(IReadOnlyList<SourceExtractRow> Rows, Exception? Error,
    IReadOnlyList<string> Events, int RuntimeCreates, int RuntimeDisposals, int RawReads, int Dispatches, int CacheLookups, int Writes, bool OriginalsRetained, bool TokenForwarded);
public interface ISourceExtractReadProbe
{ Task<SourceExtractReadObservation> ReadAsync(SourceExtractReadFixture fixture, CancellationToken cancellationToken); }

// Literal raw bytes, contextual facts and response records are independent of
// the future reader. CanonicalJson only formats already-authored fixture DATA.
public sealed class SourceExtractReadFixture
{
    public const string RawSha = "2d711642b726b04401627ca9fbac32f5c8530fb1903cc4db02258717921a4881";
    public const string RecipeSha = "1111111111111111111111111111111111111111111111111111111111111111";
    public required SourceExtractReadCase Case { get; init; }
    public SourceExtractGenerationRequest Request { get; private set; } = null!;
    public RehydrationSourceRow Row { get; private set; } = null!;
    public SourceExtractIndex Index { get; private set; } = null!;
    public SourceExtractGenerationManifest Manifest { get; private set; } = null!;
    public Dictionary<string, Artifact> Files { get; } = new(StringComparer.Ordinal);
    public SourceContext ExpectedContext { get; private set; } = null!;
    public SourceCacheLookup Lookup { get; private set; } = null!;
    public byte[] RecordBytes { get; } = Encoding.UTF8.GetBytes("[{\"canonical\":\"fixture.Cache\",\"kind\":\"class\"}]");
    public JsonObject Metadata { get; } = new() { ["producer_fact"] = "unchanged", ["wire_status"] = "unknown" };
    public Exception InfrastructureFault { get; } = new IOException("Recorded sealed lookup infrastructure fault");
    public int WantedReads => Case.Raw ? 1 : 0;
    public int WantedLookup => Case.Error is null && !Case.Raw ? 1 : Case.Id == "lookup-infrastructure" ? 1 : 0;
    public int WantedCreates => Case.Error is null || Case.Raw || Case.Id is "lookup-infrastructure" or "cache-capability" ? 1 : 0;

    public SourceExtractRow ExpectedRow() => new(Row.RowIndex, 1, ExpectedContext, RecordBytes, Case.Status,
        Case.Diagnostic ? [new("retained-diagnostic", "Literal parser issue retained", 0, 1)] : [], (JsonObject)Metadata.DeepClone(),
        Case.Diagnostic || Case.Status is StructuralParseStatus.Unsupported or StructuralParseStatus.Malformed ? "eA==" : null,
        Case.Raw ? null : Lookup.Receipt, Case.Raw ? null : Lookup.Seal, !Case.Raw);

    public static SourceExtractReadFixture Create(SourceExtractReadCase test)
    {
        var fixture = new SourceExtractReadFixture { Case = test }; var canonical = new CanonicalJson();
        var kind = test.Noncache ? "manifest" : test.Id == "hit-registered-cache-kind" ? "registered-cache-kind" : "sdk-api-index";
        var original = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        { ["path"] = "logical/API.xml", ["kind"] = kind, ["platform_api_version"] = null, ["source_release_context"] = "37.2 is acquisition context only" });
        var context = new RehydrationRowContext("fixture/repository", "pinned-revision", "public", "fixture-scope", "Apache-2.0", JsonSerializer.SerializeToElement("37.2 is acquisition context only"), [], []);
        fixture.Row = new(0, "logical/API.xml", kind, 0, test.Noncache ? null : 0, context, original);
        fixture.ExpectedContext = new("fixture/repository", "pinned-revision", RecipeSha, null);
        var physical = new RehydrationPhysicalSource(0, new("raw/API.xml", new("https://example.invalid/API.xml"), "pinned-revision", RawSha, 1), [0], [], []);
        var alias = new RehydrationCacheAlias(0, "historical/API.json.gz", null, [0], []);
        var projection = new RehydrationSourceProjection("fixture-release", RecipeSha, AcquisitionCompatibilityProfile.CanonicalRestore, [fixture.Row], [physical], test.Noncache ? [] : [alias], [0], true);
        var producer = new SourceExtractProducerIdentity("csharp-source-v1", new Dictionary<string, string> { ["assembly"] = new('a', 64), ["parser"] = new('b', 64), ["context"] = new('c', 64) }, "net10", new('b', 64), new('c', 64));
        var receipt = new SourceCacheReceipt("old-generation/cache/API.gz", producer.ProducerId, SourceExtractionProfile.CurrentCandidate.Id, RecipeSha, RawSha, new('d', 64), new('e', 64), null, 1, false);
        var seal = new SourceCacheSeal(new(receipt.Path, new('e', 64), 123, "application/gzip"), new(receipt.Path + ".csharp-cache.json", new('f', 64), 321, "application/json"), 1);
        fixture.Lookup = new(fixture.RecordBytes, receipt, seal);
        var expected = fixture.ExpectedRow();
        var indexed = new SourceExtractIndexRow(0, fixture.Row.LogicalPath, fixture.Row.Kind, 0, 0, RawSha, 1, canonical.Hash(original), fixture.ExpectedContext, context.Scope,
            test.Status, expected.Issues, expected.Metadata, expected.RawSourceBase64, receipt, seal);
        fixture.Index = new("androidmcp-native-source-index-v1", projection.Release, RecipeSha, SourceExtractionProfile.CurrentCandidate.Id, producer, [indexed]);
        fixture.Request = new(projection, SourceExtractionProfile.CurrentCandidate, producer, null);
        switch (test.Id)
        {
            case "index-recipe": fixture.Index = fixture.Index with { RecipeSha256 = new('9', 64) }; break;
            case "index-release": fixture.Index = fixture.Index with { Release = "other-release" }; break;
            case "index-profile": fixture.Index = fixture.Index with { ProfileId = SourceExtractionProfile.RELEASE011.Id }; break;
            case "index-producer": fixture.Index = fixture.Index with { Producer = producer with { ProducerId = "historical-python" } }; break;
            case "index-runtime": fixture.Index = fixture.Index with { Producer = producer with { RuntimeIdentity = "old-runtime" } }; break;
            case "index-parser": fixture.Index = fixture.Index with { Producer = producer with { ParserIdentity = new('9', 64) } }; break;
            case "index-context-producer": fixture.Index = fixture.Index with { Producer = producer with { ContextIdentity = new('9', 64) } }; break;
            case "index-code": fixture.Index = fixture.Index with { Producer = producer with { ProducerFiles = new Dictionary<string,string> { ["assembly"] = new('9',64), ["parser"] = new('b',64), ["context"] = new('c',64) } } }; break;
            case "index-schema": fixture.Index = fixture.Index with { Schema = "historical-python-index" }; break;
            case "row-path": indexed = indexed with { LogicalPath = "other/API.xml" }; break;
            case "row-kind": indexed = indexed with { Kind = "java-source" }; break;
            case "row-physical": indexed = indexed with { PhysicalSourceIndex = 9 }; break;
            case "row-cache-alias": indexed = indexed with { CacheAliasIndex = 9 }; break;
            case "row-index": indexed = indexed with { RowIndex = 9 }; break;
            case "row-sha": indexed = indexed with { SourceSha256 = new('9', 64) }; break;
            case "row-length": indexed = indexed with { SourceLength = 2 }; break;
            case "row-original": indexed = indexed with { OriginalRowCanonicalSha256 = new('9', 64) }; break;
            case "row-context": indexed = indexed with { Context = fixture.ExpectedContext with { SnapshotId = "catalog-derived-sql-sid" } }; break;
            case "row-platform-guess": indexed = indexed with { Context = fixture.ExpectedContext with { PlatformVersion = "37.2" } }; break;
            case "row-scope": indexed = indexed with { Scope = "other-scope" }; break;
            case "raw-evidence-missing": indexed = indexed with { RawSourceBase64 = null }; break;
            case "raw-evidence-base64": indexed = indexed with { RawSourceBase64 = "not base64" }; break;
            case "raw-evidence-hash": indexed = indexed with { RawSourceBase64 = "eQ==" }; break;
            case "raw-evidence-length": indexed = indexed with { RawSourceBase64 = "eHg=" }; break;
        }
        fixture.Index = fixture.Index with { Rows = test.Noncache ? [] : [indexed] };
        if(test.Id == "row-duplicate") fixture.Index = fixture.Index with { Rows = [indexed,indexed] };
        var data = new Artifact("old-generation/source-index.json", canonical.Serialize(JsonSerializer.SerializeToElement(fixture.Index)), "application/json");
        fixture.Files[data.RelativePath] = data;
        var dataPin = Pin(data);
        fixture.Manifest = new(test.Id == "manifest-schema" ? "wrong-schema" : "androidmcp-native-source-generation-v1", "old-generation", [], [dataPin], dataPin);
        var manifest = new Artifact("old-generation/index.json", canonical.Serialize(JsonSerializer.SerializeToElement(fixture.Manifest)), "application/json");
        fixture.Files[manifest.RelativePath] = manifest;
        fixture.Request = fixture.Request with { TrustedIndex = test.NoIndex ? null : Pin(manifest) };
        if (test.Id == "manifest-pin-sha") fixture.Request = fixture.Request with { TrustedIndex = fixture.Request.TrustedIndex! with { Sha256 = new('9', 64) } };
        if (test.Id == "manifest-pin-length") fixture.Request = fixture.Request with { TrustedIndex = fixture.Request.TrustedIndex! with { Length = manifest.Bytes.Length + 1 } };
        if (test.Id is "manifest-malformed" or "manifest-duplicate")
        {
            manifest = manifest with { Bytes = Encoding.UTF8.GetBytes(test.Id == "manifest-malformed" ? "{" : "{\"Schema\":1,\"Schema\":2}") };
            fixture.Files[manifest.RelativePath] = manifest; fixture.Request = fixture.Request with { TrustedIndex = Pin(manifest) };
        }
        if (test.Id == "index-data-pin") fixture.Files[data.RelativePath] = data with { Bytes = new byte[] { 0xff } };
        if (test.Raw) fixture.Files[physical.Pin.LocalPath] = new(physical.Pin.LocalPath, Encoding.UTF8.GetBytes(test.Id.StartsWith("raw-source-pin",StringComparison.Ordinal) ? "y" : "x"), "application/octet-stream");
        return fixture;
    }
    private static StoredArtifact Pin(Artifact artifact) => new(artifact.RelativePath, ContentHash.Sha256(artifact.Bytes.Span), artifact.Bytes.Length, artifact.MediaType);
}

public static class SourceExtractReadContractCases
{
    public static IReadOnlyList<SourceExtractReadCase> All { get; } =
    [
        new("hit-clean"), new("hit-registered-cache-kind"), new("hit-repeat", Repeat:true), new("hit-issue", Diagnostic:true),
        new("hit-unsupported", Status:StructuralParseStatus.Unsupported), new("hit-malformed", Status:StructuralParseStatus.Malformed),
        new("raw-no-index", Raw:true, NoIndex:true), new("raw-noncache", Raw:true, Noncache:true),
        new("raw-direct-resolve", Raw:true, NoIndex:true, DirectResolve:true),
        new("raw-source-pin-direct", "SOURCE_EXTRACT_RAW_PIN", Raw:true, NoIndex:true, DirectResolve:true),
        new("manifest-pin-sha", "SOURCE_EXTRACT_INDEX_PIN"), new("manifest-pin-length", "SOURCE_EXTRACT_INDEX_PIN"),
        new("manifest-malformed", "SOURCE_EXTRACT_INDEX_JSON"), new("manifest-duplicate", "SOURCE_EXTRACT_INDEX_JSON"),
        new("manifest-schema", "SOURCE_EXTRACT_INDEX_SCHEMA"), new("index-data-pin", "SOURCE_EXTRACT_INDEX_PIN"),
        new("index-schema", "SOURCE_EXTRACT_INDEX_SCHEMA"),
        new("index-recipe", "SOURCE_EXTRACT_INDEX_IDENTITY"), new("index-release", "SOURCE_EXTRACT_INDEX_IDENTITY"), new("index-profile", "SOURCE_EXTRACT_INDEX_IDENTITY"),
        new("index-producer", "SOURCE_EXTRACT_INDEX_IDENTITY"), new("index-runtime", "SOURCE_EXTRACT_INDEX_IDENTITY"), new("index-parser", "SOURCE_EXTRACT_INDEX_IDENTITY"), new("index-context-producer", "SOURCE_EXTRACT_INDEX_IDENTITY"),
        new("index-code", "SOURCE_EXTRACT_INDEX_IDENTITY"),
        new("row-path", "SOURCE_EXTRACT_ROW_IDENTITY"), new("row-kind", "SOURCE_EXTRACT_ROW_IDENTITY"), new("row-physical", "SOURCE_EXTRACT_ROW_IDENTITY"), new("row-cache-alias", "SOURCE_EXTRACT_ROW_IDENTITY"), new("row-index", "SOURCE_EXTRACT_ROW_IDENTITY"), new("row-duplicate", "SOURCE_EXTRACT_ROW_IDENTITY"),
        new("row-sha", "SOURCE_EXTRACT_ROW_IDENTITY"), new("row-length", "SOURCE_EXTRACT_ROW_IDENTITY"),
        new("row-original", "SOURCE_EXTRACT_ROW_IDENTITY"), new("row-context", "SOURCE_EXTRACT_ROW_IDENTITY"), new("row-platform-guess", "SOURCE_EXTRACT_ROW_IDENTITY"), new("row-scope", "SOURCE_EXTRACT_ROW_IDENTITY"),
        new("raw-evidence-missing", "SOURCE_EXTRACT_RAW_EVIDENCE", Diagnostic:true), new("raw-evidence-base64", "SOURCE_EXTRACT_RAW_EVIDENCE", Diagnostic:true),
        new("raw-evidence-hash", "SOURCE_EXTRACT_RAW_EVIDENCE", Diagnostic:true), new("raw-evidence-length", "SOURCE_EXTRACT_RAW_EVIDENCE", Diagnostic:true),
        new("raw-source-pin", "SOURCE_EXTRACT_RAW_PIN", Raw:true, NoIndex:true),
        new("cache-capability", "SOURCE_EXTRACT_CACHE_CAPABILITY"), new("lookup-infrastructure", "infrastructure"), new("cancelled", "cancelled")
    ];
    public static IEnumerable<object[]> Cases => All.Select(test => new object[] { test.Id });
    public static SourceExtractReadCase Get(string id) => All.Single(test => test.Id == id);
    public static async Task AssertContract(SourceExtractReadCase test, ISourceExtractReadProbe probe)
    {
        var fixture = SourceExtractReadFixture.Create(test); using var cancellation = new CancellationTokenSource(); if(test.Id == "cancelled") cancellation.Cancel();
        var observed = await probe.ReadAsync(fixture, cancellation.Token);
        Assert.Equal(0, observed.Writes); Assert.True(observed.OriginalsRetained); Assert.True(observed.TokenForwarded);
        Assert.Equal(fixture.WantedCreates, observed.RuntimeCreates); Assert.Equal(observed.RuntimeCreates, observed.RuntimeDisposals);
        Assert.Equal(fixture.WantedReads, observed.RawReads); Assert.Equal(test.Raw && test.Error is null ? 1 : 0, observed.Dispatches); Assert.Equal(fixture.WantedLookup, observed.CacheLookups);
        if(test.Error is not null)
        {
            Assert.Empty(observed.Rows); Assert.NotNull(observed.Error);
            if(test.Error == "infrastructure") Assert.Same(fixture.InfrastructureFault, observed.Error);
            else if(test.Error == "cancelled") Assert.IsAssignableFrom<OperationCanceledException>(observed.Error);
            else Assert.Equal(test.Error, Assert.IsType<SourceExtractException>(observed.Error).Code);
            return;
        }
        Assert.Null(observed.Error); Assert.Equal(test.Repeat ? 2 : 1, observed.Rows.Count);
        var wanted = fixture.ExpectedRow();
        foreach(var row in observed.Rows)
        {
            Assert.Equal(wanted.RowIndex,row.RowIndex); Assert.Equal(wanted.SourceLength,row.SourceLength); Assert.Equal(wanted.Context,row.Context); Assert.Null(row.Context.PlatformVersion);
            Assert.Equal(wanted.RecordsJson.ToArray(),row.RecordsJson.ToArray()); Assert.Equal(wanted.StructuralStatus,row.StructuralStatus); Assert.Equal(wanted.Issues,row.Issues);
            Assert.True(JsonNode.DeepEquals(wanted.Metadata,row.Metadata)); Assert.Equal(wanted.RawSourceBase64,row.RawSourceBase64);
            Assert.Equal(wanted.CacheHit,row.CacheHit); Assert.Equal(wanted.CacheReceipt,row.CacheReceipt); Assert.Equal(wanted.CacheSeal,row.CacheSeal);
        }
        Assert.Contains("factory-create",observed.Events); Assert.EndsWith("runtime-dispose",observed.Events.Last());
        if(!test.Raw) Assert.DoesNotContain("raw-read",observed.Events);
    }
}
