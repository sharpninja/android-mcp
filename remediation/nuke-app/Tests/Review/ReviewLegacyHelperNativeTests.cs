using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Modules.Review;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Review;

public sealed class ReviewLegacyHelperNativeTests
{
    [Theory,MemberData(nameof(ReviewLegacyHelperCases.Data),MemberType=typeof(ReviewLegacyHelperCases))]
    public void Same_original_branch_packets_bind_to_existing_extractor_assessor_partition(string name)
    {
        LegacyHelperObservation actual;
        if(name.StartsWith("visible-",StringComparison.Ordinal))
        {
            var fragments=ReviewAssessService.VisibleText(VisibleEvents(name));
            actual=new("visible","",JsonSerializer.Serialize(fragments),"[]","",0);
        }
        else if(name.StartsWith("split-",StringComparison.Ordinal))
        {
            try{var parts=ReviewLaunchService.Partition(ReviewLegacyHelperCases.InputUnits(name));actual=new("partitioned","","[]",JsonSerializer.Serialize(parts),"",0);}
            catch(ReviewException error){actual=new(error.Code,"","[]","[]","",0);}
        }
        else
        {
            var events=AssessmentEvents(name);var text=string.Join('\n',events.Select(x=>x.ToJsonString()))+"\n";
            if(name=="assess-malformed")text+="malformed-json-fragment\n";
            if(name=="assess-array-event")text+="[]\n";
            var state=new ReviewAssessService(ReviewFixture.Hash).Assess(Encoding.UTF8.GetBytes(text),
                new(name!="assess-unknown-terminal",name=="assess-unhandled-exit"?3:0,name=="assess-cancelled"),"END");
            if(name=="assess-malformed")Assert.DoesNotContain("malformed-json-fragment",string.Join('\n',state.VisibleLines));
            actual=new(state.Status,state.Report,"[]","[]",state.Reason??"",0);
        }
        ReviewLegacyHelperCases.AssertContract(name,actual);
    }
    private static JsonObject Assistant(params string[] text)=>JsonSerializer.SerializeToNode(new{type="assistant",message=new{content=text.Select(x=>new{type="text",text=x})}})!.AsObject();
    private static JsonObject Result(string text)=>JsonSerializer.SerializeToNode(new{type="result",result=text})!.AsObject();
    private static IReadOnlyList<JsonObject> VisibleEvents(string name)=>name switch
    {
        "visible-block-order"=>[Assistant("alpha"),Assistant("beta")],"visible-embedded-separators"=>[Assistant("a\n\nb","c")],
        "visible-last-result-fallback"=>[Result("first"),Result("last")],"visible-empty-text-suppresses-fallback"=>[Assistant(""),Result("fallback")],
        "visible-hidden-and-tools-ignored"=>[JsonSerializer.SerializeToNode(new{type="assistant",message=new{content=new[]{new{type="thinking",text="private-thought"},new{type="tool_use",text="tool-content"}}}})!.AsObject(),Result("fallback")],
        "visible-missing-message"=>[new JsonObject{["type"]="assistant"}],"visible-empty-last-result"=>[Result("first"),Result("")],
        "visible-whitespace-fallback"=>[Result(" ")],_=>[]
    };
    private static IReadOnlyList<JsonObject> AssessmentEvents(string name)
    {
        string[] chunks=name switch
        {
            "assess-marker"=>["exact\nEND"],"assess-marker-backticks"=>["exact\nEND```   "],"assess-wrong-marker"=>["exact END_WRONG"],
            "assess-complete-json"=>["{\"report_complete\":true,\"finding\":\"kept\"}"],"assess-split-json"=>["{\"report_complete\":tr","ue,\"finding\":\"kept\"}"],
            "assess-fenced-json"=>["```json\n{\"report_complete\":true}\n```"],"assess-inline-fenced-json"=>["```json{\"report_complete\":true}```"],
            "assess-partial-json"=>["{\"finding\":\"retained\","],"assess-absent-stop" or "assess-denial-precedence"=>["exact END"],
            "assess-score-accepted"=>["{\"report_complete\":true,\"OverallVerdict\":\"AGREE\",\"accuracy\":98,\"completeness\":98,\"FAIL\":0,\"UNKNOWN\":0}"],
            "assess-score-rejected"=>["{\"report_complete\":true,\"OverallVerdict\":\"AGREE\",\"accuracy\":97,\"completeness\":98,\"FAIL\":0,\"UNKNOWN\":0}"],
            "assess-progress-suffix"=>["progress retained","{\"report_complete\":true}"],_=>["partial finding"]
        };
        var events=chunks.Select(x=>Assistant(x)).ToList();
        if(name=="assess-missing-result")return events;
        var error=name is "assess-context-reason" or "assess-context-object" or "assess-safety-error" or "assess-error";
        var result=JsonSerializer.SerializeToNode(new{type="result",stop_reason=name is "assess-partial-json" or "assess-output-boundary"?"max_tokens":"end_turn",is_error=error})!.AsObject();
        if(name=="assess-absent-stop")result.Remove("stop_reason");
        var reason=name switch{"assess-context-reason"=>"prompt_too_long","assess-auth401" or "assess-denial-precedence"=>"authentication_error","assess-permission403"=>"permission_denied","assess-rate429"=>"rate_limit","assess-billing"=>"billing","assess-quota"=>"quota","assess-refill"=>"refill_breaker",_=>""};
        if(reason!="")result["terminal_reason"]=reason;
        if(name=="assess-context-object")result["api_error"]=new JsonObject{["code"]="prompt_too_long"};
        if(name=="assess-auth401")result["api_error_status"]=401;
        if(name=="assess-permission403")result["api_error_status"]=403;
        if(name=="assess-rate429")result["api_error_status"]=429;
        if(name=="assess-permission-list")result["permission_denials"]=new JsonArray("denied");
        if(name is "assess-safety-error" or "assess-safety-nonerror")result["safety_stops"]=new JsonArray("safety");
        events.Add(result);return events;
    }
}
