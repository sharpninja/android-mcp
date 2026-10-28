using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;

namespace AndroidMcp.BuildTool.Execution;

// Local-only capability selection. Seed results are not acquisition receipts;
// the existing Fetch operation must subsequently seal acquisition for Inventory/Extract.
public sealed class SourcesSeedOperation(IRehydrationSeedPlanner planner,ISourceBatchSeeder seeder):ISourcesSeedOperation
{
    public async Task<SourceSeedResult> RunAsync(SourcesSeedRequest request,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var planned=planner.PlanSeed(request.Recipe);
        cancellationToken.ThrowIfCancellationRequested();
        if(!planned.BaseResult.IsValid)throw new SourcesSeedException("SEED_PLAN_INVALID",planned.BaseResult.Issues);
        var plan=planned.BaseResult.Plan!;
        if(plan.SourceProjection is null)throw new SourcesSeedException("SEED_PROJECTION_REQUIRED",[]);
        if(planned.SeedProjection is null)throw new SourcesSeedException("SEED_CANDIDATES_REQUIRED",[]);
        if(plan.Network||plan.Complete||plan.HistoricalByteIdentityClaimed)throw new SourcesSeedException("SEED_PLAN_NOT_PURE",[]);
        // Preserve the existing default alias policy and both original typed projections.
        return await seeder.SeedOnlyAsync(new(new(request.GenerationId,request.ReceiptPath,plan.SourceProjection),
            planned.SeedProjection),cancellationToken);
    }
}
