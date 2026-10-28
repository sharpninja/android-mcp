using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogPipelineWorkflowCall(string Variant);
public sealed record CatalogPipelineWorkflowReply(string Outcome,int OuterReads,int RecipePinReads,int PlannerCalls,
    int InventoryAdmissions,int GenerationAdmissions,int ProducerObservations,string SelectedLogicalRows,
    bool OriginalRowsUnchanged,bool ExplicitRootsPreserved,bool HistoricalCacheUnverified,bool SourceContextPreserved,
    bool NoOutputEffects,bool InputsRetained);
public sealed record CatalogPipelineWorkflowCase(string Id,CatalogPipelineWorkflowCall Call,CatalogPipelineWorkflowReply Expected);
public interface ICatalogPipelineWorkflowCaseProvider
{
    Task<CatalogPipelineWorkflowReply> AdmitAsync(CatalogPipelineWorkflowCall call);
}
public static class CatalogPipelineWorkflowCases
{
    // External inputs are exact shared SourceWorkflowInputs pins. Correct fixture
    // receipt rows0/1 alias reused physical0; row2 is unselected despite its old
    // downloaded status. One planner derives the projection, never the wire file.
    private static CatalogPipelineWorkflowReply Reply(string outcome,int outer=1,int recipe=0,int planner=0,
        int inventory=0,int generation=0,int producer=0,string selected="") =>
        new(outcome,outer,recipe,planner,inventory,generation,producer,selected,true,true,true,true,outcome!="ok",true);
    public static IEnumerable<object[]> All => new[]
    {
        new CatalogPipelineWorkflowCase("WORKFLOW-01-shared-recipe-aliases-current-proof",new("selected-aliases"),Reply("ok",1,2,1,1,1,1,"0,1")),
        new CatalogPipelineWorkflowCase("WORKFLOW-02-outer-SHA-before-JSON",new("wrong-outer-sha"),Reply("ARTIFACT_PIN_HASH")),
        new CatalogPipelineWorkflowCase("WORKFLOW-03-outer-length-before-JSON",new("wrong-outer-length"),Reply("ARTIFACT_PIN_LENGTH")),
        new CatalogPipelineWorkflowCase("WORKFLOW-04-mutually-exclusive-input-mode",new("legacy-and-pipeline"),Reply("catalog-input-mode",0)),
        new CatalogPipelineWorkflowCase("WORKFLOW-05-shared-recipe-pin-before-planner",new("wrong-recipe-pin"),Reply("ARTIFACT_PIN_HASH",1,1)),
        new CatalogPipelineWorkflowCase("WORKFLOW-06-shared-lock-pin-before-planner",new("wrong-lock-pin"),Reply("ARTIFACT_PIN_HASH",1,2)),
        new CatalogPipelineWorkflowCase("WORKFLOW-07-no-external-admitted-projection",new("forged-projection"),Reply("catalog-pipeline-request")),
        new CatalogPipelineWorkflowCase("WORKFLOW-08-inventory-agrees-with-derived-projection",new("foreign-inventory"),Reply("INVENTORY_READ_PROJECTION",1,2,1,1)),
        new CatalogPipelineWorkflowCase("WORKFLOW-09-source-commit-pin-before-effects",new("wrong-generation-pin"),Reply("SOURCE_EXTRACT_INDEX_PIN",1,2,1,1,1,1)),
        new CatalogPipelineWorkflowCase("WORKFLOW-10-historical-downloaded-is-not-selection",new("none-selected"),Reply("ok",1,2,1,1,1,1)),
        new CatalogPipelineWorkflowCase("WORKFLOW-11-no-editable-native-producer",new("forged-producer"),Reply("catalog-pipeline-request"))
    }.Select(test=>new object[]{test});
    public static async Task AssertCase(CatalogPipelineWorkflowCase test,ICatalogPipelineWorkflowCaseProvider provider) =>
        Assert.Equal(test.Expected,await provider.AdmitAsync(test.Call));
}
public sealed class ScriptedCatalogPipelineWorkflow(CatalogPipelineWorkflowCase test,int wrong=-1):ICatalogPipelineWorkflowCaseProvider
{
    public Task<CatalogPipelineWorkflowReply> AdmitAsync(CatalogPipelineWorkflowCall call)
    {
        Assert.Same(test.Call,call);var value=test.Expected;
        return Task.FromResult(wrong switch
        {
            0=>value with{Outcome="wrong"},1=>value with{OuterReads=value.OuterReads+1},2=>value with{RecipePinReads=value.RecipePinReads+1},
            3=>value with{PlannerCalls=value.PlannerCalls+1},4=>value with{InventoryAdmissions=value.InventoryAdmissions+1},
            5=>value with{GenerationAdmissions=value.GenerationAdmissions+1},6=>value with{ProducerObservations=value.ProducerObservations+1},
            7=>value with{SelectedLogicalRows="wrong"},8=>value with{OriginalRowsUnchanged=!value.OriginalRowsUnchanged},
            9=>value with{ExplicitRootsPreserved=!value.ExplicitRootsPreserved},10=>value with{HistoricalCacheUnverified=!value.HistoricalCacheUnverified},
            11=>value with{SourceContextPreserved=!value.SourceContextPreserved},12=>value with{NoOutputEffects=!value.NoOutputEffects},
            13=>value with{InputsRetained=!value.InputsRetained},_=>value
        });
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogPipelineWorkflowMockTests
{
    [Theory,MemberData(nameof(CatalogPipelineWorkflowCases.All),MemberType=typeof(CatalogPipelineWorkflowCases))]
    public Task CorrectDerivedProjectionWorkflowAdmissionPasses(CatalogPipelineWorkflowCase test) =>
        CatalogPipelineWorkflowCases.AssertCase(test,new ScriptedCatalogPipelineWorkflow(test));
    [Theory,MemberData(nameof(CatalogPipelineWorkflowCases.All),MemberType=typeof(CatalogPipelineWorkflowCases))]
    public async Task EveryAdmissionOrderIdentitySelectionAndSideEffectMutationRejected(CatalogPipelineWorkflowCase test)
    {
        for(var wrong=0;wrong<14;wrong++) Assert.NotNull(await Record.ExceptionAsync(()=>
            CatalogPipelineWorkflowCases.AssertCase(test,new ScriptedCatalogPipelineWorkflow(test,wrong))));
    }
}
