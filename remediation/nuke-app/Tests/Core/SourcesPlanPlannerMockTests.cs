using AndroidMcp.BuildTool.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;

public static class SourcesPlanPlannerCases
{
    private static readonly (string Key,string Value)[] Required =
    [("project-root","inputs"),("source-manifest","recipe.json.gz"),("recipe-lock","recipe.lock.json"),("output-root","fresh-plan")];
    public static IEnumerable<object[]> Cases => new[]
    {
        new PlannerCase("SOURCE-PLAN-01",PlannerContractCases.Request("Sources","Plan",Required),new(Category.Sources,Operation.Plan),["source-plan"],[]),
        new PlannerCase("SOURCE-PLAN-02",PlannerContractCases.Request("Sources","Plan"),null,[],Required.Select(x=>"MISSING_PARAMETER:"+x.Key).ToArray()),
        new PlannerCase("SOURCE-PLAN-03",PlannerContractCases.Request("Sources","Plan",Required.Concat(new[]{("release","release-011"),("limit","2"),("acquisition-profile","GenericRehydrate")}).ToArray()),new(Category.Sources,Operation.Plan),["source-plan"],[]),
        new PlannerCase("SOURCE-PLAN-04",PlannerContractCases.Request("Sources","Plan",Required.Concat(new[]{("download","true")}).ToArray()),null,[],["UNSUPPORTED_PARAMETER:download"])
    }.Select(x=>new object[]{x});
}
[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-003")]
public sealed class SourcesPlanPlannerMockTests
{
    [Theory,MemberData(nameof(SourcesPlanPlannerCases.Cases),MemberType=typeof(SourcesPlanPlannerCases))]
    public void CorrectPlanningRoutePasses(PlannerCase test)=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(PlannerContractCases.Correct(test)));
    [Theory,MemberData(nameof(SourcesPlanPlannerCases.Cases),MemberType=typeof(SourcesPlanPlannerCases))]
    public void WrongRouteAndAcquisitionEffectsAreRejected(PlannerCase test)
    {
        var correct=PlannerContractCases.Correct(test);
        Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(correct,"download-before-plan"))));
        Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(correct with{Plan=null,Errors=[new("WRONG","","wrong")]}))));
    }
}
