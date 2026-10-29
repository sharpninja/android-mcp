using Xunit;
namespace AndroidMcp.BuildTool.Tests.Review;
public sealed record RequirementNormObservation(string Code,int Records,int Mappings,int Criteria,string State,int Pins,bool NoAuthorityLabel,int ProviderCalls);
public static class ReviewRequirementNormalizationCases
{
    public static string[] Names=>["documented-pending-records","unknown-response-envelope","wrong-exact-id","foreign-test-id","missing-acceptance-criterion","mapping-without-tests",
        "missing-explicit-coverage","unsupported-coverage-statement","changed-record-pin","wrong-coverage-head","missing-tested-class","duplicate-record"];
    public static IEnumerable<object[]> Data=>Names.Select(x=>new object[]{x});
    public static string Code(string name)=>name switch
    {
        "documented-pending-records"=>"normalized",
        "unknown-response-envelope"=>"OFFICIAL_RESPONSE_SCHEMA_UNAVAILABLE",
        "mapping-without-tests"=>"REQUIREMENT_MAPPING_UNAVAILABLE",
        "missing-explicit-coverage" or "unsupported-coverage-statement" or "missing-tested-class"=>"CRITERION_COVERAGE_UNAVAILABLE",
        "changed-record-pin"=>"ARTIFACT_PIN_HASH","wrong-coverage-head"=>"HEAD_MISMATCH",
        _=>"REQUIREMENTS_INCOMPLETE"
    };
    public static void AssertContract(string name,RequirementNormObservation actual)
    {
        var positive=name=="documented-pending-records";
        Assert.Equal(Code(name),actual.Code);Assert.Equal(positive?3:0,actual.Records);Assert.Equal(positive?1:0,actual.Mappings);
        Assert.Equal(positive?1:0,actual.Criteria);Assert.Equal(positive?"pending:false":"",actual.State);Assert.Equal(positive?6:0,actual.Pins);
        Assert.True(actual.NoAuthorityLabel);Assert.Equal(0,actual.ProviderCalls);
    }
}
public sealed class ReviewRequirementNormalizationMockTests
{
    [Theory,MemberData(nameof(ReviewRequirementNormalizationCases.Data),MemberType=typeof(ReviewRequirementNormalizationCases)),Trait("Gate","Mock")]
    public void Correct_documented_records_and_individual_omission_mapping_provenance_effect_controls(string name)
    {
        var code=name switch
        {
            "documented-pending-records"=>"normalized","unknown-response-envelope"=>"OFFICIAL_RESPONSE_SCHEMA_UNAVAILABLE",
            "mapping-without-tests"=>"REQUIREMENT_MAPPING_UNAVAILABLE",
            "missing-explicit-coverage" or "unsupported-coverage-statement" or "missing-tested-class"=>"CRITERION_COVERAGE_UNAVAILABLE",
            "changed-record-pin"=>"ARTIFACT_PIN_HASH","wrong-coverage-head"=>"HEAD_MISMATCH",_=>"REQUIREMENTS_INCOMPLETE"
        };
        var correct=code=="normalized"?new RequirementNormObservation(code,3,1,1,"pending:false",6,true,0):new(code,0,0,0,"",0,true,0);
        ReviewRequirementNormalizationCases.AssertContract(name,correct);
        foreach(var wrong in new[]{correct with{Code="wrong"},correct with{Records=correct.Records+1},correct with{Mappings=correct.Mappings+1},
            correct with{Criteria=correct.Criteria+1},correct with{State="wrong"},correct with{Pins=correct.Pins+1},correct with{NoAuthorityLabel=false},correct with{ProviderCalls=1}})
            Assert.ThrowsAny<Exception>(()=>ReviewRequirementNormalizationCases.AssertContract(name,wrong));
    }
}
