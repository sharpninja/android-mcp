using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
public sealed class SourceExtractRoundTripMockTests
{
    public static readonly string[] Wrong = ["records", "context", "platform", "status", "issues", "metadata", "raw-evidence",
        "hit", "receipt", "seal", "row", "length", "pin", "raw-read", "extract", "lookup", "retention", "dispose"];
    public static IEnumerable<object[]> Controls => SourceExtractRoundTripContractCases.Ids.SelectMany(id => Wrong.Select(wrong => new object[] { id, wrong }));
    [Theory, MemberData(nameof(SourceExtractRoundTripContractCases.Cases), MemberType = typeof(SourceExtractRoundTripContractCases))]
    public Task CorrectMocksProveCommittedIndexAndRawUnavailableReadbackAssertions(string id) =>
        SourceExtractRoundTripContractCases.AssertContract(new(id), new Probe());
    [Theory, MemberData(nameof(Controls))]
    public async Task EveryWrongFactIdentityRawAccessOrLookupResponseIsRejected(string id, string wrong) =>
        Assert.NotNull(await Record.ExceptionAsync(() => SourceExtractRoundTripContractCases.AssertContract(new(id), new Probe(wrong))));

    private sealed class Probe(string? wrong = null) : ISourceExtractRoundTripProbe
    {
        public Task<SourceExtractRoundTripObservation> RunAsync(SourceExtractRoundTripCase test)
        {
            var fixture = SourceExtractWriteFixture.Create(SourceExtractWriteContractCases.Get(test.Id));
            var state = new SourceExtractRoundTripState(); var rig = new SourceExtractWriteDependencies(fixture, CancellationToken.None);
            var provider = new ScriptedProviders(fixture, rig, state, wrong);
            return SourceExtractRoundTripHarness.RunAsync(fixture, provider, provider, state, () => { }, () => rig.OriginalsRetained,
                pin => rig.GenerationFiles.TryGetValue(pin.RelativePath, out var file) && SourceExtractWriteFixture.Pin(file) == pin);
        }
    }
    private sealed class ScriptedProviders(SourceExtractWriteFixture fixture, SourceExtractWriteDependencies rig,
        SourceExtractRoundTripState state, string? wrong) : ISourcesExtractOperation, ISourceExtractGenerationReader
    {
        private StoredArtifact committed = null!;
        public async Task<SourceExtractResult> ExtractAsync(SourceExtractRequest request, CancellationToken cancellationToken = default)
        {
            Assert.Equal(fixture.Request, request);
            var canonical = new CanonicalJson();
            var rows = fixture.ExpectedRows;
            var entries = rows.Select(result =>
            {
                var original = request.Projection.Rows.Single(row => row.RowIndex == result.RowIndex);
                return new SourceExtractIndexRow(original.RowIndex, original.LogicalPath, original.Kind, original.PhysicalSourceIndex,
                    original.CacheAliasIndex, SourceExtractReadFixture.RawSha, 1, canonical.Hash(original.OriginalRow), result.Context,
                    original.Context.Scope, result.StructuralStatus, result.Issues, result.Metadata, result.RawSourceBase64, result.CacheReceipt!, result.CacheSeal!);
            }).ToArray();
            // Correct mock fixture DATA over the authoritative domain DTOs.
            // No production index parser, cache codec or commit implementation.
            var data = new Artifact("new-generation/source-index.json", canonical.Serialize(JsonSerializer.SerializeToElement(new SourceExtractIndex(
                "androidmcp-native-source-index-v1", request.Projection.Release, request.Projection.RecipeSha256, request.Profile.Id, request.Producer, entries))), "application/json");
            var dataPin = await rig.Generations.WriteAsync(data, cancellationToken);
            var manifest = new SourceExtractGenerationManifest("androidmcp-native-source-generation-v1", "new-generation", [], [dataPin], dataPin);
            committed = await rig.Generations.WriteAsync(new("new-generation/index.json", canonical.Serialize(JsonSerializer.SerializeToElement(manifest)), "application/json"), cancellationToken);
            if (wrong == "retention")
            {
                if (rig.RawFiles.Count > 0) { var key = rig.RawFiles.Keys.First(); rig.RawFiles[key] = rig.RawFiles[key] with { Bytes = new byte[] { 0xff } }; }
                else { var key = fixture.GenerationFiles.Keys.First(); rig.GenerationFiles[key] = rig.GenerationFiles[key] with { Bytes = new byte[] { 0xff } }; }
            }
            return new(wrong == "pin" ? committed with { Sha256 = new('9', 64) } : committed, rows,
                fixture.SelectedCount, 1, fixture.WantedHits, fixture.WantedExtractions);
        }
        public Task<ISourceExtractGeneration> OpenAsync(SourceExtractGenerationRequest request, CancellationToken cancellationToken = default)
        {
            Assert.Equal(fixture.Request.Projection, request.Projection); Assert.Equal(fixture.Request.Profile, request.Profile);
            Assert.Equal(fixture.Request.Producer, request.Producer); Assert.Equal(fixture.Request.Java, request.Java);
            Assert.Equal(committed, request.TrustedIndex); state.SubmittedIndex = request.TrustedIndex!;
            return Task.FromResult<ISourceExtractGeneration>(new ScriptedGeneration(fixture, state, wrong));
        }
    }
    private sealed class ScriptedGeneration(SourceExtractWriteFixture fixture, SourceExtractRoundTripState state, string? wrong) : ISourceExtractGeneration
    {
        private bool first = true;
        public Task<SourceExtractRow> ReadAsync(RehydrationSourceRow source, CancellationToken cancellationToken = default)
        {
            Assert.Equal(fixture.Request.Projection.Rows.Single(row => row.RowIndex == source.RowIndex), source);
            if (first) { state.Lookups += wrong == "lookup" ? 2 : 1; first = false; }
            if (wrong == "raw-read") state.RawReads++;
            if (wrong == "extract") state.Extractions++;
            var row = fixture.ExpectedRows.Single(row => row.RowIndex == source.RowIndex) with { CacheHit = true };
            row = wrong switch
            {
                "records" => row with { RecordsJson = new byte[] { 0xff } }, "context" => row with { Context = row.Context with { SnapshotId = "sql-sid" } },
                "platform" => row with { Context = row.Context with { PlatformVersion = "guessed" } },
                "status" => row with { StructuralStatus = row.StructuralStatus == StructuralParseStatus.Clean ? StructuralParseStatus.Unsupported : StructuralParseStatus.Clean },
                "issues" => row with { Issues = row.Issues.Count == 0 ? [new("invented", "Wrong", null, null)] : [] },
                "metadata" => row with { Metadata = new JsonObject() }, "raw-evidence" => row with { RawSourceBase64 = row.RawSourceBase64 is null ? "invented" : null },
                "hit" => row with { CacheHit = false }, "receipt" => row with { CacheReceipt = null }, "seal" => row with { CacheSeal = null },
                "row" => row with { RowIndex = 99 }, "length" => row with { SourceLength = 99 }, _ => row
            };
            return Task.FromResult(row);
        }
        public void Dispose() { if (wrong != "dispose") state.ReaderDisposed = true; }
    }
}
