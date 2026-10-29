using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;

namespace AndroidMcp.BuildTool.Execution;

/// <summary>The validated Publication projection is the sole acquisition input.</summary>
public sealed class SourcesFetchOperation(IRehydrationPlanner planner,ISourceBatch batch):ISourcesFetchOperation
{
    public async Task<SourceBatchResult> RunAsync(SourcesFetchRequest request,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var planned=planner.Plan(request.Recipe);
        if(!planned.IsValid)throw new SourcesFetchException("FETCH_PLAN_INVALID",planned.Issues);
        var projection=planned.Plan!.SourceProjection;
        if(projection is null)throw new SourcesFetchException("FETCH_PROJECTION_REQUIRED",[]);
        cancellationToken.ThrowIfCancellationRequested();
        return await batch.RunAsync(new(request.GenerationId,request.ReceiptPath,projection,request.CachePolicy),cancellationToken);
    }
}
