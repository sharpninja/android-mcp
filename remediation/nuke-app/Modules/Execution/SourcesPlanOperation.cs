using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
namespace AndroidMcp.BuildTool.Execution;

/// <summary>Retain the authoritative pure plan artifact without acquiring sources.</summary>
public sealed class SourcesPlanOperation(IRehydrationPlanner planner,IArtifactStore output):ISourcesPlanOperation
{
    public async Task<SourcesPlanResult> RunAsync(RehydrationPlanRequest request,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result=planner.Plan(request);
        if(!result.IsValid)throw new SourcesPlanException("PLAN_INVALID",result.Issues);
        var plan=result.Plan!;
        if(plan.SourceProjection is null)throw new SourcesPlanException("PLAN_PROJECTION_REQUIRED",[]);
        if(plan.Network||plan.Complete||plan.HistoricalByteIdentityClaimed)throw new SourcesPlanException("PLAN_NOT_PURE",[]);
        cancellationToken.ThrowIfCancellationRequested();
        return new(plan,await output.WriteAsync(plan.Manifest,cancellationToken));
    }
}
