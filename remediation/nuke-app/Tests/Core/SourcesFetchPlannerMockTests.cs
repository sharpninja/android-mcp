using AndroidMcp.BuildTool.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;

public static class SourcesFetchPlannerCases
{
    private static readonly (string Key,string Value)[] Required =
    [ ("project-root","inputs"), ("source-manifest","rehydration/recipe.json"),
      ("recipe-lock","rehydration/recipe.lock.json"), ("output-root","fresh"), ("bundle-root","bundled") ];
    private static readonly string[] Steps = ["validate-locked-recipe", "preflight-selected-sources", "acquire-selected-sources", "seal-acquisition"];
    private static readonly string[] Edges = ["preflight-selected-sources<-validate-locked-recipe", "acquire-selected-sources<-preflight-selected-sources", "seal-acquisition<-acquire-selected-sources"];
    public static IEnumerable<object[]> Cases => new[]
    {
        new PlannerCase("FETCH-PLAN-01",PlannerContractCases.Request("Sources","Fetch",Required),new(Category.Sources,Operation.Fetch),Steps,[],Edges),
        new PlannerCase("FETCH-PLAN-02",PlannerContractCases.Request("Sources","Fetch"),null,[],Required.Select(x=>"MISSING_PARAMETER:"+x.Key).ToArray()),
        new PlannerCase("FETCH-PLAN-03",PlannerContractCases.Request("Sources","Fetch",Required.Concat(new[]{("python-tool","forbidden")}).ToArray()),null,[],["UNSUPPORTED_PARAMETER:python-tool"]),
        new PlannerCase("FETCH-PLAN-04",PlannerContractCases.Request("Sources","Fetch",Required.Concat(new[]{("generation","g1"),("release","fixture"),("limit","1"),("acquisition-profile","GenericRehydrate"),("cache-policy","PreserveAliases")}).ToArray()),new(Category.Sources,Operation.Fetch),Steps,[],Edges)
    }.Select(x=>new object[]{x});
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-003")]
public sealed class SourcesFetchPlannerMockTests
{
    [Theory,MemberData(nameof(SourcesFetchPlannerCases.Cases),MemberType=typeof(SourcesFetchPlannerCases))]
    public void CorrectPlannedRoutePasses(PlannerCase test)=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(PlannerContractCases.Correct(test)));
    [Theory,MemberData(nameof(SourcesFetchPlannerCases.Cases),MemberType=typeof(SourcesFetchPlannerCases))]
    public void WrongRouteAndEarlyEffectsFail(PlannerCase test)
    {
        var correct=PlannerContractCases.Correct(test);
        Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(correct,"effect-before-preflight"))));
        var wrong=correct with{Errors=[new("WRONG","","wrong")],Plan=null};
        Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(wrong))));
    }
}
