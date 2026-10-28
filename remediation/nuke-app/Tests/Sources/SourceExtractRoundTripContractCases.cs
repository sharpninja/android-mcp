using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record SourceExtractRoundTripCase(string Id);
public sealed record SourceExtractRoundTripObservation(SourceExtractResult Written, IReadOnlyList<SourceExtractRow> Loaded,
    StoredArtifact SubmittedIndex, bool CommitPinMatches, int RawReadsAfterCommit, int ExtractionsAfterCommit,
    int LookupsAfterCommit, bool OriginalsRetained, bool ReaderDisposed);
public interface ISourceExtractRoundTripProbe
{ Task<SourceExtractRoundTripObservation> RunAsync(SourceExtractRoundTripCase test); }

public static class SourceExtractRoundTripContractCases
{
    public static readonly string[] Ids = ["fresh", "alias-two-logical", "diagnostic", "trusted-hit-missing-raw"];
    public static IEnumerable<object[]> Cases => Ids.Select(id => new object[] { id });
    public static async Task AssertContract(SourceExtractRoundTripCase test, ISourceExtractRoundTripProbe probe)
    {
        var fixture = SourceExtractWriteFixture.Create(SourceExtractWriteContractCases.Get(test.Id));
        var result = await probe.RunAsync(test);
        Assert.True(result.CommitPinMatches); Assert.Equal(result.Written.Index, result.SubmittedIndex);
        Assert.True(result.OriginalsRetained); Assert.True(result.ReaderDisposed);
        Assert.Equal(0, result.RawReadsAfterCommit); Assert.Equal(0, result.ExtractionsAfterCommit); Assert.Equal(1, result.LookupsAfterCommit);
        Assert.Equal(fixture.SelectedCount, result.Written.Rows.Count); Assert.Equal(fixture.SelectedCount, result.Loaded.Count);
        for (var index = 0; index < fixture.SelectedCount; index++)
        {
            var expected = fixture.ExpectedRows[index]; var written = result.Written.Rows[index]; var loaded = result.Loaded[index];
            // Receipt bytes are native cache output. Expected source facts and
            // context remain independently authored fixture literals.
            var expectedJson = JsonSerializer.SerializeToNode(expected)!.AsObject();
            expectedJson.Remove("CacheReceipt"); expectedJson.Remove("CacheSeal");
            SourceExtractionContractCases.AssertSubset(expectedJson, JsonSerializer.SerializeToNode(written));
            expectedJson["CacheHit"] = true;
            SourceExtractionContractCases.AssertSubset(expectedJson, JsonSerializer.SerializeToNode(loaded));
            Assert.NotNull(written.CacheReceipt); Assert.NotNull(written.CacheSeal);
            Assert.Equal(written.CacheReceipt, loaded.CacheReceipt); Assert.Equal(written.CacheSeal, loaded.CacheSeal);
            Assert.Equal(loaded.CacheReceipt!.Path, loaded.CacheSeal!.Data.RelativePath);
        }
    }
}

public sealed class SourceExtractRoundTripState
{
    public int RawReads, Extractions, Lookups;
    public bool ReaderDisposed;
    public StoredArtifact SubmittedIndex { get; set; } = null!;
}

public static class SourceExtractRoundTripHarness
{
    // Both scripted mocks and native binding execute these actual operation and
    // reader interfaces. The original projection/context is forwarded intact.
    public static async Task<SourceExtractRoundTripObservation> RunAsync(SourceExtractWriteFixture fixture,
        ISourcesExtractOperation writer, ISourceExtractGenerationReader reader, SourceExtractRoundTripState state,
        Action afterCommit, Func<bool> retained, Func<StoredArtifact, bool> pinMatches)
    {
        var written = await writer.ExtractAsync(fixture.Request);
        afterCommit(); var loaded = new List<SourceExtractRow>();
        using (var generation = await reader.OpenAsync(new(fixture.Request.Projection, fixture.Request.Profile,
            fixture.Request.Producer, written.Index, fixture.Request.Java)))
        {
            foreach (var row in fixture.Request.Projection.Rows.Where(row => written.Rows.Any(item => item.RowIndex == row.RowIndex)))
                loaded.Add(await generation.ReadAsync(row));
        }
        return new(written, loaded, state.SubmittedIndex, pinMatches(written.Index), state.RawReads, state.Extractions,
            state.Lookups, retained(), state.ReaderDisposed);
    }
}
