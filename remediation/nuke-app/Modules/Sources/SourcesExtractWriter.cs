using System.Globalization;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;

namespace AndroidMcp.BuildTool.Sources;

public sealed partial class SourcesExtractService
{
    public async Task<SourceExtractResult> ExtractAsync(SourceExtractRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request); cancellationToken.ThrowIfCancellationRequested();
        if (request.CacheSourceLimit is < 0) throw new ArgumentOutOfRangeException(nameof(request.CacheSourceLimit));
        _ = ArtifactPaths.Segments(request.GenerationRoot);
        if (ReferenceEquals(rawSources, generations)) throw new ArtifactGenerationException("SOURCE_OUTPUT_STORE", "Raw source and generation bindings must be separate");
        var admittedLeases = leases ?? throw new ArgumentException("An admitted generation lease provider is required for extraction publication");
        using var generation = (Generation)await OpenAsync(new(request.Projection, request.Profile, request.Producer, request.TrustedPriorIndex, request.Java), cancellationToken);
        var selected = request.Projection.Rows.Where(row => request.Projection.SelectedPhysicalSourceIndices.Contains(row.PhysicalSourceIndex) && row.Kind is not null && generation.CacheProducerKinds.Contains(row.Kind))
            .Take(request.CacheSourceLimit ?? int.MaxValue).ToArray();
        var rows = new List<SourceExtractRow>();
        var aliases = new Dictionary<int, CachePublicationGroup>();
        var qualified = new Dictionary<string, QualifiedSourceInput>(StringComparer.Ordinal);
        void Input(string prefix, IArtifactStore store, StoredArtifact pin)
        {
            var path = prefix + "/" + pin.RelativePath;
            var value = new QualifiedSourceInput(store, pin);
            if (qualified.TryGetValue(path, out var previous) && previous.Pin != pin)
                throw Failure("ALIAS_CONFLICT", "An aliased input has conflicting immutable pins");
            qualified[path] = value;
        }
        foreach (var row in selected)
        {
            var extracted = await generation.ReadAsync(row, cancellationToken);
            rows.Add(extracted);
            var physical = Physical(request.Projection, row);
            var alias = row.CacheAliasIndex ?? row.RowIndex;
            if (!aliases.TryGetValue(alias, out var group))
            {
                group = new(alias, row.Kind!, physical.Pin.Sha256, extracted);
                aliases.Add(alias, group);
            }
            else group.Admit(row.Kind!, physical.Pin.Sha256, extracted);
            group.RowIndices.Add(row.RowIndex);
            if (extracted.CacheHit)
            {
                foreach (var pin in generation.IndexPins) Input("generation", generations, pin);
                Input("generation", generations, extracted.CacheSeal!.Data);
                Input("generation", generations, extracted.CacheSeal.Sidecar);
            }
            else Input("source", rawSources, new(physical.Pin.LocalPath, physical.Pin.Sha256, extracted.SourceLength, "application/octet-stream"));
        }
        var dataPath = request.GenerationRoot + "/source-index.json";
        var planned = new List<string> { dataPath };
        foreach (var group in aliases.Values.Where(group => group.Seal is null))
        {
            group.NewPath = request.GenerationRoot + "/cache/" + group.Alias.ToString(CultureInfo.InvariantCulture) + ".json.gz";
            planned.Add(group.NewPath); planned.Add(group.NewPath + ".csharp-cache.json");
        }
        var manifests = new SourceGenerationManifestFactory(request.GenerationRoot + "/index.json", dataPath, json);
        var verifier = new ArtifactReceiptVerifier(new NativeContentHash());
        var engine = new ArtifactGenerationRuntime(new QualifiedSourceStore(qualified), generations, admittedLeases, verifier, manifests,
            "sources-extract/" + request.GenerationRoot);
        var specs = qualified.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair =>
            new ArtifactSourceSpec(pair.Key, pair.Value.Pin.MediaType, pair.Value.Pin with { RelativePath = pair.Key })).ToArray();
        var recipe = new ArtifactGenerationRecipe(specs, _ => new(planned, async token =>
        {
            var artifacts = new List<Artifact>();
            foreach (var group in aliases.Values)
            {
                if (group.Seal is null)
                {
                    var publication = await sealedCache.PublishAsync(new(group.NewPath!, group.Kind, request.Producer.ProducerId,
                        request.Profile.Id, request.Projection.RecipeSha256, group.SourceSha256, group.RecordsJson,
                        ProducerFiles: request.Producer.ProducerFiles, RuntimeIdentity: request.Producer.RuntimeIdentity), token);
                    group.Receipt = publication.Receipt; group.Seal = publication.Seal;
                    foreach (var pin in new[] { publication.Seal.Data, publication.Seal.Sidecar })
                    {
                        var bytes = await generations.ReadAsync(pin.RelativePath, token);
                        var artifact = new Artifact(pin.RelativePath, bytes.ToArray(), pin.MediaType);
                        if (!verifier.Matches(artifact, pin)) throw Failure("CACHE_PUBLICATION_PIN", "Published cache bytes differ from their returned seal");
                        artifacts.Add(artifact);
                    }
                }
            }
            var indexed = new List<SourceExtractIndexRow>();
            for (var position = 0; position < rows.Count; position++)
            {
                var extracted = rows[position]; var row = selected[position]; var group = aliases[row.CacheAliasIndex ?? row.RowIndex];
                extracted = extracted with { CacheReceipt = group.Receipt, CacheSeal = group.Seal }; rows[position] = extracted;
                indexed.Add(new(row.RowIndex, row.LogicalPath, row.Kind, row.PhysicalSourceIndex, row.CacheAliasIndex, group.SourceSha256,
                    extracted.SourceLength, json.Hash(row.OriginalRow), extracted.Context, row.Context.Scope, extracted.StructuralStatus,
                    extracted.Issues, Clone(extracted.Metadata), extracted.RawSourceBase64, group.Receipt!, group.Seal!));
            }
            var index = new SourceExtractIndex(IndexSchema, request.Projection.Release, request.Projection.RecipeSha256,
                request.Profile.Id, request.Producer, indexed.ToArray());
            artifacts.Add(new(dataPath, json.Serialize(JsonSerializer.SerializeToElement(index)), "application/json"));
            return artifacts;
        }));
        var committed = await engine.ExecuteAsync(request.GenerationRoot, recipe, cancellationToken);
        return new(committed.Manifest, rows.ToArray(), selected.Length, aliases.Count, rows.Count(row => row.CacheHit), rows.Count(row => !row.CacheHit));
    }

    private sealed record QualifiedSourceInput(IArtifactStore Store, StoredArtifact Pin);
    // This view only qualifies existing admitted input paths for Core's single
    // pin/recheck protocol. It contains no cache, JSON or recipe parser.
    private sealed class QualifiedSourceStore(IReadOnlyDictionary<string, QualifiedSourceInput> inputs) : IArtifactStore
    {
        public Task<ReadOnlyMemory<byte>> ReadAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            if (!inputs.TryGetValue(relativePath, out var input)) throw new FileNotFoundException(relativePath);
            return input.Store.ReadAsync(input.Pin.RelativePath, cancellationToken);
        }
        public Task<StoredArtifact> WriteAsync(Artifact artifact, CancellationToken cancellationToken = default) =>
            throw new IOException("Qualified source inputs are read-only");
    }
    private sealed class CachePublicationGroup(int alias, string kind, string sourceSha256, SourceExtractRow first)
    {
        internal int Alias { get; } = alias;
        internal string Kind { get; } = kind;
        internal string SourceSha256 { get; } = sourceSha256;
        internal ReadOnlyMemory<byte> RecordsJson { get; } = first.RecordsJson;
        internal List<int> RowIndices { get; } = [];
        internal SourceCacheReceipt? Receipt { get; set; } = first.CacheReceipt;
        internal SourceCacheSeal? Seal { get; set; } = first.CacheSeal;
        internal string? NewPath { get; set; }
        internal void Admit(string otherKind, string otherSha256, SourceExtractRow row)
        {
            if (otherKind != Kind || otherSha256 != SourceSha256 || !row.RecordsJson.Span.SequenceEqual(RecordsJson.Span) ||
                Seal is not null && row.CacheSeal is not null && (Seal != row.CacheSeal || Receipt != row.CacheReceipt))
                throw Failure("ALIAS_CONFLICT", "One projected cache alias has conflicting source, declaration or sealed producer identities");
            if (row.CacheSeal is not null) { Seal = row.CacheSeal; Receipt = row.CacheReceipt; }
        }
    }
    private sealed class SourceGenerationManifestFactory(string path, string dataPath, ICanonicalJson canonical) : IArtifactGenerationManifestFactory
    {
        public string RelativePath { get; } = path;
        public Artifact Create(string generationId, IReadOnlyList<StoredArtifact> inputs, IReadOnlyList<StoredArtifact> outputs)
        {
            var data = outputs.Single(output => output.RelativePath == dataPath);
            return new(RelativePath, canonical.Serialize(JsonSerializer.SerializeToElement(new SourceExtractGenerationManifest(
                ManifestSchema, generationId, inputs, outputs, data))), "application/json");
        }
    }
}
