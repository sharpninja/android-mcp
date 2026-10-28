using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;

/// <summary>Pins admitted from a trusted native generation index, never inferred from editable cache files.</summary>
public sealed record SourceCacheSeal(StoredArtifact Data,StoredArtifact Sidecar,int RecordCount);

/// <summary>Verified canonical declaration array and its original native producer receipt.</summary>
public sealed record SourceCacheLookup(ReadOnlyMemory<byte> RecordsJson,SourceCacheReceipt Receipt,SourceCacheSeal Seal);

/// <summary>Both immutable artifacts required to seal this publication in the caller's generation index.</summary>
public sealed record SourceCachePublication(SourceCacheReceipt Receipt,SourceCacheSeal Seal);

/// <summary>Additive native cache admission. Existing ReadAsync and WriteAsync retain their contracts.</summary>
public interface ISealedSourceCache:ISourceCache
{
    // RecordsJson in identity is deliberately ignored. No extractor is needed to validate a sealed hit.
    // Missing or invalid seals fail closed. The caller may choose raw extraction in a fresh generation.
    Task<SourceCacheLookup> LookupAsync(SourceCacheRequest identity,SourceCacheSeal trustedSeal,CancellationToken cancellationToken=default);
    // Uses the same write/codec implementation as WriteAsync. Data is published before its sidecar.
    Task<SourceCachePublication> PublishAsync(SourceCacheRequest request,CancellationToken cancellationToken=default);
}
