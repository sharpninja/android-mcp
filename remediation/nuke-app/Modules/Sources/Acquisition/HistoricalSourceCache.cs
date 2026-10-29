using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;
namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;

/// <summary>Read locked historical inputs as historical data; never invent a C# producer identity.</summary>
public sealed class HistoricalSourceCache(IArtifactStore store,ICanonicalJson canonical):IHistoricalSourceCache
{
    public async Task<HistoricalCacheResult> ReadAsync(HistoricalCachePin pin,CancellationToken cancellationToken=default)
    {
        _=ArtifactPaths.Segments(pin.Path);
        if(pin.Kind is not ("java-source" or "sdk-api-index"))throw new TransportException("CACHE_KIND","No historical producer contract for this cache kind.");
        if(pin.RecordCount<0||new[]{pin.GZipSha256,pin.ProducerFingerprint,pin.SourceSha256,pin.RecipeSha256}.Any(x=>!Regex.IsMatch(x,"^[a-f0-9]{64}$")))throw new TransportException("CACHE_IDENTITY","Complete locked historical cache pins required.");
        var compressed=await store.ReadAsync(pin.Path,cancellationToken);
        if(ContentHash.Sha256(compressed.Span)!=pin.GZipSha256)throw new TransportException("CACHE_HASH","Historical cache compressed hash differs.");
        var (records,count)=await SourceCache.ReadRecordsAsync(compressed,canonical,cancellationToken);
        if(count!=pin.RecordCount)throw new TransportException("CACHE_RECORDS","Historical cache record count differs.");
        return new(pin,records,true);
    }
}
