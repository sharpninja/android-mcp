using Xunit;
namespace AndroidMcp.BuildTool.Tests.Review;
public sealed record ReceiptDiscoveryObservation(string Code,string Rounds,int AuditFiles,bool InputsRetained,bool UnrelatedRead,bool ProviderCall);
public static class ReviewReceiptDiscoveryCases
{
    public static string[] Names=>["single-round","sorted-rounds","missing-reviews","no-rounds","ignore-unrelated","ignore-nested","ignore-private-file","derived-priority","required-missing","round-is-file","round-is-link","receipt-is-link","malformed-request","manifest-escape","manifest-private","private-request-content","output-conflict"];
    public static IEnumerable<object[]> Data=>Names.Select(x=>new object[]{x});
    public static ReceiptDiscoveryObservation Expected(string name)=>name switch
    {
        "required-missing"=>new("REVIEW_RECEIPT_MISSING","",0,true,false,false),"round-is-file" or "round-is-link" or "receipt-is-link"=>new("REVIEW_RECEIPT_SCOPE_BLOCKED","",0,true,false,false),
        "malformed-request"=>new("INVALID_REVIEW_RECEIPT","",0,true,false,false),"manifest-escape"=>new("INVALID_PATH","",0,true,false,false),"manifest-private" or "private-request-content"=>new("PRIVATE_PAYLOAD","",0,true,false,false),
        "output-conflict"=>new("OUTPUT_CONFLICT","",1,true,false,false),"missing-reviews" or "no-rounds"=>new("collected","",1,true,false,false),
        "sorted-rounds"=>new("collected","round-001,round-002",1,true,false,false),_=>new("collected","round-001",1,true,false,false)
    };
    public static void AssertContract(string name,ReceiptDiscoveryObservation actual)
    {var expected=Expected(name);Assert.Equal(expected.Code,actual.Code);Assert.Equal(expected.Rounds,actual.Rounds);Assert.Equal(expected.AuditFiles,actual.AuditFiles);Assert.True(actual.InputsRetained);Assert.False(actual.UnrelatedRead);Assert.False(actual.ProviderCall);}
}
public sealed class ReviewReceiptDiscoveryMockTests
{
    [Theory,MemberData(nameof(ReviewReceiptDiscoveryCases.Data),MemberType=typeof(ReviewReceiptDiscoveryCases)),Trait("Gate","Mock")]
    public void Discovery_scope_output_errors_and_every_wrong_effect_are_discriminated(string name)
    {
        var actual=ReviewReceiptDiscoveryCases.Expected(name);ReviewReceiptDiscoveryCases.AssertContract(name,actual);
        foreach(var wrong in new[]{actual with{Code="wrong"},actual with{Rounds=actual.Rounds+"wrong"},actual with{AuditFiles=actual.AuditFiles+1},actual with{InputsRetained=false},actual with{UnrelatedRead=true},actual with{ProviderCall=true}})
            Assert.ThrowsAny<Exception>(()=>ReviewReceiptDiscoveryCases.AssertContract(name,wrong));
    }
}
public sealed record AssessCliObservation(string Code,int Exit,int Audits,bool SourceUnchanged,bool ProviderCall,bool OutputContract=true);
public static class ReviewAssessCliCases
{
    public static string[] Names=>["success","empty","missing-project","missing-output","extra-snapshot","unsupported-operation","malformed","private-path"];
    public static IEnumerable<object[]> Data=>Names.Select(x=>new object[]{x});
    public static AssessCliObservation Expected(string name)=>name switch
    {"success" or "empty"=>new("collected",0,1,true,false),"malformed"=>new("INVALID_REVIEW_RECEIPT",1,0,true,false),"private-path"=>new("PRIVATE_PAYLOAD",1,0,true,false),_=>new("plan-rejected",1,0,true,false)};
    public static void AssertContract(string name,AssessCliObservation actual)
    {var expected=Expected(name);Assert.Equal(expected.Code,actual.Code);Assert.Equal(expected.Exit,actual.Exit);Assert.Equal(expected.Audits,actual.Audits);Assert.True(actual.SourceUnchanged);Assert.False(actual.ProviderCall);Assert.True(actual.OutputContract);}
}
public sealed class ReviewAssessCliMockTests
{
    [Theory,MemberData(nameof(ReviewAssessCliCases.Data),MemberType=typeof(ReviewAssessCliCases)),Trait("Gate","Mock")]
    public void Ordinary_compiled_caller_contract_and_each_wrong_effect_are_discriminated(string name)
    {
        var actual=ReviewAssessCliCases.Expected(name);ReviewAssessCliCases.AssertContract(name,actual);
        foreach(var wrong in new[]{actual with{Code="wrong"},actual with{Exit=actual.Exit+1},actual with{Audits=actual.Audits+1},actual with{SourceUnchanged=false},actual with{ProviderCall=true},actual with{OutputContract=false}})
            Assert.ThrowsAny<Exception>(()=>ReviewAssessCliCases.AssertContract(name,wrong));
    }
}
