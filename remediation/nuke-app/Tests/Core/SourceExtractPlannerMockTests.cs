using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public static class SourceExtractPlannerCases
{
    private static readonly (string Key,string Value)[] Required =
    [("project-root","inputs"),("pipeline-request","extract-workflow.json"),
        ("pipeline-request-sha256",new string('a',64)),("pipeline-request-length","1234")];
    private static readonly string[] Steps = ["admit-pinned-workflow","admit-locked-recipe","verify-source-inventory","capture-runtime-identity","extract-source-generation","seal-extraction"];
    private static readonly string[] Edges = ["admit-locked-recipe<-admit-pinned-workflow","verify-source-inventory<-admit-locked-recipe","capture-runtime-identity<-verify-source-inventory","extract-source-generation<-capture-runtime-identity","seal-extraction<-extract-source-generation"];
    public static IEnumerable<object[]> Cases => new[]
    {
        new PlannerCase("EXTRACT-PLAN-01",PlannerContractCases.Request("Sources","Extract",Required),new(Category.Sources,Operation.Extract),Steps,[],Edges),
        new PlannerCase("EXTRACT-PLAN-02",PlannerContractCases.Request("Sources","Extract"),null,[],Required.Select(value=>"MISSING_PARAMETER:"+value.Key).ToArray()),
        new PlannerCase("EXTRACT-PLAN-03",PlannerContractCases.Request("sources","extract",Required),new(Category.Sources,Operation.Extract),Steps,[],Edges),
        new PlannerCase("EXTRACT-PLAN-04",PlannerContractCases.Request("Sources","Extract",Required.Where(value=>value.Key!="pipeline-request-sha256").ToArray()),null,[],["MISSING_PARAMETER:pipeline-request-sha256"]),
        new PlannerCase("EXTRACT-PLAN-05",PlannerContractCases.Request("Sources","Extract",Required.Where(value=>value.Key!="pipeline-request-length").ToArray()),null,[],["MISSING_PARAMETER:pipeline-request-length"]),
        new PlannerCase("EXTRACT-PLAN-06",PlannerContractCases.Request("Sources","Extract",Required.Concat(new[]{("producer-id","caller-fiction")}).ToArray()),null,[],["UNSUPPORTED_PARAMETER:producer-id"]),
        new PlannerCase("EXTRACT-PLAN-07",PlannerContractCases.Request("Sources","Extract",Required.Concat(new[]{("projection","caller-fiction")}).ToArray()),null,[],["UNSUPPORTED_PARAMETER:projection"])
    }.Select(test=>new object[]{test});
}

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-003")]
public sealed class SourceExtractPlannerMockTests
{
    [Theory,MemberData(nameof(SourceExtractPlannerCases.Cases),MemberType=typeof(SourceExtractPlannerCases))]
    public void CorrectPinnedWorkflowPlanPasses(PlannerCase test)=>
        PlannerContractCases.AssertContract(test,new ScriptedPlanner(PlannerContractCases.Correct(test)));

    [Theory,MemberData(nameof(SourceExtractPlannerCases.Cases),MemberType=typeof(SourceExtractPlannerCases))]
    public void WrongPlanOrPreAdmissionEffectsAreRejected(PlannerCase test)
    {
        var expected=PlannerContractCases.Correct(test);
        Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(expected,"source-read-before-workflow-pin"))));
        Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(expected with{Plan=null,Errors=[new("WRONG","","wrong")]}))));
        if(expected.Plan is not null)
        {
            Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(expected with{Plan=expected.Plan with{Steps=expected.Plan.Steps.Skip(1).ToArray()}}))));
            foreach(var step in expected.Plan.Steps.Where(step=>step.DependsOn.Count>0))
            {
                var altered=expected.Plan.Steps.Select(value=>value==step?value with{DependsOn=[]}:value).ToArray();
                Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(expected with{Plan=expected.Plan with{Steps=altered}}))));
            }
        }
    }
}
