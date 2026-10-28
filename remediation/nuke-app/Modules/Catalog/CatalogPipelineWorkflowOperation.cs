using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Sources;

namespace AndroidMcp.BuildTool.Catalog;

/// <summary>Thin native composition over one planner, inventory reader and source generation reader.</summary>
public sealed class CatalogPipelineWorkflowOperation(string catalogRoot,string acquisitionRawRoot,
    IRehydrationPlanner planner,ISourcesInventoryReader inventories,ISourceProducerIdentityBuilder identities,
    ISourceExtractGenerationReader generations,ISourcesRuntimeFactory runtimes,ICanonicalJson canonical,
    ICatalogInventoryBridge? inventoryBridge=null,Func<string?,SourceExtractionProfile>? profileResolver=null):ICatalogPipelineWorkflowOperation
{
    private readonly ICatalogInventoryBridge bridge=inventoryBridge ?? new CatalogInventoryBridge(canonical);
    private readonly Func<string?,SourceExtractionProfile> profile=profileResolver ?? SourceExtractionProfile.Resolve;
    public async Task<CatalogBuildResult> RunAsync(CatalogPipelineWorkflowRequest request,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var planned=planner.Plan(request.Recipe);
        if(!planned.IsValid || planned.Plan!.SourceProjection is null)throw new CatalogBuildException("catalog-workflow-plan-invalid","A validated source projection is required");
        var plan=planned.Plan!;
        if(plan.Network || plan.Complete || plan.HistoricalByteIdentityClaimed)throw new CatalogBuildException("catalog-workflow-plan-invalid","Planning must remain pure metadata");
        var selectedProfile=profile(request.ProfileId);
        var admittedInventory=await inventories.ReadAsync(new(plan.SourceProjection!,request.InventoryManifest),cancellationToken);
        var identity=identities.Create(request.Java);
        var intake=new CatalogSourceIntake(generations,new(admittedInventory.Receipt.Acquisition.Receipt.Projection,
            selectedProfile,identity.Identity,request.SourceGenerationCommit,identity.JavaAdmission));
        var input=bridge.Adapt(new(admittedInventory.Receipt,request.Metadata,acquisitionRawRoot,intake));
        var normalized=new CatalogSourceNormalizer(canonical).Normalize(input.Projection,request.Metadata.SourceNormalization,input.VerifiedLogicalRows);
        input=input with{NormalizedSources=normalized};
        using var runtime=runtimes.Create(selectedProfile,identity.JavaAdmission);
        var concerns=runtime.Concerns;
        var builder=new CatalogBuild(catalogRoot,runtime.Dispatcher,concerns.PermissionExpressions,concerns.AnnotationTargets,
            concerns.TypeHeaders,canonical);
        // Bridge bytes are consumed in memory. No manifest, lease, stage or
        // output is materialized before Build's sole Source.Open admission.
        return await builder.BuildAsync(input,request.OutputRoot,request.StageRoot,cancellationToken);
    }
}
