using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record OriginalDesignTextEvidence(string Design,string Code,string Dsl,DocumentationProjection Ui);
public static class LegacyDesign018Methods
{
    public static readonly string[] Names=["test_callback_state_machine_has_terminal_cleanup_semantics","test_inflight_distinguishes_admission_from_dispatch",
        "test_direct_event_extension_has_negotiation_and_fallback","test_models_include_arc_and_owner_sequences","test_all_screen_definitions_include_status_chip"];
    public static OriginalDesignTextEvidence Correct=>new(DocumentationFixture.Utf8(ModelContractFixture.Current.Design),
        DocumentationFixture.Utf8(ModelContractFixture.Current.Code),DocumentationFixture.Utf8(ModelContractFixture.Current.Dsl),DocumentationFixture.Current.Correct);
    public static void AssertOriginal(string method,OriginalDesignTextEvidence evidence)
    {
        switch(method)
        {
            case "test_callback_state_machine_has_terminal_cleanup_semantics":
                foreach(var trigger in new[]{"target_died","lease_expired","registration_timeout","cleanup_disposition"})Assert.Contains(trigger,evidence.Design,StringComparison.Ordinal);
                Assert.Contains("+onRegistrationEndpointDied(token)",evidence.Code,StringComparison.Ordinal);break;
            case "test_inflight_distinguishes_admission_from_dispatch":
                Assert.Contains("admitted but not dispatched",evidence.Design,StringComparison.Ordinal);
                Assert.Contains("dispatched unary",evidence.Design,StringComparison.Ordinal);
                Assert.DoesNotContain("a contract, capability or authority change revalidates or rejects it",evidence.Design,StringComparison.Ordinal);break;
            case "test_direct_event_extension_has_negotiation_and_fallback":
                foreach(var value in new[]{"android.bridge.directEvents.v1","/v1/subscriptions/{subscription_id}/events","DirectStreamNegotiator"})
                    Assert.Contains(value,evidence.Design+evidence.Code,StringComparison.Ordinal);break;
            case "test_models_include_arc_and_owner_sequences":
                Assert.Contains("dynamic bridge.app ArcSession",evidence.Dsl,StringComparison.Ordinal);
                Assert.Contains("dynamic bridge.app OwnerTransition",evidence.Dsl,StringComparison.Ordinal);break;
            case "test_all_screen_definitions_include_status_chip":
                foreach(var artifact in ScreenFiles(evidence.Ui))
                {
                    var id=Path.GetFileNameWithoutExtension(artifact.RelativePath);
                    Assert.Contains("data-status-id=\""+id+".STATUS\"",DocumentationFixture.Utf8(artifact),StringComparison.Ordinal);
                }
                Assert.True(JsonNode.Parse(DocumentationFixture.Content(evidence.Ui,"docs/ui-asset-manifest.json"))!.AsObject().Count>0);break;
            default:throw new InvalidOperationException("Unknown exact original design018 method.");
        }
    }
    private static IEnumerable<Artifact> ScreenFiles(DocumentationProjection projection)=>projection.Artifacts.Where(artifact=>
        artifact.RelativePath.StartsWith("ui-svg/SCR",StringComparison.Ordinal)&&artifact.RelativePath.EndsWith(".svg",StringComparison.Ordinal)&&
        !artifact.RelativePath["ui-svg/".Length..].Contains('/'));
    public static readonly (int Method,string Field,string Token)[] Mutations=[
        (0,"design","target_died"),(0,"design","lease_expired"),(0,"design","registration_timeout"),(0,"design","cleanup_disposition"),(0,"code","+onRegistrationEndpointDied(token)"),
        (1,"design","admitted but not dispatched"),(1,"design","dispatched unary"),(1,"forbidden","a contract, capability or authority change revalidates or rejects it"),
        (2,"both","android.bridge.directEvents.v1"),(2,"both","/v1/subscriptions/{subscription_id}/events"),(2,"both","DirectStreamNegotiator"),
        (3,"dsl","dynamic bridge.app ArcSession"),(3,"dsl","dynamic bridge.app OwnerTransition"),(4,"status","status"),(4,"manifest","manifest")
    ];
    public static OriginalDesignTextEvidence Wrong(string field,string token)
    {
        var value=Correct;
        if(field=="forbidden")return value with{Design=value.Design+token};
        if(field=="manifest")return value with{Ui=DocumentationFixture.Change(value.Ui,"docs/ui-asset-manifest.json",_=>"{}")};
        if(field=="status")
        {
            var artifact=ScreenFiles(value.Ui).First();var id=Path.GetFileNameWithoutExtension(artifact.RelativePath);
            return value with{Ui=DocumentationFixture.Change(value.Ui,artifact.RelativePath,text=>
            {var literal="data-status-id=\""+id+".STATUS\"";Assert.Contains(literal,text,StringComparison.Ordinal);return text.Replace(literal,"data-status-id=\"WRONG\"",StringComparison.Ordinal);})};
        }
        var original=field switch{"design"=>value.Design,"code"=>value.Code,"dsl"=>value.Dsl,"both"=>value.Design+value.Code,_=>throw new InvalidOperationException("Unknown mutation")};
        Assert.Contains(token,original,StringComparison.Ordinal);
        return field switch{
            "design"=>value with{Design=value.Design.Replace(token,"REMOVED",StringComparison.Ordinal)},
            "code"=>value with{Code=value.Code.Replace(token,"REMOVED",StringComparison.Ordinal)},
            "dsl"=>value with{Dsl=value.Dsl.Replace(token,"REMOVED",StringComparison.Ordinal)},
            "both"=>value with{Design=value.Design.Replace(token,"REMOVED",StringComparison.Ordinal),Code=value.Code.Replace(token,"REMOVED",StringComparison.Ordinal)},
            _=>throw new InvalidOperationException("Unknown mutation")
        };
    }
}
[Trait("Gate","Mock"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class LegacyDesign018MethodMockTests
{
    public static IEnumerable<object[]> Methods=>LegacyDesign018Methods.Names.Select(method=>new object[]{method});
    [Theory,MemberData(nameof(Methods))]
    public void CorrectIndependentSourceMockPassesExactOriginalPredicates(string method)=>LegacyDesign018Methods.AssertOriginal(method,LegacyDesign018Methods.Correct);
    public static IEnumerable<object[]> Mutations=>LegacyDesign018Methods.Mutations.Select(value=>new object[]{LegacyDesign018Methods.Names[value.Method],value.Field,value.Token});
    [Theory,MemberData(nameof(Mutations))]
    public void EveryOriginalTextOrScreenPredicateRejectsItsOwnWrongValue(string method,string field,string token)
    {
        var wrong=LegacyDesign018Methods.Wrong(field,token);
        Assert.NotNull(Record.Exception(()=>LegacyDesign018Methods.AssertOriginal(method,wrong)));
    }
    [Fact]
    public void OriginalScreenGlobIsNonrecursiveAndDoesNotRequireAnInventedScreenCount()
    {
        var value=LegacyDesign018Methods.Correct;
        var ignored=value.Ui.Artifacts.Where(artifact=>!artifact.RelativePath.StartsWith("ui-svg/SCR",StringComparison.Ordinal)).ToList();
        ignored.Add(new("ui-svg/nested/SCRextra.svg","<svg/>"u8.ToArray(),"image/svg+xml"));
        ignored.Add(new("ui-svg/Other.svg","<svg/>"u8.ToArray(),"image/svg+xml"));
        LegacyDesign018Methods.AssertOriginal(LegacyDesign018Methods.Names[4],value with{Ui=new(ignored)});
    }
}
