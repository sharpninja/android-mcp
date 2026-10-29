namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;
public sealed record HistoricalCachePin(string Path,string Kind,string GZipSha256,string ProducerFingerprint,int RecordCount,string SourceSha256,string RecipeSha256);
public sealed record HistoricalCacheResult(HistoricalCachePin Pin,ReadOnlyMemory<byte> RecordsJson,bool HistoricalProducer);
public interface IHistoricalSourceCache
{
    Task<HistoricalCacheResult> ReadAsync(HistoricalCachePin pin,CancellationToken cancellationToken=default);
}
