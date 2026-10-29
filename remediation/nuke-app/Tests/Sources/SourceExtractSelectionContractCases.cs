using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class SourceExtractSelectionFixture
{
    public required string Id { get; init; }
    public SourceExtractWriteFixture Basis { get; private set; } = null!;
    public SourceExtractRequest Request { get; private set; } = null!;
    public int[] Wanted { get; private set; } = [];
    public int[] WrongSelection { get; private set; } = [];
    public const string ExtraPath = "raw/UNSELECTED.xml";
    public static SourceExtractSelectionFixture Create(string id)
    {
        var fixture = new SourceExtractSelectionFixture { Id = id, Basis = SourceExtractWriteFixture.Create(SourceExtractWriteContractCases.Get("alias-two-logical")) };
        var projection = fixture.Basis.Request.Projection;
        if (id.StartsWith("physical", StringComparison.Ordinal))
        {
            var first = projection.PhysicalSources[0] with { SelectionRowIndices = [0] };
            var second = first with { PhysicalSourceIndex = 1, Pin = first.Pin with { LocalPath = "raw/API2.xml" }, SelectionRowIndices = [1] };
            projection = projection with { Rows = [projection.Rows[0], projection.Rows[1] with { PhysicalSourceIndex = 1 }], PhysicalSources = [first, second],
                SelectedPhysicalSourceIndices = id == "physical-zero" ? [] : id == "physical-one-second" ? [1] : [0] };
            fixture.Basis.RawFiles[second.Pin.LocalPath] = new(second.Pin.LocalPath, Encoding.UTF8.GetBytes("x"), "application/octet-stream");
        }
        fixture.Basis.RawFiles[ExtraPath] = new(ExtraPath, Encoding.UTF8.GetBytes("x"), "application/octet-stream");
        fixture.Request = fixture.Basis.Request with { Projection = projection, CacheSourceLimit = id is "physical-one-second" or "aliases-cache-one" ? 1 : id == "aliases-cache-zero" ? 0 : null };
        fixture.Wanted = id switch { "physical-zero" or "aliases-cache-zero" => [], "physical-one-second" => [1], "aliases-physical-one" => [0, 1], _ => [0] };
        fixture.WrongSelection = id switch { "physical-one-second" => [0], "physical-one" or "aliases-cache-one" => [0, 1], _ => [0] };
        return fixture;
    }
}
public sealed record SourceExtractSelectionObservation(SourceExtractResult? Result, Exception? Error, SourceExtractWriteDependencies Dependencies,
    IReadOnlyList<string> OriginalRowsBefore, IReadOnlyList<int> SelectedPhysicalBefore);
public interface ISourceExtractSelectionProbe
{ Task<SourceExtractSelectionObservation> RunAsync(SourceExtractSelectionFixture fixture); }
public sealed class SourceExtractSelectionProbe(Func<SourceExtractSelectionFixture, SourceExtractWriteDependencies, ISourcesExtractOperation> create) : ISourceExtractSelectionProbe
{
    public async Task<SourceExtractSelectionObservation> RunAsync(SourceExtractSelectionFixture fixture)
    {
        var rig = new SourceExtractWriteDependencies(fixture.Basis, CancellationToken.None); SourceExtractResult? result = null; Exception? error = null;
        var originals = fixture.Request.Projection.Rows.Select(row => row.OriginalRow.GetRawText()).ToArray(); var selected = fixture.Request.Projection.SelectedPhysicalSourceIndices.ToArray();
        try { result = await create(fixture, rig).ExtractAsync(fixture.Request); } catch (Exception failure) { error = failure; }
        return new(result, error, rig, originals, selected);
    }
}
public static class SourceExtractSelectionContractCases
{
    public static readonly string[] Ids = ["physical-zero", "physical-one", "physical-one-second", "aliases-physical-one", "aliases-cache-one", "aliases-cache-zero"];
    public static IEnumerable<object[]> Cases => Ids.Select(id => new object[] { id });
    public static async Task AssertContract(string id, ISourceExtractSelectionProbe probe)
    {
        var fixture = SourceExtractSelectionFixture.Create(id); var value = await probe.RunAsync(fixture); var rig = value.Dependencies;
        Assert.Null(value.Error); Assert.True(rig.OriginalsRetained);
        Assert.Equal(value.OriginalRowsBefore, fixture.Request.Projection.Rows.Select(row => row.OriginalRow.GetRawText()));
        Assert.Equal(value.SelectedPhysicalBefore, fixture.Request.Projection.SelectedPhysicalSourceIndices);
        var result = Assert.IsType<SourceExtractResult>(value.Result); Assert.Equal(fixture.Wanted, result.Rows.Select(row => row.RowIndex));
        Assert.Equal(fixture.Wanted.Length, result.SelectedLogicalCacheRows); Assert.Equal(fixture.Wanted.Length, result.RawExtractions); Assert.Equal(0, result.CacheHits);
        Assert.Equal(fixture.Wanted.Length > 0 ? 1 : 0, result.UniqueCacheAliases); Assert.Equal(fixture.Wanted.Length, rig.Dispatches);
        Assert.Equal(fixture.Wanted.Length > 0 ? 1 : 0, rig.Publications);
        var paths = fixture.Wanted.Select(index => fixture.Request.Projection.Rows.Single(row => row.RowIndex == index)).Select(row =>
            fixture.Request.Projection.PhysicalSources.Single(source => source.PhysicalSourceIndex == row.PhysicalSourceIndex).Pin.LocalPath).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(paths, rig.Events.Where(item => item.StartsWith("raw-read:", StringComparison.Ordinal)).Select(item => item["raw-read:".Length..]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        foreach (var actual in result.Rows) SourceExtractionContractCases.AssertSubset(JsonSerializer.SerializeToNode(fixture.Basis.ExpectedRows.Single(row => row.RowIndex == actual.RowIndex)), JsonSerializer.SerializeToNode(actual));
        var committed = rig.GenerationFiles[result.Index.RelativePath]; Assert.Equal(SourceExtractWriteFixture.Pin(committed), result.Index);
        var manifest = JsonSerializer.Deserialize<SourceExtractGenerationManifest>(committed.Bytes.Span)!;
        Assert.Equal(paths.Select(path => "source/" + path), manifest.Inputs.Select(pin => pin.RelativePath).Order(StringComparer.Ordinal));
    }
}
