using AndroidMcp.BuildTool.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;

public static class PublicationCheckPlannerCases
{
    private static readonly (string Key,string Value)[] Required=[("git-checkout","repository"),("git-identity","git.json"),("receipt-root","fresh-receipts")];
    public static IEnumerable<object[]> Cases=>new[]
    {
        new PlannerCase("PUB-PLAN-01",PlannerContractCases.Request("Publish","Check",Required),new(Category.Publish,Operation.Check),["check-deliverables"],[]),
        new PlannerCase("PUB-PLAN-02",PlannerContractCases.Request("Publish","Check"),null,[],Required.Select(x=>"MISSING_PARAMETER:"+x.Key).ToArray()),
        new PlannerCase("PUB-PLAN-03",PlannerContractCases.Request("Publish","Check",Required.Concat(new[]{("policy-relative-path","policy/custom.json")}).ToArray()),new(Category.Publish,Operation.Check),["check-deliverables"],[]),
        new PlannerCase("PUB-PLAN-04",PlannerContractCases.Request("Publish","Check",Required.Concat(new[]{("push","true")}).ToArray()),null,[],["UNSUPPORTED_PARAMETER:push"])
    }.Select(x=>new object[]{x});
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class PublicationCheckPlannerMockTests
{
    [Theory,MemberData(nameof(PublicationCheckPlannerCases.Cases),MemberType=typeof(PublicationCheckPlannerCases))]
    public void CorrectMockRoutePasses(PlannerCase test)=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(PlannerContractCases.Correct(test)));
    [Theory,MemberData(nameof(PublicationCheckPlannerCases.Cases),MemberType=typeof(PublicationCheckPlannerCases))]
    public void WrongRouteOrSideEffectIsRejected(PlannerCase test)
    {
        var correct=PlannerContractCases.Correct(test);
        Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(correct,"push-before-policy-check"))));
        Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(correct with{Errors=[new("WRONG","","wrong")],Plan=null}))));
    }
}
