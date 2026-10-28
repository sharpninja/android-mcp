using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;

namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;

/// <summary>Acquire Publication's selected physical pins, retaining all logical audit rows.</summary>
public sealed class SourceBatch(IByteTransport transport,IArtifactStore store,IArtifactStore bundle,
    IArtifactLeaseProvider leases,ICanonicalJson canonical):ISourceBatch
{
    public async Task<SourceBatchResult> RunAsync(SourceBatchRequest request,CancellationToken cancellationToken=default)
    {
        Validate(request);
        var projection=request.Projection;
        var identity=canonical.Hash(JsonSerializer.SerializeToElement(request));
        await using var lease=await leases.AcquireAsync("source-batch/"+request.ReceiptPath,cancellationToken);
        var sealedBytes=await Optional(store,request.ReceiptPath,cancellationToken);
        if(sealedBytes is not null)
        {
            if(!projection.Resume)Fail("BATCH_RESUME");
            SourceBatchReceipt receipt;
            try
            {
                var normalized=canonical.Serialize(sealedBytes.Value);
                if(!normalized.AsSpan().SequenceEqual(sealedBytes.Value.Span))Fail("BATCH_SEALED");
                receipt=JsonSerializer.Deserialize<SourceBatchReceipt>(normalized)??throw new JsonException();
            }
            catch(Exception e) when(e is JsonException or CanonicalJsonException or NotSupportedException){throw new TransportException("BATCH_SEALED","Source batch receipt is malformed.");}
            if(receipt.GenerationId!=request.GenerationId||receipt.RequestSha256!=identity)Fail("BATCH_IDENTITY");
            VerifyReceiptShape(request,receipt);
            await VerifyOutputs(receipt.Outputs,cancellationToken);
            foreach(var source in receipt.Sources)
                SourceAcquirer.Verify((await store.ReadAsync(source.Source.Artifact.RelativePath,cancellationToken)).Span,
                    projection.PhysicalSources.Single(x=>x.PhysicalSourceIndex==source.PhysicalSourceIndex).Pin);
            return new(receipt,new(request.ReceiptPath,ContentHash.Sha256(sealedBytes.Value.Span),sealedBytes.Value.Length,"application/json"),true);
        }

        var selected=projection.PhysicalSources.Where(x=>projection.SelectedPhysicalSourceIndices.Contains(x.PhysicalSourceIndex)).ToArray();
        var copies=new Dictionary<string,Artifact>(StringComparer.Ordinal);
        // Read and verify all existing raw targets and declared bundle prerequisites
        // before a newly missing source can trigger HTTP or an output write.
        foreach(var item in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pin=item.Pin;SourceAcquirer.ValidatePin(pin);
            var existing=await Optional(store,pin.LocalPath,cancellationToken);
            if(existing is not null)
            {
                SourceAcquirer.Verify(existing.Value.Span,pin);
                if(!projection.Resume)Fail("BATCH_RESUME");
                continue;
            }
            if(item.Prerequisites.Any(x=>x.Code!=RehydrationPrerequisiteCode.RequiredImmutableSnapshot))Fail("BATCH_PREREQUISITE");
            if(item.RequiredSnapshots.Count>0)
            {
                foreach(var snapshot in item.RequiredSnapshots)
                {
                    if(snapshot.LocalPath!=pin.LocalPath||snapshot.Sha256!=pin.Sha256||snapshot.Length is not null&&pin.SizeBytes is not null&&snapshot.Length!=pin.SizeBytes)Fail("BATCH_PROJECTION");
                    if(snapshot.BundlePath is null)Fail("BATCH_PREREQUISITE");
                    var bytes=await Optional(bundle,snapshot.BundlePath!,cancellationToken);
                    if(bytes is null)Fail("BATCH_PREREQUISITE");
                    SourceAcquirer.Verify(bytes!.Value.Span,pin);
                    if(snapshot.Length is not null&&bytes.Value.Length!=snapshot.Length)Fail("SOURCE_SIZE");
                    var path=pin.RequiredLocalSnapshot??pin.LocalPath;
                    await PrepareCopy(new(path,bytes.Value,"application/octet-stream"),copies,cancellationToken);
                }
            }
            else if(item.Prerequisites.Count>0)Fail("BATCH_PREREQUISITE");
            if(pin.RequiredLocalSnapshot is not null&&!copies.ContainsKey(pin.RequiredLocalSnapshot))
            {
                var snapshot=await Optional(store,pin.RequiredLocalSnapshot,cancellationToken);
                if(snapshot is null)Fail("BATCH_PREREQUISITE");
                SourceAcquirer.Verify(snapshot!.Value.Span,pin);
            }
        }
        var historical=new List<(RehydrationCacheAlias Alias,Artifact Copy)>();
        if(request.CachePolicy==SourceBatchCachePolicy.ImportLockedHistorical)
        {
            var selectedRows=SelectedRows(projection);
            foreach(var alias in projection.CacheAliases.Where(x=>x.SelectionRowIndices.Any(selectedRows.Contains)))
            {
                if(alias.Pin is null||alias.Prerequisites.Count>0)Fail("BATCH_PREREQUISITE");
                var pin=alias.Pin!;
                var existing=await Optional(store,alias.CachePath,cancellationToken);
                var input=existing is not null?store:bundle;
                if(existing is null&&await Optional(bundle,alias.CachePath,cancellationToken) is null)Fail("BATCH_PREREQUISITE");
                _=await new HistoricalSourceCache(input,canonical).ReadAsync(pin,cancellationToken);
                var bytes=await input.ReadAsync(alias.CachePath,cancellationToken);
                var copy=new Artifact(alias.CachePath,bytes,"application/gzip");
                await PrepareCopy(copy,copies,cancellationToken);
                historical.Add((alias,copy));
            }
        }
        var outputs=new List<StoredArtifact>();
        // Snapshot dependencies precede raw acquisition; historical artifacts follow it.
        foreach(var copy in copies.Values.Where(x=>historical.All(h=>h.Copy.RelativePath!=x.RelativePath)))
            Add(outputs,await WriteMissing(copy,cancellationToken));
        var recording=new RecordingStore(store,outputs);
        var sources=new List<SourceBatchSource>();
        foreach(var item in selected)
        {
            var acquired=await new SourceAcquirer(transport,recording).AcquireAsync(item.Pin,cancellationToken);
            Add(outputs,acquired.Artifact);sources.Add(new(item.PhysicalSourceIndex,acquired));
        }
        var imported=new List<SourceBatchHistoricalCache>();
        foreach(var item in historical)
        {
            var artifact=await WriteMissing(item.Copy,cancellationToken);Add(outputs,artifact);
            imported.Add(new(item.Alias.CacheAliasIndex,item.Alias.Pin!,artifact));
        }
        var rowCount=SelectedRows(projection).Count;
        var result=new SourceBatchReceipt(request.GenerationId,identity,projection,request.CachePolicy,projection.Rows.Count,rowCount,rowCount,
            projection.PhysicalSources.Count,selected.Length,selected.Length==projection.PhysicalSources.Count,false,sources,imported,outputs);
        VerifyReceiptShape(request,result);
        await VerifyOutputs(outputs,cancellationToken);
        var manifest=await store.WriteAsync(new(request.ReceiptPath,canonical.Serialize(JsonSerializer.SerializeToElement(result)),"application/json"),cancellationToken);
        return new(result,manifest,false);
    }

    private static HashSet<int> SelectedRows(RehydrationSourceProjection projection)=>projection.Rows.Where(x=>projection.SelectedPhysicalSourceIndices.Contains(x.PhysicalSourceIndex)).Select(x=>x.RowIndex).ToHashSet();
    private static void Validate(SourceBatchRequest request)
    {
        if(string.IsNullOrWhiteSpace(request.GenerationId)||!ArtifactPaths.IsRelative(request.ReceiptPath))Fail("BATCH_PROJECTION");
        var p=request.Projection;
        if(!Regex.IsMatch(p.RecipeSha256,"^[a-f0-9]{64}$")||string.IsNullOrWhiteSpace(p.Release)||!Enum.IsDefined(p.Profile)||!Enum.IsDefined(request.CachePolicy))Fail("BATCH_PROJECTION");
        if(p.Rows.Select(x=>x.RowIndex).Distinct().Count()!=p.Rows.Count||p.PhysicalSources.Select(x=>x.PhysicalSourceIndex).Distinct().Count()!=p.PhysicalSources.Count||
            p.CacheAliases.Select(x=>x.CacheAliasIndex).Distinct().Count()!=p.CacheAliases.Count||p.SelectedPhysicalSourceIndices.Distinct().Count()!=p.SelectedPhysicalSourceIndices.Count)Fail("BATCH_PROJECTION");
        var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase){request.ReceiptPath};
        foreach(var physical in p.PhysicalSources)
        {
            SourceAcquirer.ValidatePin(physical.Pin);
            if(!paths.Add(physical.Pin.LocalPath)||physical.SelectionRowIndices.Count==0||!physical.SelectionRowIndices.Order().SequenceEqual(p.Rows.Where(x=>x.PhysicalSourceIndex==physical.PhysicalSourceIndex).Select(x=>x.RowIndex).Order()))Fail("BATCH_PROJECTION");
            if(physical.Pin.Compatibility!=p.Profile||physical.Pin.Archive is not null&&physical.Pin.Archive.Compatibility!=p.Profile)Fail("BATCH_PROFILE");
            foreach(var snapshot in physical.RequiredSnapshots){if(!ArtifactPaths.IsRelative(snapshot.LocalPath)||snapshot.BundlePath is not null&&!ArtifactPaths.IsRelative(snapshot.BundlePath))Fail("BATCH_PROJECTION");}
        }
        if(p.SelectedPhysicalSourceIndices.Any(i=>p.PhysicalSources.All(x=>x.PhysicalSourceIndex!=i))||p.Rows.Any(row=>p.PhysicalSources.All(x=>x.PhysicalSourceIndex!=row.PhysicalSourceIndex)))Fail("BATCH_PROJECTION");
        foreach(var alias in p.CacheAliases)
        {
            if(!ArtifactPaths.IsRelative(alias.CachePath)||!paths.Add(alias.CachePath)||!alias.SelectionRowIndices.Order().SequenceEqual(p.Rows.Where(x=>x.CacheAliasIndex==alias.CacheAliasIndex).Select(x=>x.RowIndex).Order()))Fail("BATCH_PROJECTION");
            if(alias.Pin is not null&&(alias.Pin.Path!=alias.CachePath||alias.Pin.RecipeSha256!=p.RecipeSha256||p.Rows.Where(x=>x.CacheAliasIndex==alias.CacheAliasIndex).Any(row=>p.PhysicalSources.Single(s=>s.PhysicalSourceIndex==row.PhysicalSourceIndex).Pin.Sha256!=alias.Pin.SourceSha256)))Fail("BATCH_PROJECTION");
        }
        if(p.Rows.Any(x=>x.CacheAliasIndex is not null&&p.CacheAliases.All(a=>a.CacheAliasIndex!=x.CacheAliasIndex)))Fail("BATCH_PROJECTION");
        foreach(var path in paths)if(paths.Any(other=>other.Length>path.Length&&other.StartsWith(path+"/",StringComparison.OrdinalIgnoreCase)))Fail("BATCH_PROJECTION");
    }
    private void VerifyReceiptShape(SourceBatchRequest request,SourceBatchReceipt receipt)
    {
        var p=request.Projection;var selected=p.PhysicalSources.Where(x=>p.SelectedPhysicalSourceIndices.Contains(x.PhysicalSourceIndex)).ToArray();var rows=SelectedRows(p).Count;
        if(!canonical.Serialize(JsonSerializer.SerializeToElement(p)).AsSpan().SequenceEqual(canonical.Serialize(JsonSerializer.SerializeToElement(receipt.Projection)))||
            receipt.CachePolicy!=request.CachePolicy||receipt.TotalRows!=p.Rows.Count||receipt.SelectedRows!=rows||receipt.ProcessedRows!=rows||receipt.TotalPhysicalSources!=p.PhysicalSources.Count||receipt.SelectedPhysicalSources!=selected.Length||receipt.Complete!=(selected.Length==p.PhysicalSources.Count)||receipt.HistoricalByteIdentityClaimed||receipt.Sources.Count!=selected.Length)Fail("BATCH_SEALED");
        foreach(var pair in receipt.Sources.Zip(selected))
        {
            var source=pair.First.Source;var pin=pair.Second.Pin;
            if(pair.First.PhysicalSourceIndex!=pair.Second.PhysicalSourceIndex||source.Artifact.RelativePath!=pin.LocalPath||source.Artifact.Sha256!=pin.Sha256||source.Artifact.MediaType!="application/octet-stream"||pin.SizeBytes is not null&&source.Artifact.Length!=pin.SizeBytes||source.Url!=pin.Url||source.Revision!=pin.Revision||source.GitBlob!=pin.GitBlob||source.Status is not ("reused" or "downloaded")||!receipt.Outputs.Contains(source.Artifact))Fail("BATCH_SEALED");
        }
        var expectedCaches=request.CachePolicy==SourceBatchCachePolicy.ImportLockedHistorical?p.CacheAliases.Where(x=>x.SelectionRowIndices.Any(SelectedRows(p).Contains)).ToArray():[];
        if(receipt.HistoricalCaches.Count!=expectedCaches.Length)Fail("BATCH_SEALED");
        foreach(var pair in receipt.HistoricalCaches.Zip(expectedCaches))if(pair.First.CacheAliasIndex!=pair.Second.CacheAliasIndex||pair.First.Pin!=pair.Second.Pin||pair.First.Artifact.RelativePath!=pair.Second.CachePath||pair.First.Artifact.Sha256!=pair.First.Pin.GZipSha256||pair.First.Artifact.MediaType!="application/gzip"||!receipt.Outputs.Contains(pair.First.Artifact))Fail("BATCH_SEALED");
        if(receipt.Outputs.Select(x=>x.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=receipt.Outputs.Count)Fail("BATCH_SEALED");
        var admitted=new Dictionary<string,(string Sha,string Media)>(StringComparer.Ordinal);
        void Admit(string path,string sha,string media)
        {if(path==request.ReceiptPath||admitted.TryGetValue(path,out var old)&&old!=(sha,media))Fail("BATCH_SEALED");admitted[path]=(sha,media);}
        foreach(var source in selected)
        {
            Admit(source.Pin.LocalPath,source.Pin.Sha256,"application/octet-stream");
            if(source.Pin.RequiredLocalSnapshot is not null)Admit(source.Pin.RequiredLocalSnapshot,source.Pin.Sha256,"application/octet-stream");
            if(source.Pin.Archive?.ArchiveSha256 is not null)
                Admit(source.Pin.Archive.ArchiveLocalPath??"archives/"+source.Pin.Archive.ArchiveSha256+".zip",source.Pin.Archive.ArchiveSha256,"application/zip");
        }
        foreach(var cache in expectedCaches)if(cache.Pin is not null)Admit(cache.CachePath,cache.Pin.GZipSha256,"application/gzip");
        foreach(var output in receipt.Outputs)if(!admitted.TryGetValue(output.RelativePath,out var bound)||output.Sha256!=bound.Sha||output.MediaType!=bound.Media)Fail("BATCH_SEALED");
    }
    private async Task PrepareCopy(Artifact artifact,Dictionary<string,Artifact> copies,CancellationToken ct)
    {
        _=ArtifactPaths.Segments(artifact.RelativePath);
        if(copies.TryGetValue(artifact.RelativePath,out var previous)&&!previous.Bytes.Span.SequenceEqual(artifact.Bytes.Span))Fail("BATCH_PROJECTION");
        var existing=await Optional(store,artifact.RelativePath,ct);
        if(existing is not null&&!existing.Value.Span.SequenceEqual(artifact.Bytes.Span))Fail("SOURCE_HASH");
        copies[artifact.RelativePath]=artifact;
    }
    private async Task<StoredArtifact> WriteMissing(Artifact artifact,CancellationToken ct)
    {
        var existing=await Optional(store,artifact.RelativePath,ct);
        if(existing is not null)
        {
            if(!existing.Value.Span.SequenceEqual(artifact.Bytes.Span))Fail("SOURCE_HASH");
            return new(artifact.RelativePath,ContentHash.Sha256(existing.Value.Span),existing.Value.Length,artifact.MediaType);
        }
        return await store.WriteAsync(artifact,ct);
    }
    private async Task VerifyOutputs(IReadOnlyList<StoredArtifact> outputs,CancellationToken ct)
    {
        foreach(var output in outputs)
        {
            if(!ArtifactPaths.IsRelative(output.RelativePath))Fail("BATCH_SEALED");
            var bytes=await Optional(store,output.RelativePath,ct);
            if(bytes is null||ContentHash.Sha256(bytes.Value.Span)!=output.Sha256||bytes.Value.Length!=output.Length)Fail("BATCH_SEALED");
        }
    }
    private static async Task<ReadOnlyMemory<byte>?> Optional(IArtifactStore input,string path,CancellationToken ct)
    {try{return await input.ReadAsync(path,ct);}catch(FileNotFoundException){return null;}catch(DirectoryNotFoundException){return null;}}
    private static void Add(List<StoredArtifact> outputs,StoredArtifact artifact)
    {var previous=outputs.FirstOrDefault(x=>x.RelativePath==artifact.RelativePath);if(previous is not null){if(previous!=artifact)Fail("BATCH_SEALED");return;}outputs.Add(artifact);}
    [DoesNotReturn]
    private static void Fail(string code)=>throw new TransportException(code,"Source batch prerequisite, binding or retained artifact differs; existing bytes preserved.");
    private sealed class RecordingStore(IArtifactStore inner,List<StoredArtifact> outputs):IArtifactStore
    {
        public Task<ReadOnlyMemory<byte>> ReadAsync(string relativePath,CancellationToken cancellationToken=default)=>inner.ReadAsync(relativePath,cancellationToken);
        public async Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default){var stored=await inner.WriteAsync(artifact,cancellationToken);Add(outputs,stored);return stored;}
    }
}
