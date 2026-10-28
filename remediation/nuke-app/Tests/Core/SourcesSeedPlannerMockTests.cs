using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public static class SourcesSeedPlannerCases
{
    private static readonly (string Key,string Value)[] Required =
    [("project-root","inputs"),("source-manifest","recipe.json.gz"),("recipe-lock","recipe.lock.json"),
        ("output-root","seed-cache"),("bundle-root","retained-bundle")];
    private static readonly string[] Steps = ["validate-locked-recipe","project-seed-candidates","seed-available-sources","seal-seed-result"];
    private static readonly string[] Edges = ["project-seed-candidates<-validate-locked-recipe","seed-available-sources<-project-seed-candidates","seal-seed-result<-seed-available-sources"];
    public static IEnumerable<object[]> Cases => new[]
    {
        new PlannerCase("SEED-PLAN-01",PlannerContractCases.Request("Sources","Seed",Required),new(Category.Sources,Operation.Seed),Steps,[],Edges),
        new PlannerCase("SEED-PLAN-02",PlannerContractCases.Request("Sources","Seed"),null,[],Required.Select(value=>"MISSING_PARAMETER:"+value.Key).ToArray()),
        new PlannerCase("SEED-PLAN-03",PlannerContractCases.Request("sources","seed",Required),new(Category.Sources,Operation.Seed),Steps,[],Edges),
        new PlannerCase("SEED-PLAN-04",PlannerContractCases.Request("Sources","Seed",Required.Concat(new[]{("generation","seed-1"),("release","release-011"),("acquisition-profile","GenericRehydrate")}).ToArray()),new(Category.Sources,Operation.Seed),Steps,[],Edges),
        new PlannerCase("SEED-PLAN-05",PlannerContractCases.Request("Sources","Seed",Required.Where(value=>value.Key!="bundle-root").ToArray()),null,[],["MISSING_PARAMETER:bundle-root"]),
        new PlannerCase("SEED-PLAN-06",PlannerContractCases.Request("Sources","Seed",Required.Concat(new[]{("cache-policy","ImportVerified")}).ToArray()),null,[],["UNSUPPORTED_PARAMETER:cache-policy"]),
        new PlannerCase("SEED-PLAN-07",PlannerContractCases.Request("Sources","Seed",Required.Concat(new[]{("limit","2")}).ToArray()),null,[],["UNSUPPORTED_PARAMETER:limit"])
    }.Select(test=>new object[]{test});
}

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-003")]
public sealed class SourcesSeedPlannerMockTests
{
    [Theory,MemberData(nameof(SourcesSeedPlannerCases.Cases),MemberType=typeof(SourcesSeedPlannerCases))]
    public void CorrectSeedPlanPasses(PlannerCase test)=>
        PlannerContractCases.AssertContract(test,new ScriptedPlanner(PlannerContractCases.Correct(test)));

    [Theory,MemberData(nameof(SourcesSeedPlannerCases.Cases),MemberType=typeof(SourcesSeedPlannerCases))]
    public void WrongPlanAndPrematureEffectsAreRejected(PlannerCase test)
    {
        var expected=PlannerContractCases.Correct(test);
        Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(expected,"download-before-seed-plan"))));
        Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(expected with{Plan=null,Errors=[new("WRONG","","wrong")]}))));
        if(expected.Plan is null)return;
        Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(expected with{Plan=expected.Plan with{Steps=expected.Plan.Steps.Skip(1).ToArray()}}))));
        foreach(var step in expected.Plan.Steps.Where(value=>value.DependsOn.Count>0))
        {
            var altered=expected.Plan.Steps.Select(value=>value==step?value with{DependsOn=[]}:value).ToArray();
            Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(expected with{Plan=expected.Plan with{Steps=altered}}))));
        }
    }
}
