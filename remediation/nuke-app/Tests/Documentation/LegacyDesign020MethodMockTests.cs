using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public static class LegacyDesign020Methods
{
    public static readonly string[] Names=["test_arc_authentication_is_explicitly_an_adapter_to_shared_registry","test_component_writes_converge_on_single_sqlite_coordinator",
        "test_owner_log_read_and_export_have_both_model_and_code_path","test_unknown_callback_late_result_and_cancel_are_closed_in_contract",
        "test_nonresumable_stage_expiry_has_one_policy","test_migration_precedes_live_schema","test_screen_tables_and_story_actions_resolve_to_declared_ids",
        "test_self_adb_pairing_is_an_explicit_story_not_a_grant"];
    public static OriginalDesignTextEvidence Correct
    {
        get
        {
            var design="late confirmed registration result\nlate confirmed failure\nlate success after cancel\ncallback event\ncallback.cancel without\n"+
                "No resumable-stage mode is selected\nMigration precedes the live CHECK\n[SCR09](screens/SCR09.svg)\n[STB06](screens/STB06.svg)";
            var dsl="bridge.app.arc -> bridge.app.auth\nbridge.app.storage -> bridge.db\nbridge.app.ownerCommands -> bridge.app.audit";
            var code="ArcAuthenticator ..> ClientAuthenticator\n+exportApprovedSnapshot\n+queryRedactedSnapshot";
            var manifest=new JsonObject{["screens"]=new JsonArray(new JsonObject{["id"]="SCR09",["controls"]=new JsonObject{["SCR09.C01"]="Review"},["states"]=new JsonObject{["SCR09.S0"]="Paused"}}),
                ["storyboards"]=new JsonArray(new JsonObject{["id"]="STB06",["boundary"]="Pairing does not grant control",["steps"]=new JsonArray(new JsonObject{["screen_id"]="SCR09",["control_id"]="SCR09.C01",["state_id"]="SCR09.S0"})})};
            return new(design,code,dsl,new([new Artifact("docs/ui-asset-manifest.json",System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(manifest),"application/json")]));
        }
    }
    private static JsonObject Manifest(OriginalDesignTextEvidence value)=>JsonNode.Parse(DocumentationFixture.Content(value.Ui,"docs/ui-asset-manifest.json"))!.AsObject();
    public static void AssertOriginal(string method,OriginalDesignTextEvidence value)
    {
        switch(Array.IndexOf(Names,method)){
            case 0:Assert.Contains("ArcAuthenticator ..> ClientAuthenticator",value.Code,StringComparison.Ordinal);Assert.Contains("bridge.app.arc -> bridge.app.auth",value.Dsl,StringComparison.Ordinal);break;
            case 1:Assert.Equal(new[]{"storage"},Regex.Matches(value.Dsl,@"bridge\.app\.(\w+)\s*->\s*bridge\.db").Select(match=>match.Groups[1].Value));break;
            case 2:
                Assert.Contains("bridge.app.ownerCommands -> bridge.app.audit",value.Dsl,StringComparison.Ordinal);
                Assert.Contains("+exportApprovedSnapshot",value.Code,StringComparison.Ordinal);Assert.Contains("+queryRedactedSnapshot",value.Code,StringComparison.Ordinal);break;
            case 3:
                foreach(var token in new[]{"late confirmed registration result","late confirmed failure","late success after cancel","callback event","callback.cancel without"})Assert.Contains(token,value.Design,StringComparison.Ordinal);break;
            case 4:
                Assert.DoesNotContain("retaining only an explicitly resumable stage",value.Design,StringComparison.Ordinal);Assert.Contains("No resumable-stage mode is selected",value.Design,StringComparison.Ordinal);break;
            case 5:
                Assert.Contains("Migration precedes the live CHECK",value.Design,StringComparison.Ordinal);Assert.DoesNotContain("names are compatibility labels",value.Design,StringComparison.Ordinal);break;
            case 6:
                var manifest=Manifest(value);var screens=new Dictionary<string,JsonObject>(StringComparer.Ordinal);
                // Original dict comprehension is last-wins, not an invented
                // duplicate-ID rejection at this assertion boundary.
                foreach(var screen in manifest["screens"]!.AsArray())screens[screen!["id"]!.GetValue<string>()]=screen.AsObject();
                foreach(var item in manifest["screens"]!.AsArray().Concat(manifest["storyboards"]!.AsArray())){
                    var id=item!["id"]!.GetValue<string>();Assert.Contains("["+id+"](screens/"+id+".svg)",value.Design,StringComparison.Ordinal);
                }
                foreach(var story in manifest["storyboards"]!.AsArray())foreach(var step in story!["steps"]!.AsArray()){
                    var screen=screens[step!["screen_id"]!.GetValue<string>()];Assert.True(screen["controls"]!.AsObject().ContainsKey(step["control_id"]!.GetValue<string>()));
                    var stateId=step["state_id"]!.GetValue<string>();var states=screen["states"]!;
                    Assert.True(states switch{
                        JsonObject stateMap=>stateMap.ContainsKey(stateId),
                        JsonArray stateList=>stateList.Any(state=>state is JsonValue item&&item.TryGetValue<string>(out var text)&&text==stateId),
                        JsonValue scalar when scalar.TryGetValue<string>(out var text)=>text.Contains(stateId,StringComparison.Ordinal),
                        _=>throw new InvalidOperationException("Original state membership requires a container")});
                }break;
            case 7:
                var storyboard=Manifest(value)["storyboards"]!.AsArray().FirstOrDefault(story=>story!["id"]!.GetValue<string>()=="STB06");Assert.NotNull(storyboard);
                Assert.Contains("does not grant",storyboard["boundary"]!.GetValue<string>(),StringComparison.Ordinal);
                Assert.Contains(storyboard["steps"]!.AsArray(),step=>step!["screen_id"]!.GetValue<string>()=="SCR09");break;
            default:throw new InvalidOperationException("Unknown original design020 method");
        }
    }
    public static IEnumerable<object[]> Mutations
    {
        get
        {
            yield return new object[]{0,"code","ArcAuthenticator ..> ClientAuthenticator"};yield return new object[]{0,"dsl","bridge.app.arc -> bridge.app.auth"};
            foreach(var kind in new[]{"writer-missing","writer-extra","writer-owner"})yield return new object[]{1,kind,""};
            yield return new object[]{2,"dsl","bridge.app.ownerCommands -> bridge.app.audit"};
            foreach(var token in new[]{"+exportApprovedSnapshot","+queryRedactedSnapshot"})yield return new object[]{2,"code",token};
            foreach(var token in new[]{"late confirmed registration result","late confirmed failure","late success after cancel","callback event","callback.cancel without"})yield return new object[]{3,"design",token};
            yield return new object[]{4,"forbidden","retaining only an explicitly resumable stage"};yield return new object[]{4,"design","No resumable-stage mode is selected"};
            yield return new object[]{5,"design","Migration precedes the live CHECK"};yield return new object[]{5,"forbidden","names are compatibility labels"};
            foreach(var token in new[]{"[SCR09](screens/SCR09.svg)","[STB06](screens/STB06.svg)"})yield return new object[]{6,"design",token};
            yield return new object[]{6,"control",""};yield return new object[]{6,"state",""};
            foreach(var kind in new[]{"missing-story","grant-boundary","wrong-screen"})yield return new object[]{7,kind,""};
        }
    }
    public static OriginalDesignTextEvidence Wrong(string kind,string token)
    {
        var value=Correct;
        if(kind=="code")return value with{Code=value.Code.Replace(token,"REMOVED",StringComparison.Ordinal)};
        if(kind=="dsl")return value with{Dsl=value.Dsl.Replace(token,"REMOVED",StringComparison.Ordinal)};
        if(kind=="design")return value with{Design=value.Design.Replace(token,"REMOVED",StringComparison.Ordinal)};
        if(kind=="forbidden")return value with{Design=value.Design+token};
        if(kind=="writer-missing")return value with{Dsl=value.Dsl.Replace("bridge.app.storage -> bridge.db","",StringComparison.Ordinal)};
        if(kind=="writer-extra")return value with{Dsl=value.Dsl+"\nbridge.app.other -> bridge.db"};
        if(kind=="writer-owner")return value with{Dsl=value.Dsl.Replace("bridge.app.storage -> bridge.db","bridge.app.other -> bridge.db",StringComparison.Ordinal)};
        var manifest=Manifest(value);var story=manifest["storyboards"]![0]!;var step=story["steps"]![0]!;
        switch(kind){
            case "control":step["control_id"]="MISSING";break;
            case "state":step["state_id"]="MISSING";break;
            case "missing-story":manifest["storyboards"]=new JsonArray();break;
            case "grant-boundary":story["boundary"]="grants authority";break;
            case "wrong-screen":step["screen_id"]="SCR01";break;
            default:throw new InvalidOperationException("Unknown original design020 mutation");
        }
        return value with{Ui=DocumentationFixture.Change(value.Ui,"docs/ui-asset-manifest.json",_=>manifest.ToJsonString())};
    }
}

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-005")]
public sealed class LegacyDesign020MethodMockTests
{
    public static IEnumerable<object[]> Methods=>LegacyDesign020Methods.Names.Select(method=>new object[]{method});
    [Theory,MemberData(nameof(Methods))]
    public void CorrectIndependentObservationsPassOriginalPredicates(string method)=>LegacyDesign020Methods.AssertOriginal(method,LegacyDesign020Methods.Correct);
    [Theory,MemberData(nameof(LegacyDesign020Methods.Mutations),MemberType=typeof(LegacyDesign020Methods))]
    public void EveryRequiredOriginalPredicateRejectsItsOwnWrongValue(int method,string kind,string token)=>
        Assert.NotNull(Record.Exception(()=>LegacyDesign020Methods.AssertOriginal(LegacyDesign020Methods.Names[method],LegacyDesign020Methods.Wrong(kind,token))));
    [Fact]
    public void OriginalStateMembershipPreservesContainerSemanticsButNeverMappingValues()
    {
        var value=LegacyDesign020Methods.Correct;var method=LegacyDesign020Methods.Names[6];
        LegacyDesign020Methods.AssertOriginal(method,value);
        OriginalDesignTextEvidence States(JsonNode states)=>value with{Ui=DocumentationFixture.Change(value.Ui,"docs/ui-asset-manifest.json",text=>{
            var manifest=JsonNode.Parse(text)!;manifest["screens"]![0]!["states"]=states;return manifest.ToJsonString();})};
        LegacyDesign020Methods.AssertOriginal(method,States(new JsonArray("SCR09.S0")));
        LegacyDesign020Methods.AssertOriginal(method,States(new JsonObject{["SCR09.S0"]=null}));
        LegacyDesign020Methods.AssertOriginal(method,States(new JsonArray(null,37,"SCR09.S0")));
        LegacyDesign020Methods.AssertOriginal(method,States(JsonValue.Create("prefix SCR09.S0 suffix")!));
        Assert.NotNull(Record.Exception(()=>LegacyDesign020Methods.AssertOriginal(method,States(new JsonObject{["OTHER"]="SCR09.S0"}))));
        Assert.NotNull(Record.Exception(()=>LegacyDesign020Methods.AssertOriginal(method,States(new JsonArray("OTHER")))));
    }
}
