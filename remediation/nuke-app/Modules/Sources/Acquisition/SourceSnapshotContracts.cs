using System.Text.Json.Serialization;
using AndroidMcp.BuildTool.Publication;

namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;

public enum SnapshotCallerMode { Restore,Fetch }
public sealed record SourceSnapshotRequest(RehydrationPlanRequest Recipe,SnapshotCallerMode Mode=SnapshotCallerMode.Restore,
    bool Download=false,string GenerationId="snapshot-acquisition",string ReceiptPath="sources/snapshot.json");
public sealed record SourceSnapshotCounts(int DownloadedRows,int BlobGroups,int Existing,int MissingBefore,int Downloaded);
public sealed record SourceSnapshotResult(string Release,string RecipeSha256,SnapshotCallerMode Mode,bool DryRun,int ExitCode,
    SourceSnapshotCounts Counts,IReadOnlyList<string> MissingPaths,SourceBatchResult? Acquisition,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SourceSnapshotMetadataProof>? MetadataProofs=null);
public interface ISourceSnapshotOperation
{
    Task<SourceSnapshotResult> RunAsync(SourceSnapshotRequest request,CancellationToken cancellationToken=default);
}
