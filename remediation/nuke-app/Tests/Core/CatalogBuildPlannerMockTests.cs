using AndroidMcp.BuildTool.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;

public static class CatalogBuildPlannerCases
{
    private static readonly (string Key,string Value)[] Required =
    [("project-root","catalog-contracts"),("source-manifest","sources.json"),("output-root","fresh-catalog"),("stage-root","private-stage")];
    private static readonly string[] Steps=["validate-source-manifest","build-catalog"];
    private static readonly string[] Edges=["build-catalog<-validate-source-manifest"];
    public static IEnumerable<object[]> Cases=>new[]
    {
        new PlannerCase("CAT-PLAN-01",PlannerContractCases.Request("Catalog","Build",Required),new(Category.Catalog,Operation.Build),Steps,[],Edges),
        new PlannerCase("CAT-PLAN-02",PlannerContractCases.Request("Catalog","Build"),null,[],Required.Select(x=>"MISSING_PARAMETER:"+x.Key).ToArray()),
        new PlannerCase("CAT-PLAN-03",PlannerContractCases.Request("Catalog","Build",Required.Concat(new[]{("java-native-manifest","native-source-lock.json"),("extraction-profile","Release011")}).ToArray()),new(Category.Catalog,Operation.Build),Steps,[],Edges),
        new PlannerCase("CAT-PLAN-04",PlannerContractCases.Request("Catalog","Build",Required.Concat(new[]{("legacy-producer","forbidden")}).ToArray()),null,[],["UNSUPPORTED_PARAMETER:legacy-producer"])
    }.Select(x=>new object[]{x});
}
[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004")]
public sealed class CatalogBuildPlannerMockTests
{
    [Theory,MemberData(nameof(CatalogBuildPlannerCases.Cases),MemberType=typeof(CatalogBuildPlannerCases))]
    public void CorrectBuildPlanningRoutePasses(PlannerCase test)=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(PlannerContractCases.Correct(test)));
    [Theory,MemberData(nameof(CatalogBuildPlannerCases.Cases),MemberType=typeof(CatalogBuildPlannerCases))]
    public void WrongRouteAndEarlyDatabaseEffectsAreRejected(PlannerCase test)
    {
        var correct=PlannerContractCases.Correct(test);
        Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(correct,"database-before-preflight"))));
        Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(correct with{Plan=null,Errors=[new("WRONG","","wrong")]}))));
    }
}
