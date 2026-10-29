using Xunit;
namespace AndroidMcp.BuildTool.Tests.Review;
public sealed record ReviewEnvironmentPolicyObservation(string ExactKeys,string Prefix,string ContainsAny,bool ClearAll,int Calls);
public static class ReviewEnvironmentPolicyCases
{
    public static ReviewEnvironmentPolicyObservation Expected=>new("CLAUDE_CODE_MAX_OUTPUT_TOKENS,MAX_THINKING_TOKENS","HV_","MAX_,DEADLINE,TIMEOUT,BUDGET",false,1);
    public static void AssertContract(ReviewEnvironmentPolicyObservation actual)
    {var expected=Expected;Assert.Equal(expected.ExactKeys,actual.ExactKeys);Assert.Equal(expected.Prefix,actual.Prefix);Assert.Equal(expected.ContainsAny,actual.ContainsAny);Assert.False(actual.ClearAll);Assert.Equal(1,actual.Calls);}
}
public sealed class ReviewEnvironmentPolicyMockTests
{
    [Fact,Trait("Gate","Mock")]
    public void Exact_original_name_only_policy_and_each_wrong_request_field_are_discriminated()
    {
        var actual=ReviewEnvironmentPolicyCases.Expected;ReviewEnvironmentPolicyCases.AssertContract(actual);
        foreach(var wrong in new[]{actual with{ExactKeys="CLAUDE_CODE_MAX_OUTPUT_TOKENS"},actual with{ExactKeys=""},actual with{Prefix="hv_"},actual with{ContainsAny="MAX_,TIMEOUT"},actual with{ClearAll=true},actual with{Calls=2}})
            Assert.ThrowsAny<Exception>(()=>ReviewEnvironmentPolicyCases.AssertContract(wrong));
    }
}
