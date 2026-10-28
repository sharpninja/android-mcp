using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;

namespace AndroidMcp.BuildTool.Sources;

public sealed class SourceExtractException(string code, string message, Exception? inner = null) : IOException(message, inner)
{ public string Code { get; } = code; }

// Native producer identity is selected by application composition, not accepted
// from an editable historical receipt or silently renamed Python fingerprint.
public sealed record SourceExtractProducerIdentity(string ProducerId,
    IReadOnlyDictionary<string, string> ProducerFiles, string RuntimeIdentity,
    string ParserIdentity, string ContextIdentity);

// CacheSourceLimit counts selected logical cache-producing rows. It is not
// Acquisition's physical-source limit or a limit over deduplicated aliases.
public sealed record SourceExtractRequest(RehydrationSourceProjection Projection,
    string GenerationRoot, SourceExtractionProfile Profile, SourceExtractProducerIdentity Producer,
    StoredArtifact? TrustedPriorIndex = null, SourceJavaAdmission? Java = null, int? CacheSourceLimit = null);

// Cache capability belongs to the same runtime registration descriptors that
// create its providers. No independent static kind/alias table is introduced.
public interface ISourcesCacheRuntime : ISourcesRuntime
{
    IReadOnlySet<string> CacheProducerKinds { get; }
}

// RecordsJson is the canonical declaration ARRAY supplied by the sole sealed
// cache SPI. The facade does not decode it on hits. A Catalog importer may
// deserialize those declaration bytes once into its existing DB ingestion.
// RawSourceBase64 is retained only where diagnostics/unsupported facts require
// whole-source evidence. It permits Catalog issue retention without raw reread.
public sealed record SourceExtractRow(int RowIndex, long SourceLength, SourceContext Context,
    ReadOnlyMemory<byte> RecordsJson, StructuralParseStatus StructuralStatus,
    IReadOnlyList<SourceIssue> Issues, JsonObject? Metadata, string? RawSourceBase64,
    SourceCacheReceipt? CacheReceipt, SourceCacheSeal? CacheSeal, bool CacheHit);

public sealed record SourceExtractResult(StoredArtifact Index,
    IReadOnlyList<SourceExtractRow> Rows, int SelectedLogicalCacheRows,
    int UniqueCacheAliases, int CacheHits, int RawExtractions);

// Native generation commit points to one domain index-data artifact. The
// shared Core generation runtime writes that commit last. Cache data/sidecars
// remain ancillary immutable publications until this commit is admitted.
public sealed record SourceExtractGenerationManifest(string Schema, string GenerationId,
    IReadOnlyList<StoredArtifact> Inputs, IReadOnlyList<StoredArtifact> Outputs, StoredArtifact IndexData);
public sealed record SourceExtractIndex(string Schema, string Release, string RecipeSha256,
    string ProfileId, SourceExtractProducerIdentity Producer, IReadOnlyList<SourceExtractIndexRow> Rows);
public sealed record SourceExtractIndexRow(int RowIndex, string LogicalPath, string? Kind,
    int PhysicalSourceIndex, int? CacheAliasIndex, string SourceSha256, long SourceLength,
    string OriginalRowCanonicalSha256, SourceContext Context, string? Scope,
    StructuralParseStatus StructuralStatus, IReadOnlyList<SourceIssue> Issues, JsonObject? Metadata,
    string? RawSourceBase64, SourceCacheReceipt CacheReceipt, SourceCacheSeal CacheSeal);

public interface ISourcesExtractOperation
{
    Task<SourceExtractResult> ExtractAsync(SourceExtractRequest request, CancellationToken cancellationToken = default);
}

// Single raw miss/noncache resolver shared by Extract and Catalog intake.
public sealed record SourceExtractResolveRequest(RehydrationSourceProjection Projection,
    RehydrationSourceRow Row, SourceExtractionProfile Profile, SourceExtractProducerIdentity Producer,
    SourceJavaAdmission? Java = null);
public interface ISourceExtractResolver
{
    Task<SourceExtractRow> ResolveAsync(SourceExtractResolveRequest request, CancellationToken cancellationToken = default);
}

// Both CLI consumers use this single Source-owned index admission/lookup path.
// The caller pins the index BEFORE it is read. The existing projection is the
// sole source-row parser, with its physical pins and exact OriginalRow context.
public sealed record SourceExtractGenerationRequest(RehydrationSourceProjection Projection,
    SourceExtractionProfile Profile, SourceExtractProducerIdentity Producer, StoredArtifact? TrustedIndex = null,
    SourceJavaAdmission? Java = null);
public interface ISourceExtractGenerationReader
{
    Task<ISourceExtractGeneration> OpenAsync(SourceExtractGenerationRequest request, CancellationToken cancellationToken = default);
}
public interface ISourceExtractGeneration : IDisposable
{
    Task<SourceExtractRow> ReadAsync(RehydrationSourceRow row, CancellationToken cancellationToken = default);
}
