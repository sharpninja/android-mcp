using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Modules.Review;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Review;

public sealed class ReviewLegacyCallerNativeTests
{
    [Theory,MemberData(nameof(ReviewLegacyCallerCases.Data),MemberType=typeof(ReviewLegacyCallerCases))]
    public async Task Same_legacy_caller_packets_bind_to_existing_prepare_launch_replay_with_fake_process_only(string name)
    {
        var fixture=new ReviewFixture();ReviewRunResult? result=null;var status="";var prior=true;var coverage=true;
        try
        {
            var mode=name switch{"preflight-duplicate-coverage"=>"coverage-omission","preflight-scope-leak"=>"scope-leak","preflight-missing-unit"=>"missing-unit-trx","preflight-missing-requirements"=>"requirements-open","preflight-private-payload"=>"json-credential",_=>"ready"};
            var prepared=await fixture.PrepareAsync(mode);
            if(name=="caller-draft-gate")fixture.Ready.Value=fixture.Ready.Value with{IsDraft=true};
            if(name=="caller-head-gate")fixture.Ready.Value=fixture.Ready.Value with{Head="changed"};
            if(name is "caller-process-uncertain" or "caller-active-resume")fixture.Runner.ThrowOnCall=true;
            void Reply(string text,string reason="end_turn",bool error=false,string? terminal=null,int? code=null,string model=ReviewLaunchService.Model)
                =>fixture.Runner.Replies.Enqueue(ReviewFixture.Response(text,reason,error,terminal,code,model));
            switch(name)
            {
                case "caller-output-continuation":Reply("first material finding","max_tokens");Reply("done END_REVIEW_fixture");break;
                case "caller-many-continuations":for(var index=1;index<=12;index++)Reply("finding"+index.ToString("D2"),"max_tokens");Reply("done END_REVIEW_fixture");break;
                case "caller-split-json":
                    var fragments=new[]{"{\"report_complete\":tr","ue}"};
                    var stream=string.Join('\n',fragments.Select(x=>JsonSerializer.Serialize(new{type="assistant",message=new{model=ReviewLaunchService.Model,effort=ReviewLaunchService.Effort,content=new[]{new{type="text",text=x}}}})))+"\n"+JsonSerializer.Serialize(new{type="result",stop_reason="end_turn",is_error=false})+"\n";
                    fixture.Runner.Replies.Enqueue(Encoding.UTF8.GetBytes(stream));break;
                case "caller-partial-json":Reply("{\"finding\":\"retained\",","max_tokens");Reply("done END_REVIEW_fixture");break;
                case "caller-provider-denial":Reply("provider partial",error:true,terminal:"authentication_error",code:401);break;
                case "caller-context-unified":Reply("context partial",error:true,terminal:"prompt_too_long",code:400);Reply("left finding END_REVIEW_fixture");Reply("right finding END_REVIEW_fixture");Reply("unified END_REVIEW_fixture");break;
                case "caller-malformed-uncertain":fixture.Runner.Replies.Enqueue(Encoding.UTF8.GetBytes("invalid-event-json\n"));break;
                case "caller-unhandled-exit":Reply("partial retained");fixture.Runner.ExitCodes.Enqueue(3);break;
                case "caller-model-mismatch":Reply("done END_REVIEW_fixture",model:"wrong-model");break;
                default:Reply("done END_REVIEW_fixture");break;
            }
            result=await fixture.Launcher.LaunchAsync(prepared,fixture.Capability,"/tmp/review-artifacts","/tmp/review-work");
            if(name is "caller-terminal-resume" or "caller-active-resume")
            {
                var before=fixture.Runner.Calls.Count;
                fixture.Runner.ThrowOnCall=false;
                result=await fixture.Launcher.ResumeAsync(prepared,fixture.Capability,"/tmp/review-artifacts","/tmp/review-work");
                Assert.Equal(before,fixture.Runner.Calls.Count);
            }
            status=result.Status;coverage=result.Coverage.SequenceEqual(prepared.Snapshot.Evidence.Select(x=>x.Path))&&result.Unified;
            if(name=="caller-output-continuation"||name=="caller-partial-json")
            {
                var next=Encoding.UTF8.GetString(fixture.Store.Values["reviews/fixture/calls/00000002/prompt.txt"]);
                prior=next.Contains(name=="caller-output-continuation"?"first material finding":"{\"finding\":\"retained\",",StringComparison.Ordinal);
            }
            if(name=="caller-many-continuations")
            {
                for(var call=2;call<=13;call++)
                {
                    var prompt=Encoding.UTF8.GetString(fixture.Store.Values[$"reviews/fixture/calls/{call:D8}/prompt.txt"]);
                    for(var previous=1;previous<call;previous++)prior&=prompt.Contains("finding"+previous.ToString("D2"),StringComparison.Ordinal);
                }
            }
            if(name=="caller-context-unified")
            {
                var final=Encoding.UTF8.GetString(fixture.Store.Values["reviews/fixture/calls/00000004/prompt.txt"]);
                prior=final.Contains("context partial",StringComparison.Ordinal)&&final.Contains("left finding",StringComparison.Ordinal)&&final.Contains("right finding",StringComparison.Ordinal);
                foreach(var child in new[]{2,3})prior&=Encoding.UTF8.GetString(fixture.Store.Values[$"reviews/fixture/calls/{child:D8}/prompt.txt"]).Contains("PARTIAL EVIDENCE PASS",StringComparison.Ordinal);
            }
        }
        catch(ReviewException error){status=error.Code;result=null;}
        var capFree=fixture.Runner.Calls.All(call=>
            !call.Arguments.Any(x=>x.Split('=')[0] is "--max-turns" or "--max-budget-usd" or "--timeout" or "--max-time" or "--max-duration" or "--fallback-model"||x.Contains("skip-permission",StringComparison.Ordinal))&&
            !call.PublicEnvironment.Any(x=>x.Key.StartsWith("HV_",StringComparison.Ordinal)&&(x.Key.Contains("MAX_",StringComparison.Ordinal)||x.Key.Contains("DEADLINE",StringComparison.Ordinal)||x.Key.Contains("TIMEOUT",StringComparison.Ordinal)||x.Key.Contains("BUDGET",StringComparison.Ordinal)))&&
            call.Arguments[call.Arguments.ToList().IndexOf("--tools")+1]==""&&call.StandardInputPath is not null);
        var visible=fixture.Store.Values.Keys.Count(x=>x.EndsWith("/response.visible.jsonl",StringComparison.Ordinal));
        var audit=result is not null&&fixture.Audit.FullReport is not null&&fixture.Audit.FullReport==result.Report;
        ReviewLegacyCallerCases.AssertContract(name,new(status,result?.Report??"",fixture.Runner.Calls.Count,prior,coverage,capFree,visible,audit));
    }
}
