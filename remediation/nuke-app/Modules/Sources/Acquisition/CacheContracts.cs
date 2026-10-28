using AndroidMcp.BuildTool.Core;
namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;
public sealed record SourceCacheRequest(string Path,string Kind,string ProducerId,string ProfileId,string RecipeSha256,
    string SourceSha256,ReadOnlyMemory<byte> RecordsJson,string? HistoricalGZipSha256=null,bool StrictHistoricalBytes=false,
    IReadOnlyDictionary<string,string>? ProducerFiles=null,string RuntimeIdentity="net10");
public sealed record SourceCacheReceipt(string Path,string ProducerId,string ProfileId,string RecipeSha256,string SourceSha256,
    string ProducerFingerprint,string GZipSha256,string? HistoricalGZipSha256,int RecordCount,bool DerivedGeneration);
public interface ISourceCache
{
    Task<SourceCacheReceipt> WriteAsync(SourceCacheRequest request,CancellationToken cancellationToken=default);
    Task<ReadOnlyMemory<byte>> ReadAsync(SourceCacheRequest request,CancellationToken cancellationToken=default);
}
