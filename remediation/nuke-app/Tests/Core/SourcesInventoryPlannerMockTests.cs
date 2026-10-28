using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public static class SourcesInventoryPlannerCases
{
    private static readonly (string Key,string Value)[] Required =
    [("project-root","inputs"),("source-manifest","recipe.json.gz"),("recipe-lock","recipe.lock.json"),
        ("acquisition-root","sealed-sources"),("acquisition-manifest","source-batch.json"),
        ("acquisition-manifest-sha256",new string('a',64)),("acquisition-manifest-length","1234"),("output-root","new-inventory")];
    private static readonly string[] Steps = ["validate-source-plan","admit-acquisition-receipt","verify-acquisition-readonly","seal-source-inventory"];
    private static readonly string[] Edges = ["admit-acquisition-receipt<-validate-source-plan","verify-acquisition-readonly<-admit-acquisition-receipt","seal-source-inventory<-verify-acquisition-readonly"];
    public static IEnumerable<object[]> Cases => new[]
    {
        new PlannerCase("INVENTORY-PLAN-01",PlannerContractCases.Request("Sources","Inventory",Required),new(Category.Sources,Operation.Inventory),Steps,[],Edges),
        new PlannerCase("INVENTORY-PLAN-02",PlannerContractCases.Request("Sources","Inventory"),null,[],Required.Select(x=>"MISSING_PARAMETER:"+x.Key).ToArray()),
        new PlannerCase("INVENTORY-PLAN-03",PlannerContractCases.Request("Sources","Inventory",Required.Concat(new[]{("release","release-011"),("limit","1"),("acquisition-profile","GenericRehydrate")}).ToArray()),new(Category.Sources,Operation.Inventory),Steps,[],Edges),
        new PlannerCase("INVENTORY-PLAN-04",PlannerContractCases.Request("Sources","Inventory",Required.Concat(new[]{("repair-missing","true")}).ToArray()),null,[],["UNSUPPORTED_PARAMETER:repair-missing"]),
        new PlannerCase("INVENTORY-PLAN-05",PlannerContractCases.Request("Sources","Inventory",Required.Where(x=>x.Key!="acquisition-manifest-sha256").ToArray()),null,[],["MISSING_PARAMETER:acquisition-manifest-sha256"])
    }.Select(test=>new object[]{test});
}

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-003")]
public sealed class SourcesInventoryPlannerMockTests
{
    [Theory,MemberData(nameof(SourcesInventoryPlannerCases.Cases),MemberType=typeof(SourcesInventoryPlannerCases))]
    public void CorrectReadOnlyInventoryPlanPasses(PlannerCase test)=>
        PlannerContractCases.AssertContract(test,new ScriptedPlanner(PlannerContractCases.Correct(test)));
    [Theory,MemberData(nameof(SourcesInventoryPlannerCases.Cases),MemberType=typeof(SourcesInventoryPlannerCases))]
    public void WrongPlanAndAcquisitionEffectsAreRejected(PlannerCase test)
    {
        var expected=PlannerContractCases.Correct(test);
        Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(expected,"acquisition-before-pinned-read"))));
        Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(expected with{Plan=null,Errors=[new("WRONG","","wrong")]}))));
    }
}
