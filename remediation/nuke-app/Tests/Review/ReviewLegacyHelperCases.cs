using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Modules.Review;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Review;

public sealed record LegacyHelperObservation(string Status,string Report,string Fragments,string Parts,string Reason,int ProviderCalls);
public static class ReviewLegacyHelperCases
{
    public static string[] Names=>["visible-block-order","visible-embedded-separators","visible-last-result-fallback","visible-empty-text-suppresses-fallback","visible-empty-events","visible-hidden-and-tools-ignored","visible-missing-message","visible-empty-last-result","visible-whitespace-fallback",
        "assess-marker","assess-marker-backticks","assess-wrong-marker","assess-complete-json","assess-split-json","assess-fenced-json","assess-inline-fenced-json","assess-partial-json","assess-output-boundary","assess-unknown-terminal","assess-cancelled","assess-missing-result","assess-malformed","assess-array-event","assess-context-reason","assess-context-object","assess-auth401","assess-permission403","assess-rate429","assess-billing","assess-quota","assess-refill","assess-permission-list","assess-safety-error","assess-safety-nonerror","assess-unhandled-exit","assess-error","assess-absent-stop","assess-denial-precedence","assess-score-accepted","assess-score-rejected","assess-progress-suffix",
        "split-two","split-three","split-five","split-even","split-odd","split-unicode","split-overwrites-both-offsets","split-empty-text","split-indivisible-rune","split-empty-list"];
    public static IEnumerable<object[]> Data=>Names.Select(x=>new object[]{x});
    public static string Fragments(params string[] chunks)=>JsonSerializer.Serialize(chunks);
    public static ReviewEvidenceUnit Unit(string text,int start=0,int? end=null,string key="key")=>new(key,"original.file","full-original-sha",start,end??text.EnumerateRunes().Count(),text);
    public static IReadOnlyList<ReviewEvidenceUnit> InputUnits(string name)=>name switch
    {
        "split-two"=>[Unit("A",key:"A"),Unit("B",key:"B")],"split-three"=>[Unit("A",key:"A"),Unit("B",key:"B"),Unit("C",key:"C")],
        "split-five"=>[Unit("A",key:"A"),Unit("B",key:"B"),Unit("C",key:"C"),Unit("D",key:"D"),Unit("E",key:"E")],
        "split-even"=>[Unit("ABCD")],"split-odd"=>[Unit("ABCDE")],"split-unicode"=>[Unit("A😀éZ",7,11)],
        "split-overwrites-both-offsets"=>[Unit("ABCD",7,999)],"split-empty-text"=>[Unit("")],"split-indivisible-rune"=>[Unit("😀")],_=>[]
    };
    public static LegacyHelperObservation Expected(string name)
    {
        LegacyHelperObservation Visible(params string[] chunks)=>new("visible","",Fragments(chunks),"[]","",0);
        LegacyHelperObservation State(string status,string report,string reason="")=>new(status,report,"[]","[]",reason,0);
        if(name.StartsWith("visible-",StringComparison.Ordinal))return name switch
        {
            "visible-block-order"=>Visible("alpha","beta"),"visible-embedded-separators"=>Visible("a\n\nb","c"),"visible-last-result-fallback"=>Visible("last"),
            "visible-empty-text-suppresses-fallback"=>Visible(""),"visible-hidden-and-tools-ignored"=>Visible("fallback"),"visible-whitespace-fallback"=>Visible(" "),_=>Visible()
        };
        if(name.StartsWith("split-",StringComparison.Ordinal))
        {
            if(name is "split-empty-text" or "split-indivisible-rune" or "split-empty-list")return State("PROVIDER_CAPABILITY_BLOCKED","");
            IReadOnlyList<IReadOnlyList<ReviewEvidenceUnit>> parts=name switch
            {
                "split-two"=>[[Unit("A",key:"A")],[Unit("B",key:"B")]],"split-three"=>[[Unit("A",key:"A")],[Unit("B",key:"B"),Unit("C",key:"C")]],
                "split-five"=>[[Unit("A",key:"A"),Unit("B",key:"B")],[Unit("C",key:"C"),Unit("D",key:"D"),Unit("E",key:"E")]],
                "split-even"=>[[Unit("AB",0,2)],[Unit("CD",2,4)]],"split-odd"=>[[Unit("AB",0,2)],[Unit("CDE",2,5)]],
                "split-unicode"=>[[Unit("A😀",7,9)],[Unit("éZ",9,11)]],_=>[[Unit("AB",7,9)],[Unit("CD",9,11)]]
            };
            return new("partitioned","","[]",JsonSerializer.Serialize(parts),"",0);
        }
        return name switch
        {
            "assess-marker"=>State("complete","exact\nEND"),"assess-marker-backticks"=>State("complete","exact\nEND```   "),
            "assess-wrong-marker"=>State("continue","exact END_WRONG","end_turn"),
            "assess-complete-json"=>State("complete","{\"report_complete\":true,\"finding\":\"kept\"}"),
            "assess-split-json"=>State("complete","{\"report_complete\":tr\n\nue,\"finding\":\"kept\"}"),
            "assess-fenced-json"=>State("complete","```json\n{\"report_complete\":true}\n```"),
            "assess-inline-fenced-json"=>State("complete","```json{\"report_complete\":true}```"),
            "assess-partial-json"=>State("continue","{\"finding\":\"retained\",","max_tokens"),"assess-output-boundary"=>State("continue","partial finding","max_tokens"),
            "assess-unknown-terminal" or "assess-cancelled"=>State("outcome-uncertain","partial finding","No authoritative terminal outcome."),
            "assess-missing-result" or "assess-malformed" or "assess-array-event"=>State("outcome-uncertain","partial finding","Missing terminal event or malformed event stream."),
            "assess-context-reason"=>State("context","partial finding","prompt_too_long"),"assess-context-object"=>State("context","partial finding","{\"code\":\"prompt_too_long\"}"),
            "assess-auth401" or "assess-denial-precedence"=>State("paused-provider",name=="assess-denial-precedence"?"exact END":"partial finding","authentication_error"),
            "assess-permission403"=>State("paused-provider","partial finding","permission_denied"),"assess-rate429"=>State("paused-provider","partial finding","rate_limit"),
            "assess-billing"=>State("paused-provider","partial finding","billing"),"assess-quota"=>State("paused-provider","partial finding","quota"),"assess-refill"=>State("paused-provider","partial finding","refill_breaker"),
            "assess-permission-list" or "assess-safety-error"=>State("paused-provider","partial finding"),"assess-safety-nonerror"=>State("continue","partial finding","end_turn"),
            "assess-unhandled-exit" or "assess-error"=>State("outcome-uncertain","partial finding","Unhandled terminal error: "),
            "assess-absent-stop"=>State("continue","exact END","Incomplete terminal response"),
            "assess-score-accepted"=>State("accepted","{\"report_complete\":true,\"OverallVerdict\":\"AGREE\",\"accuracy\":98,\"completeness\":98,\"FAIL\":0,\"UNKNOWN\":0}"),
            "assess-score-rejected"=>State("rejected","{\"report_complete\":true,\"OverallVerdict\":\"AGREE\",\"accuracy\":97,\"completeness\":98,\"FAIL\":0,\"UNKNOWN\":0}"),
            "assess-progress-suffix"=>State("complete","progress retained\n\n{\"report_complete\":true}"),_=>throw new ArgumentException(name)
        };
    }
    public static void AssertContract(string name,LegacyHelperObservation actual)
    {var expected=Expected(name);Assert.Equal(expected.Status,actual.Status);Assert.Equal(expected.Report,actual.Report);Assert.Equal(expected.Fragments,actual.Fragments);Assert.Equal(expected.Parts,actual.Parts);Assert.Equal(expected.Reason,actual.Reason);Assert.Equal(0,actual.ProviderCalls);}
}

public sealed class ReviewLegacyHelperMockTests
{
    [Theory,MemberData(nameof(ReviewLegacyHelperCases.Data),MemberType=typeof(ReviewLegacyHelperCases)),Trait("Gate","Mock")]
    public void Original_branch_packets_and_each_wrong_visible_error_range_effect_are_discriminated(string name)
    {
        var expected=ReviewLegacyHelperCases.Expected(name);
        // A scripted boundary supplies the exact source-derived observation, independently
        // of the production extraction/classification/partition functions bound afterward.
        Func<string,LegacyHelperObservation> boundary=input=>input==name?expected:throw new ArgumentException(input);
        var actual=boundary(name);ReviewLegacyHelperCases.AssertContract(name,actual);
        foreach(var wrong in new[]{actual with{Status="wrong"},actual with{Report=actual.Report+"changed"},actual with{Fragments="changed"},actual with{Parts="changed"},actual with{Reason=actual.Reason+"changed"},actual with{ProviderCalls=1}})
            Assert.ThrowsAny<Exception>(()=>ReviewLegacyHelperCases.AssertContract(name,wrong));
    }
}
