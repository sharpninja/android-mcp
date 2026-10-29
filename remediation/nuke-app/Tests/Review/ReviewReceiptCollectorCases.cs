using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Review;

public sealed record ReceiptCollectorObservation(string Code,string Rows,int Writes,int ForbiddenReads,bool AuthorityClaim,int ProviderCalls);
public static class ReviewReceiptCollectorCases
{
    public static string[] Names=>["complete","unfinished","empty-completion","nonzero-exit","first-request-line","file-path-fallback","prompt-hash","manifest-hash","input-hash","hidden","response-hash","derived-priority","derived-fallback","first-init","invalid-request","invalid-visible","invalid-derived","invalid-input-path","private-input-path","missing-artifact","duplicate-manifest-input","batch-order","batch-failure","changed-independent-pin","outer-unknown-field","outer-pin-hash","outer-private-path"];
    public static IEnumerable<object[]> Data=>Names.Select(x=>new object[]{x});
    public static string Code(string name)=>name switch
    {
        "prompt-hash" or "batch-failure"=>"REQUEST_PROMPT_HASH_MISMATCH","manifest-hash"=>"MANIFEST_HASH_MISMATCH","input-hash"=>"FROZEN_INPUT_HASH_MISMATCH",
        "hidden"=>"HIDDEN_REASONING_PRESENT","response-hash"=>"VISIBLE_RESPONSE_HASH_MISMATCH","invalid-request" or "invalid-visible" or "invalid-derived" or "outer-unknown-field"=>"INVALID_REVIEW_RECEIPT",
        "invalid-input-path"=>"INVALID_PATH","private-input-path" or "outer-private-path"=>"PRIVATE_PAYLOAD","missing-artifact"=>"REVIEW_RECEIPT_MISSING","changed-independent-pin" or "outer-pin-hash"=>"ARTIFACT_PIN_HASH",_=>"collected"
    };
    public static string ExpectedRows(string name)
    {
        if(Code(name)!="collected")return "[]";
        object Row(string round)=>new{round,input_files=name=="duplicate-manifest-input"?2:1,input_hashes_match=true,request_hashes_match=true,hidden_reasoning_excluded=true,
            returned_model=name=="first-init"?"first-recorded-model":"recorded-model",returned_tools=new[]{name=="first-init"?"FirstTool":"ReadOnly"},
            requested_model="historical-requested-model",requested_effort="historical-requested-effort",finished=name!="unfinished",
            exit_code=name is "unfinished" or "empty-completion"?(int?)null:name=="nonzero-exit"?7:0,
            complete_derived_report=name is not ("unfinished" or "derived-priority"),
            parsed_report=name=="unfinished"?null:"reviews/"+round+"/"+(name=="derived-fallback"?"review-report.reassembled.json":"review-report.json")};
        var rows=name=="batch-order"?new[]{Row("round-001"),Row("round-002")}:new[]{Row("round-001")};
        return Encoding.UTF8.GetString(new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(rows)));
    }
    public static void AssertContract(string name,ReceiptCollectorObservation actual)
    {
        Assert.Equal(Code(name),actual.Code);Assert.Equal(ExpectedRows(name),actual.Rows);Assert.Equal(Code(name)=="collected"?1:0,actual.Writes);
        Assert.Equal(0,actual.ForbiddenReads);Assert.False(actual.AuthorityClaim);Assert.Equal(0,actual.ProviderCalls);
    }
}
public sealed class ReviewReceiptCollectorMockTests
{
    [Theory,MemberData(nameof(ReviewReceiptCollectorCases.Data),MemberType=typeof(ReviewReceiptCollectorCases)),Trait("Gate","Mock")]
    public void Original_collector_rows_hash_errors_priority_and_each_wrong_effect_are_discriminated(string name)
    {
        var code=ReviewReceiptCollectorCases.Code(name);var expected=new ReceiptCollectorObservation(code,ReviewReceiptCollectorCases.ExpectedRows(name),code=="collected"?1:0,0,false,0);
        Func<string,ReceiptCollectorObservation> boundary=input=>input==name?expected:throw new ArgumentException(input);
        var actual=boundary(name);ReviewReceiptCollectorCases.AssertContract(name,actual);
        foreach(var wrong in new[]{actual with{Code="wrong"},actual with{Rows=actual.Rows+"changed"},actual with{Writes=actual.Writes+1},actual with{ForbiddenReads=1},actual with{AuthorityClaim=true},actual with{ProviderCalls=1}})
            Assert.ThrowsAny<Exception>(()=>ReviewReceiptCollectorCases.AssertContract(name,wrong));
    }
}
