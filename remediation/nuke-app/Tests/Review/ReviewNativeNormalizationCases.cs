using Xunit;
namespace AndroidMcp.BuildTool.Tests.Review;
public sealed record NativeNormObservation(string Code,int Projects,string Roles,int FullRuns,bool NoAuthorityLabel,int ProviderCalls);
public static class ReviewNativeNormalizationCases
{
    public static string[] Names=>["complete-mixed-and-gradle","filtered-run","rejected-producer","evaluation-not-in-producer","changed-evaluation-pin","missing-metadata","unknown-role","duplicate-native-project","dropped-native-project","wrong-definition-sha","gradle-fake-test","missing-gradle-policy","wrong-policy-head","stale-source-fingerprint","wrong-trx-origin-hash","source-definition-mismatch"];
    public static IEnumerable<object[]> Data=>Names.Select(x=>new object[]{x});
    public static string Code(string name)=>name switch
    {
        "complete-mixed-and-gradle"=>"normalized","filtered-run"=>"UNFILTERED_RUN_REQUIRED",
        "rejected-producer" or "evaluation-not-in-producer" or "stale-source-fingerprint" or "wrong-trx-origin-hash"=>"NATIVE_GATE_UNVERIFIED",
        "changed-evaluation-pin"=>"ARTIFACT_PIN_HASH","wrong-definition-sha" or "source-definition-mismatch"=>"EVIDENCE_HASH_MISMATCH",
        "wrong-policy-head"=>"HEAD_MISMATCH",_=>"PROJECT_METADATA_UNRESOLVED"
    };
    public static void AssertContract(string name,NativeNormObservation actual)
    {
        var valid=name=="complete-mixed-and-gradle";
        Assert.Equal(Code(name),actual.Code);Assert.Equal(valid?3:0,actual.Projects);Assert.Equal(valid?"app.csproj:none,design/build.gradle.kts:none,Tests.csproj:mixed":"",actual.Roles);
        Assert.Equal(valid?1:0,actual.FullRuns);Assert.True(actual.NoAuthorityLabel);Assert.Equal(0,actual.ProviderCalls);
    }
}
public sealed class ReviewNativeNormalizationMockTests
{
    [Theory,MemberData(nameof(ReviewNativeNormalizationCases.Data),MemberType=typeof(ReviewNativeNormalizationCases)),Trait("Gate","Mock")]
    public void Correct_native_origin_projection_and_individual_schema_effect_controls(string name)
    {
        var code=name switch
        {
            "complete-mixed-and-gradle"=>"normalized","filtered-run"=>"UNFILTERED_RUN_REQUIRED",
            "rejected-producer" or "evaluation-not-in-producer" or "stale-source-fingerprint" or "wrong-trx-origin-hash"=>"NATIVE_GATE_UNVERIFIED",
            "changed-evaluation-pin"=>"ARTIFACT_PIN_HASH","wrong-definition-sha" or "source-definition-mismatch"=>"EVIDENCE_HASH_MISMATCH",
            "wrong-policy-head"=>"HEAD_MISMATCH",_=>"PROJECT_METADATA_UNRESOLVED"
        };
        var actual=code=="normalized"?new NativeNormObservation(code,3,"app.csproj:none,design/build.gradle.kts:none,Tests.csproj:mixed",1,true,0):new(code,0,"",0,true,0);
        ReviewNativeNormalizationCases.AssertContract(name,actual);
        foreach(var wrong in new[]{actual with{Code="wrong"},actual with{Projects=actual.Projects+1},actual with{Roles="wrong"},actual with{FullRuns=actual.FullRuns+1},actual with{NoAuthorityLabel=false},actual with{ProviderCalls=1}})
            Assert.ThrowsAny<Exception>(()=>ReviewNativeNormalizationCases.AssertContract(name,wrong));
    }
}
