using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Sources;

namespace AndroidMcp.BuildTool.Execution;

// The host supplies stores bound to the admitted workflow's explicit roles.
// This composer consumes existing services, never recipe rows or cache envelopes.
public sealed class SourcesExtractWorkflowOperation(IRehydrationPlanner planner,ISourcesInventoryReader inventoryReader,
    ISourceProducerIdentityBuilder identityBuilder,ISourcesExtractOperation extractor,
    Func<string?,SourceExtractionProfile>? profileResolver=null):ISourcesExtractWorkflowOperation
{
    private readonly Func<string?,SourceExtractionProfile> resolveProfile=profileResolver??SourceExtractionProfile.Resolve;
    public async Task<SourceExtractResult> RunAsync(SourcesExtractWorkflowRequest request,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var planned=planner.Plan(request.Recipe);
        cancellationToken.ThrowIfCancellationRequested();
        if(!planned.IsValid)throw new SourcesExtractWorkflowException("EXTRACT_WORKFLOW_PLAN_INVALID",planned.Issues);
        var plan=planned.Plan!;
        if(plan.SourceProjection is null)throw new SourcesExtractWorkflowException("EXTRACT_WORKFLOW_PROJECTION_REQUIRED",[]);
        if(plan.Network||plan.Complete||plan.HistoricalByteIdentityClaimed)
            throw new SourcesExtractWorkflowException("EXTRACT_WORKFLOW_PLAN_NOT_PURE",[]);
        var profile=resolveProfile(request.ProfileId);
        if(request.Java is{} java&&(java.ExpectedManifestLength is null or <0||
            java.ExpectedManifestSha256 is null||java.ExpectedManifestSha256.Length!=64||!java.ExpectedManifestSha256.All(Uri.IsHexDigit)))
            throw new SourcesExtractWorkflowException("EXTRACT_WORKFLOW_JAVA_PIN",[]);
        var inventory=await inventoryReader.ReadAsync(new(plan.SourceProjection,request.InventoryManifest),cancellationToken);
        var identity=identityBuilder.Create(request.Java);
        return await extractor.ExtractAsync(new(inventory.Receipt.Acquisition.Receipt.Projection,request.GenerationRoot,
            profile,identity.Identity,request.TrustedPriorIndex,identity.JavaAdmission,request.CacheSourceLimit),cancellationToken);
    }
}
