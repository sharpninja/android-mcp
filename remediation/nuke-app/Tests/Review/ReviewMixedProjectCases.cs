using Xunit;
namespace AndroidMcp.BuildTool.Tests.Review;
public sealed record MixedProjectObservation(string Code,string Units,string Integrations,int TrxEvidenceCount,int ReadyCalls,int GitCalls,int ProviderCalls);
public static class ReviewMixedProjectCases
{
    public static string[] Names=>["mixed-shared-full-trx","mixed-filtered-run","mixed-missing-run","mixed-wrong-trx-pin","mixed-wrong-run-metadata","mixed-collapsed-unit","duplicate-project-row","unknown-role","mixed-nontest-contradiction"];
    public static IEnumerable<object[]> Data=>Names.Select(x=>new object[]{x});
    public static string Code(string name)=>name switch
    {
        "mixed-shared-full-trx"=>"prepared",
        "mixed-filtered-run" or "mixed-missing-run" or "mixed-wrong-trx-pin" or "mixed-wrong-run-metadata"=>"FULL_TEST_RUN_UNVERIFIED",
        "mixed-collapsed-unit"=>"INTEGRATION_SCOPE_INCOMPLETE",
        _=>"PROJECT_METADATA_UNRESOLVED"
    };
    public static void AssertContract(string name,MixedProjectObservation actual)
    {
        Assert.Equal(Code(name),actual.Code);
        Assert.Equal(name=="mixed-shared-full-trx"?"Tests.csproj":"",actual.Units);
        Assert.Equal(name=="mixed-shared-full-trx"?"Tests.csproj":"",actual.Integrations);
        Assert.Equal(1,actual.TrxEvidenceCount);Assert.Equal(1,actual.ReadyCalls);Assert.Equal(5,actual.GitCalls);Assert.Equal(0,actual.ProviderCalls);
    }
}
public sealed class ReviewMixedProjectMockTests
{
    [Theory,MemberData(nameof(ReviewMixedProjectCases.Data),MemberType=typeof(ReviewMixedProjectCases)),Trait("Gate","Mock")]
    public void Correct_and_individual_role_pin_and_effect_controls(string name)
    {
        var actual=name switch
        {
            "mixed-shared-full-trx"=>new MixedProjectObservation("prepared","Tests.csproj","Tests.csproj",1,1,5,0),
            "mixed-filtered-run" or "mixed-missing-run" or "mixed-wrong-trx-pin" or "mixed-wrong-run-metadata"=>new("FULL_TEST_RUN_UNVERIFIED","","",1,1,5,0),
            "mixed-collapsed-unit"=>new("INTEGRATION_SCOPE_INCOMPLETE","","",1,1,5,0),
            _=>new("PROJECT_METADATA_UNRESOLVED","","",1,1,5,0)
        };
        ReviewMixedProjectCases.AssertContract(name,actual);
        foreach(var wrong in new[]{actual with{Code="wrong"},actual with{Units="wrong"},actual with{Integrations="wrong"},
            actual with{TrxEvidenceCount=2},actual with{ReadyCalls=2},actual with{GitCalls=6},actual with{ProviderCalls=1}})
            Assert.ThrowsAny<Exception>(()=>ReviewMixedProjectCases.AssertContract(name,wrong));
    }
}
