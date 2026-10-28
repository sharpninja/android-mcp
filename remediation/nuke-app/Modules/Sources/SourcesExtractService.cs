using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;

namespace AndroidMcp.BuildTool.Sources;

/// <summary>The shared native cache intake and raw miss resolver. Publication is a later gated increment.</summary>
public sealed class SourcesExtractService(IArtifactStore rawSources, IArtifactStore generations,
    ISourcesRuntimeFactory runtimes, ISealedSourceCache cache, ICanonicalJson canonical)
    : ISourceExtractGenerationReader, ISourceExtractResolver
{
    private readonly ICanonicalJson json = canonical;
    private readonly ISealedSourceCache sealedCache = cache;
    internal const string ManifestSchema = "androidmcp-native-source-generation-v1";
    internal const string IndexSchema = "androidmcp-native-source-index-v1";
    private static SourceExtractException Failure(string code, string message, Exception? inner = null) => new("SOURCE_EXTRACT_" + code, message, inner);

    public async Task<ISourceExtractGeneration> OpenAsync(SourceExtractGenerationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request); cancellationToken.ThrowIfCancellationRequested();
        SourceExtractIndex? index = null;
        if (request.TrustedIndex is not null)
        {
            var manifest = await ReadPinnedJson<SourceExtractGenerationManifest>(request.TrustedIndex, cancellationToken);
            if (manifest.Schema != ManifestSchema || string.IsNullOrWhiteSpace(manifest.GenerationId) || manifest.IndexData is null || manifest.Outputs is null ||
                !manifest.Outputs.Contains(manifest.IndexData)) throw Failure("INDEX_SCHEMA", "Native generation commit does not own its index-data pin");
            index = await ReadPinnedJson<SourceExtractIndex>(manifest.IndexData, cancellationToken);
            ValidateIndex(request, index);
        }
        // Dependency/ABI admission on hits is permitted. Extract and raw reads
        // remain absent until a miss/noncache row is actually requested.
        var runtime = runtimes.Create(request.Profile, request.Java);
        try
        {
            if (index is not null && index.Rows.Count > 0)
            {
                if (runtime is not ISourcesCacheRuntime capabilities || index.Rows.Any(row => row.Kind is null || !capabilities.CacheProducerKinds.Contains(row.Kind)))
                    throw Failure("CACHE_CAPABILITY", "Committed cache row is not backed by an actual cache-producing runtime registration");
            }
            return new Generation(this, request, runtime, index?.Rows ?? []);
        }
        catch { runtime.Dispose(); throw; }
    }

    public async Task<SourceExtractRow> ResolveAsync(SourceExtractResolveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request); cancellationToken.ThrowIfCancellationRequested();
        var row = AdmittedRow(request.Projection, request.Row);
        using var runtime = runtimes.Create(request.Profile, request.Java);
        return await ResolveRaw(request.Projection, row, runtime, cancellationToken);
    }

    private async Task<T> ReadPinnedJson<T>(StoredArtifact pin, CancellationToken cancellationToken)
    {
        if (!ArtifactPaths.IsRelative(pin.RelativePath) || pin.Length < 0 || pin.MediaType != "application/json" || string.IsNullOrEmpty(pin.Sha256))
            throw Failure("INDEX_PIN", "Native generation JSON pin is invalid");
        var bytes = await generations.ReadAsync(pin.RelativePath, cancellationToken);
        if (bytes.Length != pin.Length || ContentHash.Sha256(bytes.Span) != pin.Sha256)
            throw Failure("INDEX_PIN", "Native generation bytes differ from the caller-admitted pin");
        try
        {
            // The shared canonical authority performs the sole strict JSON,
            // Unicode, finite-number and decoded duplicate-name inspection.
            var inspected = json.Serialize(bytes);
            return JsonSerializer.Deserialize<T>(inspected) ?? throw Failure("INDEX_SCHEMA", "Native generation JSON value is absent");
        }
        catch (Exception error) when (error is JsonException or CanonicalJsonException)
        { throw Failure("INDEX_JSON", "Native generation JSON is malformed or ambiguous", error); }
    }

    private void ValidateIndex(SourceExtractGenerationRequest request, SourceExtractIndex index)
    {
        if (index.Schema != IndexSchema || index.Rows is null || index.Producer is null)
            throw Failure("INDEX_SCHEMA", "Unsupported native source index");
        if (index.Release != request.Projection.Release || index.RecipeSha256 != request.Projection.RecipeSha256 || index.ProfileId != request.Profile.Id ||
            json.Hash(JsonSerializer.SerializeToElement(index.Producer)) != json.Hash(JsonSerializer.SerializeToElement(request.Producer)))
            throw Failure("INDEX_IDENTITY", "Native generation recipe, release, profile or producer identity differs");
        var rows = new HashSet<int>();
        foreach (var item in index.Rows)
        {
            if (!rows.Add(item.RowIndex)) throw Failure("ROW_IDENTITY", "Native index repeats a logical source row");
            var row = request.Projection.Rows.SingleOrDefault(row => row.RowIndex == item.RowIndex) ?? throw Failure("ROW_IDENTITY", "Native index row is absent from the admitted projection");
            var physical = Physical(request.Projection, row); var context = Context(request.Projection, row, physical.Pin);
            if (item.LogicalPath != row.LogicalPath || item.Kind != row.Kind || item.PhysicalSourceIndex != row.PhysicalSourceIndex || item.CacheAliasIndex != row.CacheAliasIndex ||
                item.SourceSha256 != physical.Pin.Sha256 || item.SourceLength < 0 || physical.Pin.SizeBytes is { } size && item.SourceLength != size ||
                item.OriginalRowCanonicalSha256 != json.Hash(row.OriginalRow) || item.Context != context || item.Scope != row.Context.Scope ||
                item.Issues is null || item.CacheReceipt is null || item.CacheSeal is null)
                throw Failure("ROW_IDENTITY", "Committed source row differs from its exact logical, physical or contextual identity");
            if (item.CacheReceipt.SourceSha256 != item.SourceSha256 || item.CacheReceipt.ProducerId != request.Producer.ProducerId ||
                item.CacheReceipt.ProfileId != request.Profile.Id || item.CacheReceipt.RecipeSha256 != request.Projection.RecipeSha256 ||
                item.CacheReceipt.Path != item.CacheSeal.Data.RelativePath || item.CacheReceipt.RecordCount != item.CacheSeal.RecordCount)
                throw Failure("ROW_IDENTITY", "Native row cache receipt differs from its admitted source identity");
            ValidateRawEvidence(item);
        }
    }

    private static void ValidateRawEvidence(SourceExtractIndexRow item)
    {
        var required = item.Issues.Count > 0 || item.StructuralStatus is StructuralParseStatus.Unsupported or StructuralParseStatus.Malformed;
        if (!required && item.RawSourceBase64 is null) return;
        byte[] raw;
        try { raw = Convert.FromBase64String(item.RawSourceBase64 ?? throw Failure("RAW_EVIDENCE", "Required complete raw source evidence is absent")); }
        catch (FormatException error) { throw Failure("RAW_EVIDENCE", "Required raw source evidence is not valid base64", error); }
        if (raw.LongLength != item.SourceLength || ContentHash.Sha256(raw) != item.SourceSha256)
            throw Failure("RAW_EVIDENCE", "Sealed raw source evidence differs from its complete original source bytes");
    }

    private static RehydrationSourceRow AdmittedRow(RehydrationSourceProjection projection, RehydrationSourceRow requested)
    {
        var row = projection.Rows.SingleOrDefault(row => row.RowIndex == requested.RowIndex);
        if (row is null || row.LogicalPath != requested.LogicalPath || row.Kind != requested.Kind || row.PhysicalSourceIndex != requested.PhysicalSourceIndex || row.CacheAliasIndex != requested.CacheAliasIndex ||
            row.Context != requested.Context || row.OriginalRow.GetRawText() != requested.OriginalRow.GetRawText())
            throw Failure("ROW_IDENTITY", "Requested source row is not the admitted projection row");
        return row;
    }
    private static RehydrationPhysicalSource Physical(RehydrationSourceProjection projection, RehydrationSourceRow row) =>
        projection.PhysicalSources.SingleOrDefault(physical => physical.PhysicalSourceIndex == row.PhysicalSourceIndex) ?? throw Failure("ROW_IDENTITY", "Physical source pin is absent");
    private static SourceContext Context(RehydrationSourceProjection projection, RehydrationSourceRow row, SourcePin pin)
    {
        string? platform = null;
        if (row.OriginalRow.TryGetProperty("platform_api_version", out var value) && value.ValueKind != JsonValueKind.Null)
            platform = value.ValueKind switch { JsonValueKind.String => value.GetString(), JsonValueKind.Number => value.GetRawText(), _ => throw Failure("ROW_IDENTITY", "Explicit platform version must be text, number or null") };
        return new(row.Context.Repository ?? "", row.Context.Revision ?? pin.Revision, projection.RecipeSha256, platform);
    }

    private async Task<SourceExtractRow> ResolveRaw(RehydrationSourceProjection projection, RehydrationSourceRow row, ISourcesRuntime runtime, CancellationToken cancellationToken)
    {
        var physical = Physical(projection, row); var bytes = await rawSources.ReadAsync(physical.Pin.LocalPath, cancellationToken);
        if (ContentHash.Sha256(bytes.Span) != physical.Pin.Sha256 || physical.Pin.SizeBytes is { } length && bytes.Length != length ||
            physical.Pin.GitBlob is { } git && ContentHash.GitBlobSha1Legacy(bytes.Span) != git)
            throw Failure("RAW_PIN", "Raw source differs from its immutable acquisition pins");
        var context = Context(projection, row, physical.Pin);
        var result = runtime.Dispatcher.Extract(new(row.LogicalPath, bytes.ToArray(), context), row.Kind, row.Context.Scope);
        var records = json.Serialize(JsonSerializer.SerializeToElement(result.Records));
        var retainRaw = result.Issues.Count > 0 || result.StructuralStatus is StructuralParseStatus.Unsupported or StructuralParseStatus.Malformed;
        return new(row.RowIndex, bytes.Length, context, records, result.StructuralStatus, result.Issues.ToArray(), Clone(result.Metadata),
            retainRaw ? Convert.ToBase64String(bytes.Span) : null, null, null, false);
    }
    private static JsonObject? Clone(JsonObject? value) => value is null ? null : (JsonObject)value.DeepClone();

    private sealed class Generation(SourcesExtractService service, SourceExtractGenerationRequest request, ISourcesRuntime runtime,
        IReadOnlyList<SourceExtractIndexRow> admitted) : ISourceExtractGeneration
    {
        private readonly Dictionary<int, SourceExtractIndexRow> indexed = admitted.ToDictionary(row => row.RowIndex);
        private readonly Dictionary<string, SourceCacheLookup> lookups = new(StringComparer.Ordinal);
        private bool disposed;
        public async Task<SourceExtractRow> ReadAsync(RehydrationSourceRow requested, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this); cancellationToken.ThrowIfCancellationRequested();
            var row = AdmittedRow(request.Projection, requested);
            if (!indexed.TryGetValue(row.RowIndex, out var item)) return await service.ResolveRaw(request.Projection, row, runtime, cancellationToken);
            var identity = new SourceCacheRequest(item.CacheReceipt.Path, item.Kind!, request.Producer.ProducerId, request.Profile.Id, request.Projection.RecipeSha256,
                item.SourceSha256, ReadOnlyMemory<byte>.Empty, item.CacheReceipt.HistoricalGZipSha256, ProducerFiles: request.Producer.ProducerFiles, RuntimeIdentity: request.Producer.RuntimeIdentity);
            var key = service.json.Hash(JsonSerializer.SerializeToElement(new { identity.Path, identity.Kind, identity.ProducerId, identity.ProfileId, identity.RecipeSha256, identity.SourceSha256,
                identity.ProducerFiles, identity.RuntimeIdentity, identity.HistoricalGZipSha256, item.CacheSeal }));
            if (!lookups.TryGetValue(key, out var lookup))
            {
                lookup = await service.sealedCache.LookupAsync(identity, item.CacheSeal, cancellationToken);
                if (lookup.Receipt != item.CacheReceipt || lookup.Seal != item.CacheSeal)
                    throw Failure("ROW_IDENTITY", "Sealed cache provider returned a different admitted identity");
                lookups.Add(key, lookup);
            }
            // No declaration-payload JSON parse and no source read on a hit.
            return new(row.RowIndex, item.SourceLength, item.Context, lookup.RecordsJson.ToArray(), item.StructuralStatus, item.Issues.ToArray(), Clone(item.Metadata),
                item.RawSourceBase64, lookup.Receipt, lookup.Seal, true);
        }
        public void Dispose() { if (disposed) return; disposed = true; runtime.Dispose(); }
    }
}
