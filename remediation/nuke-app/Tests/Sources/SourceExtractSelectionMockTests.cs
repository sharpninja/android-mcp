using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
public sealed class SourceExtractSelectionMockTests
{
    public static IEnumerable<object[]> Controls => SourceExtractSelectionContractCases.Ids.SelectMany(id => new[] { "selection", "raw", "publication", "count", "rows", "manifest", "retention" }.Select(wrong => new object[] { id, wrong }));
    [Theory, MemberData(nameof(SourceExtractSelectionContractCases.Cases), MemberType = typeof(SourceExtractSelectionContractCases))]
    public Task CorrectMocksProvePhysicalSelectionBeforeLogicalCacheLimit(string id) => SourceExtractSelectionContractCases.AssertContract(id,
        new SourceExtractSelectionProbe((fixture, rig) => new Scripted(fixture, rig)));
    [Theory, MemberData(nameof(Controls))]
    public async Task EveryBroadenedSelectionOrUnselectedEffectIsRejected(string id, string wrong) => Assert.NotNull(await Record.ExceptionAsync(() =>
        SourceExtractSelectionContractCases.AssertContract(id, new SourceExtractSelectionProbe((fixture, rig) => new Scripted(fixture, rig, wrong)))));
    private sealed class Scripted(SourceExtractSelectionFixture fixture, SourceExtractWriteDependencies rig, string? wrong = null) : ISourcesExtractOperation
    {
        public async Task<SourceExtractResult> ExtractAsync(SourceExtractRequest request, CancellationToken cancellationToken = default)
        {
            Assert.Equal(fixture.Request, request); var selected = wrong == "selection" ? fixture.WrongSelection : fixture.Wanted;
            using var runtime = rig.Runtimes.Create(request.Profile, request.Java);
            foreach (var index in selected)
            {
                var row = request.Projection.Rows.Single(row => row.RowIndex == index); var physical = request.Projection.PhysicalSources.Single(source => source.PhysicalSourceIndex == row.PhysicalSourceIndex);
                var bytes = await rig.Raw.ReadAsync(physical.Pin.LocalPath, cancellationToken);
                runtime.Dispatcher.Extract(new(row.LogicalPath, bytes, fixture.Basis.Context), row.Kind, row.Context.Scope);
            }
            if (wrong == "raw") await rig.Raw.ReadAsync(SourceExtractSelectionFixture.ExtraPath, cancellationToken);
            var publicationCount = (selected.Length > 0 ? 1 : 0) + (wrong == "publication" ? 1 : 0);
            for (var index = 0; index < publicationCount; index++)
                await rig.Cache.PublishAsync(new("new-generation/cache/0.json.gz", request.Projection.Rows[0].Kind!, request.Producer.ProducerId, request.Profile.Id,
                    request.Projection.RecipeSha256, SourceExtractReadFixture.RawSha, fixture.Basis.Records(), ProducerFiles: request.Producer.ProducerFiles, RuntimeIdentity: request.Producer.RuntimeIdentity), cancellationToken);
            if (wrong == "retention") rig.RawFiles[SourceExtractSelectionFixture.ExtraPath] = rig.RawFiles[SourceExtractSelectionFixture.ExtraPath] with { Bytes = new byte[] { 0xff } };
            var rows = selected.Select(index => fixture.Basis.ExpectedRows.Single(row => row.RowIndex == index)).ToArray();
            var canonical = new CanonicalJson();
            var indexed = rows.Select(result =>
            {
                var row = request.Projection.Rows.Single(row => row.RowIndex == result.RowIndex);
                return new SourceExtractIndexRow(row.RowIndex, row.LogicalPath, row.Kind, row.PhysicalSourceIndex, row.CacheAliasIndex,
                    SourceExtractReadFixture.RawSha, result.SourceLength, canonical.Hash(row.OriginalRow), result.Context, row.Context.Scope,
                    result.StructuralStatus, result.Issues, result.Metadata, result.RawSourceBase64, result.CacheReceipt!, result.CacheSeal!);
            }).ToArray();
            var data = new Artifact("new-generation/source-index.json", canonical.Serialize(JsonSerializer.SerializeToElement(new SourceExtractIndex(
                "androidmcp-native-source-index-v1", request.Projection.Release, request.Projection.RecipeSha256, request.Profile.Id, request.Producer, indexed))), "application/json");
            var dataPin = await rig.Generations.WriteAsync(data, cancellationToken);
            var inputPaths = selected.Select(index => request.Projection.Rows.Single(row => row.RowIndex == index)).Select(row => request.Projection.PhysicalSources.Single(source => source.PhysicalSourceIndex == row.PhysicalSourceIndex).Pin.LocalPath).Distinct(StringComparer.Ordinal).ToList();
            if (wrong == "manifest") inputPaths.Add(SourceExtractSelectionFixture.ExtraPath);
            var inputs = inputPaths.Select(path => SourceExtractWriteFixture.Pin(rig.RawFiles[path]) with { RelativePath = "source/" + path }).ToArray();
            var outputs = publicationCount > 0 ? fixture.Basis.PublishedArtifacts.Select(SourceExtractWriteFixture.Pin).Append(dataPin).ToArray() : [dataPin];
            var commit = await rig.Generations.WriteAsync(new("new-generation/index.json", canonical.Serialize(JsonSerializer.SerializeToElement(new SourceExtractGenerationManifest(
                "androidmcp-native-source-generation-v1", "new-generation", inputs, outputs, dataPin))), "application/json"), cancellationToken);
            if (wrong == "rows") rows = rows.Length == 0 ? [fixture.Basis.ExpectedRows[0]] : [];
            return new(commit, rows, selected.Length + (wrong == "count" ? 1 : 0), selected.Length > 0 ? 1 : 0, 0, selected.Length);
        }
    }
}
