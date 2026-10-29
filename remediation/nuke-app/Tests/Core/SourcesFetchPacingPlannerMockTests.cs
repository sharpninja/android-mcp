using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public static class SourcesFetchPacingPlannerCases
{
    public static IEnumerable<object[]> All
    {
        get
        {
            var basis=(PlannerCase)SourcesFetchPlannerCases.Cases.First()[0];
            foreach(var value in new[]{"0","0.25","1"})
            {
                var parameters=basis.Request.Parameters.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);
                parameters.Add("fetch-delay-seconds",value);
                yield return new object[]{basis with{Id="FETCH-PACING-"+value,Request=basis.Request with{Parameters=parameters}}};
            }
            var rejected=basis.Request.Parameters.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);
            rejected.Add("fetch-delay-seconds","1");
            yield return new object[]{new PlannerCase("FETCH-PACING-NOT-SEED",
                basis.Request with{Operation="Seed",Parameters=rejected},null,[],["UNSUPPORTED_PARAMETER:fetch-delay-seconds"])};
        }
    }
}

[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-003")]
public sealed class SourcesFetchPacingPlannerMockTests
{
    [Theory,MemberData(nameof(SourcesFetchPacingPlannerCases.All),MemberType=typeof(SourcesFetchPacingPlannerCases))]
    public void CorrectCategoryScopedParameter(PlannerCase test)
        =>PlannerContractCases.AssertContract(test,new ScriptedPlanner(PlannerContractCases.Correct(test)));

    [Theory,MemberData(nameof(SourcesFetchPacingPlannerCases.All),MemberType=typeof(SourcesFetchPacingPlannerCases))]
    public void WrongAdmissionAndEarlyEffectsAreRejected(PlannerCase test)
    {
        var correct=PlannerContractCases.Correct(test);
        Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(correct,"effect-before-preflight"))));
        var wrong=correct with{Errors=[new("WRONG","fetch-delay-seconds","Wrong admission")],Plan=null};
        Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(wrong))));
    }
}
