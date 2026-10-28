using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;

public sealed class SourceCache(IArtifactStore store,IArtifactLeaseProvider leases,ICanonicalJson canonical):ISourceCache
{
    public async Task<SourceCacheReceipt> WriteAsync(SourceCacheRequest request,CancellationToken cancellationToken=default)
    {
        Validate(request);await using var lease=await leases.AcquireAsync("source-cache/"+request.Path,cancellationToken);
        var records=canonical.Serialize(request.RecordsJson);using var parsed=JsonDocument.Parse(records);
        if(parsed.RootElement.ValueKind!=JsonValueKind.Array)throw new TransportException("CACHE_RECORDS","Declaration cache must be a JSON array.");
        var count=parsed.RootElement.GetArrayLength();var fingerprint=Fingerprint(request);
        using var lines=new MemoryStream();
        foreach(var record in parsed.RootElement.EnumerateArray()){lines.Write(canonical.Serialize(record));lines.WriteByte((byte)'\n');}
        using var output=new MemoryStream();using(var gzip=new GZipStream(output,CompressionLevel.Optimal,true))gzip.Write(lines.ToArray());
        var compressed=output.ToArray();var gzipHash=ContentHash.Sha256(compressed);
        var differs=request.HistoricalGZipSha256 is not null&&request.HistoricalGZipSha256!=gzipHash;
        if(request.StrictHistoricalBytes&&differs)throw new TransportException("CACHE_BYTES","Historical gzip bytes differ; no cache published.");
        var receipt=new SourceCacheReceipt(request.Path,request.ProducerId,request.ProfileId,request.RecipeSha256,request.SourceSha256,fingerprint,gzipHash,request.HistoricalGZipSha256,count,differs);
        var sidecar=request.Path+".csharp-cache.json";
        ReadOnlyMemory<byte>? existingReceipt=null;
        try{existingReceipt=await store.ReadAsync(sidecar,cancellationToken);}
        catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
        if(existingReceipt is not null)
        {
            var old=JsonSerializer.Deserialize<SourceCacheReceipt>(existingReceipt.Value.Span)??throw new TransportException("CACHE_RECEIPT","Cache receipt missing.");
            if(old!=receipt)throw new TransportException("CACHE_IDENTITY","Cache pins changed; use a new generation.");
            _=await ReadAsync(request,cancellationToken);return old;
        }
        // A previous crash may leave the immutable data artifact; WriteAsync only accepts identical bytes.
        await store.WriteAsync(new(request.Path,compressed,"application/gzip"),cancellationToken);
        await store.WriteAsync(new(sidecar,canonical.Serialize(JsonSerializer.SerializeToElement(receipt)),"application/json"),cancellationToken);
        return receipt;
    }
    public async Task<ReadOnlyMemory<byte>> ReadAsync(SourceCacheRequest request,CancellationToken cancellationToken=default)
    {
        Validate(request);var sidecar=await store.ReadAsync(request.Path+".csharp-cache.json",cancellationToken);
        var receipt=JsonSerializer.Deserialize<SourceCacheReceipt>(sidecar.Span)??throw new TransportException("CACHE_RECEIPT","Cache receipt missing.");
        if(receipt.Path!=request.Path||receipt.ProducerId!=request.ProducerId||receipt.ProfileId!=request.ProfileId||receipt.RecipeSha256!=request.RecipeSha256||receipt.SourceSha256!=request.SourceSha256||receipt.ProducerFingerprint!=Fingerprint(request)||receipt.HistoricalGZipSha256!=request.HistoricalGZipSha256)throw new TransportException("CACHE_IDENTITY","Cache pins differ.");
        var compressed=await store.ReadAsync(request.Path,cancellationToken);if(ContentHash.Sha256(compressed.Span)!=receipt.GZipSha256)throw new TransportException("CACHE_HASH","Cache bytes changed.");
        if(request.StrictHistoricalBytes&&receipt.GZipSha256!=request.HistoricalGZipSha256&&request.HistoricalGZipSha256 is not null)throw new TransportException("CACHE_BYTES","Historical gzip bytes differ.");
        var (bytes,count)=await ReadRecordsAsync(compressed,canonical,cancellationToken);
        if(count!=receipt.RecordCount||!canonical.Serialize(bytes).AsSpan().SequenceEqual(canonical.Serialize(request.RecordsJson)))throw new TransportException("CACHE_RECORDS","Cache records/count changed.");return bytes;
    }
    internal static async Task<(byte[] Bytes,int Count)> ReadRecordsAsync(ReadOnlyMemory<byte> compressed,ICanonicalJson canonical,CancellationToken cancellationToken)
    {
        using var input=new MemoryStream(compressed.ToArray(),false);using var gzip=new GZipStream(input,CompressionMode.Decompress);using var output=new MemoryStream();
        var buffer=new byte[64*1024];while(true){var n=await gzip.ReadAsync(buffer,cancellationToken);if(n==0)break;if(output.Length+n>64*1024*1024)throw new TransportException("CACHE_LIMIT","Expanded cache exceeds64MiB.");await output.WriteAsync(buffer.AsMemory(0,n),cancellationToken);}
        var payload=output.ToArray();using var array=new MemoryStream();array.WriteByte((byte)'[');var start=0;var count=0;
        for(var position=0;position<payload.Length;position++)
        {
            if(payload[position]!='\n')continue;
            if(position==start)throw new TransportException("CACHE_RECORDS","Empty JSONL cache line.");
            var record=canonical.Serialize(payload.AsMemory(start,position-start));
            if(count++>0)array.WriteByte((byte)',');array.Write(record);start=position+1;
        }
        if(start!=payload.Length)throw new TransportException("CACHE_RECORDS","JSONL cache requires a final newline.");array.WriteByte((byte)']');
        return(array.ToArray(),count);
    }
    private string Fingerprint(SourceCacheRequest request)=>canonical.Hash(JsonSerializer.SerializeToElement(new{cache_contract=3,language="csharp",runtime=request.RuntimeIdentity,kind=request.Kind,producer=request.ProducerId,profile=request.ProfileId,code=request.ProducerFiles}));
    private static void Validate(SourceCacheRequest request)
    {
        _=ArtifactPaths.Segments(request.Path);
        if(request.ProducerFiles is null||request.ProducerFiles.Count==0||request.ProducerFiles.Any(x=>!ArtifactPaths.IsRelative(x.Key)||!Regex.IsMatch(x.Value,"^[a-f0-9]{64}$"))||string.IsNullOrWhiteSpace(request.RuntimeIdentity))throw new TransportException("CACHE_IDENTITY","Pinned C# producer files and runtime identity required.");
        if(!request.ProducerId.StartsWith("csharp-",StringComparison.Ordinal)||string.IsNullOrWhiteSpace(request.Kind)||string.IsNullOrWhiteSpace(request.ProfileId)||!Regex.IsMatch(request.RecipeSha256,"^[a-f0-9]{64}$")||!Regex.IsMatch(request.SourceSha256,"^[a-f0-9]{64}$")||request.HistoricalGZipSha256 is not null&&!Regex.IsMatch(request.HistoricalGZipSha256,"^[a-f0-9]{64}$"))throw new TransportException("CACHE_IDENTITY","Explicit C# producer and complete pins required.");
    }
}
