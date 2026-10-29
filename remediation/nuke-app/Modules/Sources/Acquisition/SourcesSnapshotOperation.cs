using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;

namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;

// Composition only: Publication supplies exact typed rows; existing batch and
// acquisition services own all decoding, retries, archives, pins and writes.
public sealed class SourcesSnapshotOperation(IRehydrationSnapshotPlanner planner,IArtifactStore raw,
    Func<SnapshotCallerMode,ISourceBatch> createBatch,ISnapshotMetadataProofReader? metadataProofs=null):ISourceSnapshotOperation
{
    public async Task<SourceSnapshotResult> RunAsync(SourceSnapshotRequest request,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if(!Enum.IsDefined(request.Mode))Fail("SNAPSHOT_MODE");
        if(request.Recipe.Limit is not null)Fail("SNAPSHOT_LIMIT");
        _=ArtifactPaths.Segments(request.ReceiptPath);
        var planned=planner.PlanSnapshot(request.Recipe);
        if(!planned.Baseline.IsValid)Fail("SNAPSHOT_PLAN_INVALID");
        var baseline=planned.Baseline.Plan!;
        var snapshot=planned.SnapshotProjection??throw new TransportException("SNAPSHOT_PROJECTION","Typed snapshot projection is required.");
        var basis=baseline.SourceProjection??throw new TransportException("SNAPSHOT_PROJECTION","Typed source context is required.");
        if(snapshot.Release!=baseline.Release||snapshot.RecipeSha256!=baseline.RecipeSha256||snapshot.Profile!=request.Recipe.Profile||
            snapshot.Rows.Count!=basis.Rows.Count||snapshot.Rows.Select(row=>row.RowIndex).Distinct().Count()!=snapshot.Rows.Count||
            snapshot.Rows.Any(row=>basis.Rows.All(context=>context.RowIndex!=row.RowIndex)))Fail("SNAPSHOT_PROJECTION");
        if(snapshot.Rows.Any(row=>!row.StatusPresent))Fail("SNAPSHOT_STATUS");
        var eligible=snapshot.Rows.OrderBy(row=>row.RowIndex).Where(row=>row.DownloadedEligible).ToArray();
        foreach(var row in eligible)
        {
            SourceAcquirer.ValidatePin(row.Pin);
            if(row.Pin.GitBlob is null||!Regex.IsMatch(row.Pin.GitBlob,"^[a-f0-9]{40}$"))Fail("SNAPSHOT_GIT_BLOB");
        }
        var winners=eligible.GroupBy(row=>row.Pin.GitBlob,StringComparer.Ordinal).Select(group=>group.First()).ToArray();
        var physical=new List<RehydrationPhysicalSource>();var rows=new List<RehydrationSourceRow>();var missing=new List<int>();
        foreach(var winner in winners)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pin=winner.Pin;
            if(pin.Archive is null&&pin.RequiredLocalSnapshot is null&&winner.RequiredSnapshots.Count==0)
                pin=pin with{Encoding=request.Mode==SnapshotCallerMode.Restore?SourceWireEncoding.SnapshotRestoreGitiles:SourceWireEncoding.SnapshotFetchGitiles};
            var index=physical.Count;
            rows.Add(basis.Rows.Single(row=>row.RowIndex==winner.RowIndex) with{PhysicalSourceIndex=index,CacheAliasIndex=null});
            physical.Add(new(index,pin,[winner.RowIndex],winner.RequiredSnapshots,
                winner.Prerequisites.Select(item=>item with{SelectionRowIndices=[winner.RowIndex]}).ToArray()));
            ReadOnlyMemory<byte>? bytes;
            try{bytes=await raw.ReadAsync(winner.Pin.LocalPath,cancellationToken);}
            catch(FileNotFoundException){bytes=null;}catch(DirectoryNotFoundException){bytes=null;}
            if(bytes is null)missing.Add(index);
            else if(request.Mode==SnapshotCallerMode.Restore)SourceAcquirer.Verify(bytes.Value.Span,winner.Pin);
        }
        var paths=missing.Select(index=>physical[index].Pin.LocalPath).ToArray();
        var externalProofs=new List<SourceSnapshotMetadataProof>();
        if(request.Download&&request.Mode==SnapshotCallerMode.Restore&&request.Recipe.Profile==AcquisitionCompatibilityProfile.CanonicalRestore)
            foreach(var index in missing)
            {
                var reference=winners[index].MetadataReference;
                if(reference is null)continue;
                if(metadataProofs is null)Fail("METADATA_PROOF_REQUIRED");
                var admitted=await metadataProofs.BindAsync(physical[index].Pin,reference,cancellationToken);
                physical[index]=physical[index] with{Pin=admitted.Pin,Prerequisites=physical[index].Prerequisites.Where(item=>
                    item.Code is not (RehydrationPrerequisiteCode.CanonicalArchiveETag or RehydrationPrerequisiteCode.CanonicalArchivePlatformVersion or
                        RehydrationPrerequisiteCode.CanonicalArchiveRepositoryXml or RehydrationPrerequisiteCode.CanonicalArchiveSize)).ToArray()};
                if(admitted.Evidence is not null)externalProofs.Add(new(winners[index].RowIndex,snapshot.RecipeSha256,admitted.Evidence));
            }
        SourceBatchResult? acquisition=null;
        if(request.Download&&missing.Count>0)
        {
            // The old Fetch caller is a Gitiles-only frontend. Mixed archive and
            // immutable snapshot responsibilities use Restore; never fall back.
            if(request.Mode==SnapshotCallerMode.Fetch&&missing.Any(index=>
                physical[index].Pin.Compatibility!=AcquisitionCompatibilityProfile.CanonicalRestore||
                physical[index].Pin.Encoding!=SourceWireEncoding.SnapshotFetchGitiles||physical[index].Pin.Archive is not null||
                physical[index].Pin.RequiredLocalSnapshot is not null||physical[index].RequiredSnapshots.Count>0))Fail("SNAPSHOT_FETCH_TRANSPORT");
            var projection=new RehydrationSourceProjection(snapshot.Release,snapshot.RecipeSha256,snapshot.Profile,rows,physical,[],missing,snapshot.Resume);
            acquisition=await createBatch(request.Mode).RunAsync(new(request.GenerationId,request.ReceiptPath,projection),cancellationToken);
        }
        return new(snapshot.Release,snapshot.RecipeSha256,request.Mode,!request.Download,
            !request.Download&&request.Mode==SnapshotCallerMode.Fetch?1:0,
            new(eligible.Length,winners.Length,winners.Length-missing.Count,missing.Count,
                acquisition?.Receipt.Sources.Count(source=>source.Source.Status=="downloaded")??0),paths,acquisition,
            externalProofs.Count==0?null:externalProofs);
    }
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Fail(string code)=>throw new TransportException(code,"Snapshot caller admission or retained source differs; originals preserved.");
}
