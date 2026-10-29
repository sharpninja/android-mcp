using Xunit;
namespace AndroidMcp.BuildTool.Tests.Review;
public sealed record LegacyCallerObservation(string Status,string Report,int Calls,bool PriorPreserved,bool CoverageComplete,bool CapFree,int VisibleReceipts,bool AuditWholeReport);
public static class ReviewLegacyCallerCases
{
    public static string[] Names=>["caller-final","caller-output-continuation","caller-many-continuations","caller-split-json","caller-partial-json","caller-provider-denial","caller-context-unified","caller-process-uncertain","caller-malformed-uncertain","caller-unhandled-exit","caller-model-mismatch","caller-active-resume","caller-terminal-resume","caller-draft-gate","caller-head-gate",
        "preflight-duplicate-coverage","preflight-scope-leak","preflight-missing-unit","preflight-missing-requirements","preflight-private-payload"];
    public static IEnumerable<object[]> Data=>Names.Select(x=>new object[]{x});
    public static string ManyReport=>string.Join("\n\n",new[]{"finding01","finding02","finding03","finding04","finding05","finding06","finding07","finding08","finding09","finding10","finding11","finding12","done END_REVIEW_fixture"});
    public static LegacyCallerObservation Expected(string name)=>name switch
    {
        "caller-final" or "caller-terminal-resume"=>new("complete","done END_REVIEW_fixture",1,true,true,true,1,true),
        "caller-output-continuation"=>new("complete","first material finding\n\ndone END_REVIEW_fixture",2,true,true,true,2,true),
        "caller-many-continuations"=>new("complete",ManyReport,13,true,true,true,13,true),
        "caller-split-json"=>new("complete","{\"report_complete\":tr\n\nue}",1,true,true,true,1,true),
        "caller-partial-json"=>new("complete","{\"finding\":\"retained\",\n\ndone END_REVIEW_fixture",2,true,true,true,2,true),
        "caller-provider-denial"=>new("paused-provider","provider partial",1,true,true,true,1,true),
        "caller-context-unified"=>new("complete","unified END_REVIEW_fixture",4,true,true,true,4,true),
        "caller-process-uncertain"=>new("outcome-uncertain","",1,true,true,true,0,true),
        "caller-malformed-uncertain"=>new("outcome-uncertain","",1,true,true,true,1,true),
        "caller-unhandled-exit"=>new("outcome-uncertain","partial retained",1,true,true,true,1,true),
        "caller-model-mismatch"=>new("MODEL_MISMATCH","",1,true,true,true,1,false),
        "caller-active-resume"=>new("OUTCOME_UNCERTAIN","",1,true,true,true,0,false),
        "caller-draft-gate"=>new("PR_NOT_READY","",0,true,true,true,0,false),
        "caller-head-gate"=>new("HEAD_MISMATCH","",0,true,true,true,0,false),
        "preflight-duplicate-coverage"=>new("COVERAGE_INCOMPLETE","",0,true,true,true,0,false),
        "preflight-scope-leak"=>new("SCOPE_VIOLATION","",0,true,true,true,0,false),
        "preflight-missing-unit"=>new("TEST_EVIDENCE_INCOMPLETE","",0,true,true,true,0,false),
        "preflight-missing-requirements"=>new("REQUIREMENTS_INCOMPLETE","",0,true,true,true,0,false),
        "preflight-private-payload"=>new("PRIVATE_PAYLOAD","",0,true,true,true,0,false),_=>throw new ArgumentException(name)
    };
    public static void AssertContract(string name,LegacyCallerObservation actual)
    {
        var expected=Expected(name);Assert.Equal(expected.Status,actual.Status);Assert.Equal(expected.Report,actual.Report);Assert.Equal(expected.Calls,actual.Calls);
        Assert.Equal(expected.PriorPreserved,actual.PriorPreserved);Assert.Equal(expected.CoverageComplete,actual.CoverageComplete);Assert.Equal(expected.CapFree,actual.CapFree);
        Assert.Equal(expected.VisibleReceipts,actual.VisibleReceipts);Assert.Equal(expected.AuditWholeReport,actual.AuditWholeReport);
    }
}
public sealed class ReviewLegacyCallerMockTests
{
    [Theory,MemberData(nameof(ReviewLegacyCallerCases.Data),MemberType=typeof(ReviewLegacyCallerCases)),Trait("Gate","Mock")]
    public void Source_derived_caller_outputs_and_each_wrong_effect_are_discriminated(string name)
    {
        var expected=ReviewLegacyCallerCases.Expected(name);Func<string,LegacyCallerObservation> scripted=input=>input==name?expected:throw new ArgumentException(input);
        var actual=scripted(name);ReviewLegacyCallerCases.AssertContract(name,actual);
        foreach(var wrong in new[]{actual with{Status="wrong"},actual with{Report=actual.Report+"changed"},actual with{Calls=actual.Calls+1},actual with{PriorPreserved=!actual.PriorPreserved},actual with{CoverageComplete=!actual.CoverageComplete},actual with{CapFree=!actual.CapFree},actual with{VisibleReceipts=actual.VisibleReceipts+1},actual with{AuditWholeReport=!actual.AuditWholeReport}})
            Assert.ThrowsAny<Exception>(()=>ReviewLegacyCallerCases.AssertContract(name,wrong));
    }
}
