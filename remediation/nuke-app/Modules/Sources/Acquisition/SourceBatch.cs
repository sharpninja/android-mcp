using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;

namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;

/// <summary>Acquire Publication's selected physical pins, retaining all logical audit rows.</summary>
public sealed class SourceBatch(IByteTransport transport,IArtifactStore store,IArtifactStore bundle,
    IArtifactLeaseProvider leases,ICanonicalJson canonical,double? fetchDelaySeconds=null,
    ITransportDelay? postDownloadDelay=null):ISourceBatch,ISourceBatchReader,ISourceBatchSeeder
{
    public Task<SourceBatchResult> RunAsync(SourceBatchRequest request,CancellationToken cancellationToken=default)
        =>RunCoreAsync(request,false,null,cancellationToken);
    public Task<SourceBatchResult> ResumeOnlyAsync(SourceBatchResumeRequest request,CancellationToken cancellationToken=default)
        =>RunCoreAsync(request.Acquisition,true,request.RequiredManifest,cancellationToken);
    private async Task<SourceBatchResult> RunCoreAsync(SourceBatchRequest request,bool resumeOnly,StoredArtifact? requiredManifest,CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Validate(request);
        var pause=PostDownloadPause(request.Projection.Profile);
        if(resumeOnly&&(requiredManifest is null||requiredManifest.RelativePath!=request.ReceiptPath))Fail("BATCH_RECEIPT_PIN");
        var projection=request.Projection;
        var identity=canonical.Hash(JsonSerializer.SerializeToElement(request));
        await using var lease=await leases.AcquireAsync("source-batch/"+request.ReceiptPath,cancellationToken);
        var sealedBytes=await Optional(store,request.ReceiptPath,cancellationToken);
        if(resumeOnly)
        {
            if(sealedBytes is null)Fail("BATCH_REQUIRED_RECEIPT");
            if(sealedBytes.Value.Length!=requiredManifest!.Length||ContentHash.Sha256(sealedBytes.Value.Span)!=requiredManifest.Sha256||requiredManifest.MediaType!="application/json")Fail("BATCH_RECEIPT_PIN");
        }
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
            if(acquired.Status=="downloaded"&&pause>TimeSpan.Zero)
                await WaitAfterDownload(pause,cancellationToken);
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

    private TimeSpan PostDownloadPause(AcquisitionCompatibilityProfile profile)
    {
        var seconds=fetchDelaySeconds??(profile==AcquisitionCompatibilityProfile.GenericRehydrate?1d:0d);
        if(!double.IsFinite(seconds)||seconds<0)Fail("BATCH_DELAY");
        try{return TimeSpan.FromSeconds(seconds);}
        catch(ArgumentException){throw new TransportException("BATCH_DELAY","Fetch delay exceeds the native duration range.");}
        catch(OverflowException){throw new TransportException("BATCH_DELAY","Fetch delay exceeds the native duration range.");}
    }
    private async Task WaitAfterDownload(TimeSpan duration,CancellationToken cancellationToken)
    {
        if(postDownloadDelay is not null){await postDownloadDelay.DelayAsync(duration,cancellationToken);return;}
        // Task.Delay has a per-call platform timer bound. Preserve a longer
        // admitted duration through cancellable chunks rather than truncation.
        var maximum=TimeSpan.FromMilliseconds(uint.MaxValue-1d);
        while(duration>TimeSpan.Zero)
        {
            var step=duration>maximum?maximum:duration;
            await Task.Delay(step,cancellationToken);
            duration-=step;
        }
    }

    public async Task<SourceSeedResult> SeedOnlyAsync(SourceBatchSeedRequest request,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Validate(request.Acquisition);ValidateSeed(request);
        var acquisition=request.Acquisition;var projection=request.SeedProjection;
        var identity=canonical.Hash(JsonSerializer.SerializeToElement(request));
        await using var lease=await leases.AcquireAsync("source-batch/"+acquisition.ReceiptPath,cancellationToken);
        var sealedBytes=await Optional(store,acquisition.ReceiptPath,cancellationToken);
        if(sealedBytes is not null)
        {
            if(!acquisition.Projection.Resume)Fail("BATCH_RESUME");
            SourceSeedReceipt prior;
            try
            {
                var normalized=canonical.Serialize(sealedBytes.Value);
                if(!normalized.AsSpan().SequenceEqual(sealedBytes.Value.Span))Fail("SEED_SEALED");
                prior=JsonSerializer.Deserialize<SourceSeedReceipt>(normalized)??throw new JsonException();
            }
            catch(Exception problem)when(problem is JsonException or CanonicalJsonException or NotSupportedException)
            {throw new TransportException("SEED_SEALED","Seed receipt is malformed; original retained.");}
            if(prior.RequestSha256!=identity||!Same(prior.Acquisition,acquisition)||!Same(prior.SeedProjection,projection))Fail("SEED_IDENTITY");
            VerifySeedReceipt(request,prior);
            await VerifyOutputs(prior.Outputs,cancellationToken);
            foreach(var entry in prior.Entries.Where(entry=>entry.Artifact is not null))
                SourceAcquirer.Verify((await store.ReadAsync(entry.Artifact!.RelativePath,cancellationToken)).Span,
                    projection.Sources.Single(source=>source.PhysicalSourceIndex==entry.PhysicalSourceIndex).Pin);
            // Newly available bundle candidates cannot mutate an already sealed partial generation.
            return new(prior,new(acquisition.ReceiptPath,ContentHash.Sha256(sealedBytes.Value.Span),sealedBytes.Value.Length,"application/json"),
                new(0,prior.Entries.Count(entry=>entry.Status!=SourceSeedStatus.Absent),prior.Entries.Count(entry=>entry.Status==SourceSeedStatus.Absent)),true);
        }

        var prepared=new List<(RehydrationSeedSource Source,SourceSeedStatus Status,ReadOnlyMemory<byte>? Bytes,string? Candidate)>();
        foreach(var source in projection.Sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var existing=await Optional(store,source.Pin.LocalPath,cancellationToken);
            if(existing is not null)
            {
                SourceAcquirer.Verify(existing.Value.Span,source.Pin);
                if(!acquisition.Projection.Resume)Fail("BATCH_RESUME");
                prepared.Add((source,SourceSeedStatus.Reused,existing.Value,null));continue;
            }
            ReadOnlyMemory<byte>? selected=null;string? candidatePath=null;
            foreach(var candidate in source.BundleCandidates)
            {
                var bytes=await Optional(bundle,candidate.Path,cancellationToken);
                if(bytes is null)continue;
                // First existing candidate must verify; corruption never falls through.
                SourceAcquirer.Verify(bytes.Value.Span,source.Pin);
                selected=bytes.Value.ToArray();candidatePath=candidate.Path;break;
            }
            prepared.Add((source,selected is null?SourceSeedStatus.Absent:SourceSeedStatus.Imported,selected,candidatePath));
        }
        var outputs=new List<StoredArtifact>();var entries=new List<SourceSeedEntry>();
        // Every target/candidate above is verified before the first copy, preserving original batch preflight behavior.
        foreach(var item in prepared)
        {
            cancellationToken.ThrowIfCancellationRequested();StoredArtifact? artifact=null;
            if(item.Status==SourceSeedStatus.Imported)
                artifact=await WriteMissing(new(item.Source.Pin.LocalPath,item.Bytes!.Value,"application/octet-stream"),cancellationToken);
            else if(item.Status==SourceSeedStatus.Reused)
                artifact=new(item.Source.Pin.LocalPath,item.Source.Pin.Sha256,item.Bytes!.Value.Length,"application/octet-stream");
            if(artifact is not null)Add(outputs,artifact);
            entries.Add(new(item.Source.PhysicalSourceIndex,item.Status,artifact,item.Candidate));
        }
        var counts=new SourceSeedCounts(entries.Count(entry=>entry.Status==SourceSeedStatus.Imported),entries.Count(entry=>entry.Status==SourceSeedStatus.Reused),entries.Count(entry=>entry.Status==SourceSeedStatus.Absent));
        var receipt=new SourceSeedReceipt(identity,acquisition,entries,counts,outputs,counts.Absent==0,false,projection);
        VerifySeedReceipt(request,receipt);await VerifyOutputs(outputs,cancellationToken);
        var manifest=await store.WriteAsync(new(acquisition.ReceiptPath,canonical.Serialize(JsonSerializer.SerializeToElement(receipt)),"application/json"),cancellationToken);
        return new(receipt,manifest,counts,false);
    }
    private static void ValidateSeed(SourceBatchSeedRequest request)
    {
        var acquisition=request.Acquisition;var basis=acquisition.Projection;
        if(!basis.SelectedPhysicalSourceIndices.Order().SequenceEqual(basis.PhysicalSources.Select(source=>source.PhysicalSourceIndex).Order()))Fail("SEED_SELECTION");
        if(acquisition.CachePolicy!=SourceBatchCachePolicy.PreserveAliases)Fail("SEED_CACHE_POLICY");
        var seed=request.SeedProjection;
        if(seed is null||seed.Release!=basis.Release||seed.RecipeSha256!=basis.RecipeSha256||seed.Sources is null||
            !seed.Sources.Select(source=>source.PhysicalSourceIndex).SequenceEqual(basis.PhysicalSources.Select(source=>source.PhysicalSourceIndex)))Fail("SEED_PROJECTION");
        foreach(var source in seed.Sources)
        {
            var physical=basis.PhysicalSources.Single(item=>item.PhysicalSourceIndex==source.PhysicalSourceIndex);
            if(source.WinningSelectionRowIndex!=physical.SelectionRowIndices.Max()||source.Pin is null||source.Pin.LocalPath!=physical.Pin.LocalPath||source.Pin.Sha256!=physical.Pin.Sha256||source.Pin.Compatibility!=basis.Profile)Fail("SEED_PROJECTION");
            SourceAcquirer.ValidatePin(source.Pin);
            var row=basis.Rows.Single(item=>item.RowIndex==source.WinningSelectionRowIndex);
            var snapshot=source.RequiredSnapshot;
            if(snapshot is not null&&(snapshot.SourcePath!=row.LogicalPath||snapshot.LocalPath!=source.Pin.LocalPath||snapshot.Sha256!=source.Pin.Sha256||snapshot.Length!=source.Pin.SizeBytes))Fail("SEED_PROJECTION");
            var path=snapshot?.BundlePath;
            if(path!=source.Pin.RequiredLocalSnapshot||source.BundleCandidates is null||source.BundleCandidates.Count!=(path is null?1:2))Fail("SEED_PROJECTION");
            var first=source.BundleCandidates[0];
            if(first.CandidateOrdinal!=0||first.Kind!=RehydrationSeedCandidateKind.LocalPath||first.Path!=source.Pin.LocalPath)Fail("SEED_PROJECTION");
            if(path is not null)
            {
                _=ArtifactPaths.Segments(path);var second=source.BundleCandidates[1];
                if(second.CandidateOrdinal!=1||second.Kind!=RehydrationSeedCandidateKind.RequiredLocalSnapshot||second.Path!=path)Fail("SEED_PROJECTION");
            }
        }
    }
    private void VerifySeedReceipt(SourceBatchSeedRequest request,SourceSeedReceipt receipt)
    {
        if(receipt.Entries is null||receipt.Outputs is null||receipt.OriginalCounts is null||receipt.Entries.Count!=request.SeedProjection.Sources.Count||receipt.HistoricalByteIdentityClaimed)Fail("SEED_SEALED");
        foreach(var pair in receipt.Entries.Zip(request.SeedProjection.Sources))
        {
            var entry=pair.First;var source=pair.Second;
            if(entry.PhysicalSourceIndex!=source.PhysicalSourceIndex||!Enum.IsDefined(entry.Status))Fail("SEED_SEALED");
            if(entry.Status==SourceSeedStatus.Absent){if(entry.Artifact is not null||entry.BundleCandidate is not null)Fail("SEED_SEALED");continue;}
            var artifact=entry.Artifact;
            if(artifact is null||artifact.RelativePath!=source.Pin.LocalPath||artifact.Sha256!=source.Pin.Sha256||artifact.Length<0||artifact.MediaType!="application/octet-stream"||source.Pin.SizeBytes is not null&&artifact.Length!=source.Pin.SizeBytes)Fail("SEED_SEALED");
            if(entry.Status==SourceSeedStatus.Reused&&entry.BundleCandidate is not null||entry.Status==SourceSeedStatus.Imported&&!source.BundleCandidates.Any(candidate=>candidate.Path==entry.BundleCandidate))Fail("SEED_SEALED");
        }
        var counts=new SourceSeedCounts(receipt.Entries.Count(entry=>entry.Status==SourceSeedStatus.Imported),receipt.Entries.Count(entry=>entry.Status==SourceSeedStatus.Reused),receipt.Entries.Count(entry=>entry.Status==SourceSeedStatus.Absent));
        if(receipt.OriginalCounts!=counts||receipt.RawComplete!=(counts.Absent==0)||!receipt.Outputs.SequenceEqual(receipt.Entries.Where(entry=>entry.Artifact is not null).Select(entry=>entry.Artifact!)))Fail("SEED_SEALED");
    }
    private bool Same<T>(T left,T right)=>canonical.Serialize(JsonSerializer.SerializeToElement(left)).AsSpan().SequenceEqual(canonical.Serialize(JsonSerializer.SerializeToElement(right)));
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


