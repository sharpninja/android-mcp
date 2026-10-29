using System.Globalization;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Sources;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record NativeSourceCacheFixtureResult(SourceExtractGenerationRequest Request, SourceExtractIndex Index,
    IReadOnlyList<SourceExtractRow> ProducedRows, ArtifactGenerationResult Generation);

/// <summary>
/// Shared native consumer INPUT preparation, not the Sources/Extract writer or
/// an expected-result oracle. Catalog retains its independently authored SQL,
/// permission, declaration-count and no-raw-read assertions.
/// </summary>
public static class NativeSourceCacheGenerationFixture
{
    public static SourceExtractProducerIdentity Producer(SourceJavaAdmission? java = null)
    {
        var assembly = ContentHash.Sha256(File.ReadAllBytes(typeof(SourcesExtractService).Assembly.Location));
        var parser = java is null ? assembly : ContentHash.Sha256(File.ReadAllBytes(java.ManifestPath));
        return new("csharp-native-fixture-generation-v1", new Dictionary<string, string>(StringComparer.Ordinal)
        { ["native/assembly"] = assembly, ["native/parser"] = parser, ["native/context"] = assembly }, "net10", parser, assembly);
    }

    public static async Task<NativeSourceCacheFixtureResult> CreateAsync(SourceExtractGenerationRequest request,
        IArtifactStore rawSources, IArtifactStore generations, IArtifactLeaseProvider leases,
        ISourcesRuntimeFactory runtimeFactory, IReadOnlyList<int> cachedRowIndices, string generationRoot,
        CancellationToken cancellationToken = default, Func<SourceExtractIndex, SourceExtractIndex>? indexMutation = null)
    {
        _ = ArtifactPaths.Segments(generationRoot);
        if (request.TrustedIndex is not null) throw new ArgumentException("Native fixture preparation creates a fresh generation, not an unpinned cache adoption");
        var canonical = new CanonicalJson(); var cache = new SourceCache(generations, leases, canonical);
        var resolver = new SourcesExtractService(rawSources, generations, runtimeFactory, cache, canonical);
        var selected = cachedRowIndices.Select(index => request.Projection.Rows.Single(row => row.RowIndex == index)).ToArray();
        var specs = selected.Select(row => request.Projection.PhysicalSources.Single(physical => physical.PhysicalSourceIndex == row.PhysicalSourceIndex))
            .DistinctBy(physical => physical.PhysicalSourceIndex).Select(physical => new ArtifactSourceSpec(physical.Pin.LocalPath, "application/octet-stream",
                physical.Pin.SizeBytes is { } size ? new(physical.Pin.LocalPath, physical.Pin.Sha256, size, "application/octet-stream") : null)).ToArray();
        var produced = new List<SourceExtractRow>(); var indexed = new List<SourceExtractIndexRow>(); SourceExtractIndex? indexData = null;
        var dataPath = generationRoot + "/source-index.json";
        var manifests = new FixtureManifestFactory(generationRoot + "/index.json", dataPath, canonical);
        var runtime = new ArtifactGenerationRuntime(rawSources, generations, leases, new ArtifactReceiptVerifier(new NativeContentHash()), manifests, "native-source-fixture/" + generationRoot);
        var recipe = new ArtifactGenerationRecipe(specs, _ => new([dataPath], async token =>
        {
            foreach (var row in selected)
            {
                var resolved = await resolver.ResolveAsync(new(request.Projection, row, request.Profile, request.Producer, request.Java), token);
                var physical = request.Projection.PhysicalSources.Single(physical => physical.PhysicalSourceIndex == row.PhysicalSourceIndex);
                var alias = (row.CacheAliasIndex ?? row.RowIndex).ToString(CultureInfo.InvariantCulture);
                var publication = await cache.PublishAsync(new(generationRoot + "/cache/" + alias + ".json.gz", row.Kind!, request.Producer.ProducerId,
                    request.Profile.Id, request.Projection.RecipeSha256, physical.Pin.Sha256, resolved.RecordsJson,
                    ProducerFiles: request.Producer.ProducerFiles, RuntimeIdentity: request.Producer.RuntimeIdentity), token);
                produced.Add(resolved);
                indexed.Add(new(row.RowIndex, row.LogicalPath, row.Kind, row.PhysicalSourceIndex, row.CacheAliasIndex, physical.Pin.Sha256,
                    resolved.SourceLength, canonical.Hash(row.OriginalRow), resolved.Context, row.Context.Scope, resolved.StructuralStatus,
                    resolved.Issues, resolved.Metadata, resolved.RawSourceBase64, publication.Receipt, publication.Seal));
            }
            indexData = new("androidmcp-native-source-index-v1", request.Projection.Release, request.Projection.RecipeSha256,
                request.Profile.Id, request.Producer, indexed.ToArray());
            if (indexMutation is not null) indexData = indexMutation(indexData);
            return new Artifact[] { new(dataPath, canonical.Serialize(JsonSerializer.SerializeToElement(indexData)), "application/json") };
        }));
        var committed = await runtime.ExecuteAsync(generationRoot, recipe, cancellationToken);
        return new(request with { TrustedIndex = committed.Manifest }, indexData!, produced.ToArray(), committed);
    }

    // Fixture DATA serialization over the authoritative Source DTO. Core owns
    // lease/preflight/input recheck/immutable writes/commit-last behavior.
    private sealed class FixtureManifestFactory(string relativePath, string dataPath, ICanonicalJson canonical) : IArtifactGenerationManifestFactory
    {
        public string RelativePath { get; } = relativePath;
        public Artifact Create(string generationId, IReadOnlyList<StoredArtifact> inputs, IReadOnlyList<StoredArtifact> outputs)
        {
            var data = outputs.Single(artifact => artifact.RelativePath == dataPath);
            var manifest = new SourceExtractGenerationManifest("androidmcp-native-source-generation-v1", generationId, inputs, outputs, data);
            return new(RelativePath, canonical.Serialize(JsonSerializer.SerializeToElement(manifest)), "application/json");
        }
    }
}
