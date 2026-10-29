using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

// Literal predicates from test_design023_ui.py. These test assertions are a
// Python oracle, intentionally distinct from the CJS validator's JS truthiness.
public static class LegacyUiOriginalMethods
{
    public static readonly string[] Names=["test_current_control_and_state_contracts_are_explicit","test_markdown_is_single_canonical_behavior_source",
        "test_every_state_has_matching_vector_variant_and_exact_control_enablement","test_storyboard_images_match_named_state_and_branch_success_does_not_force_error",
        "test_every_rendered_state_health_uses_closed_domains_and_integer_revision"];
    public static JsonObject Manifest(DocumentationProjection projection)=>JsonNode.Parse(DocumentationFixture.Content(projection,"docs/ui-asset-manifest.json"))!.AsObject();
    private static IEnumerable<JsonObject> Screens(JsonObject manifest)=>manifest["screens"]!.AsArray().Select(value=>value!.AsObject());
    public static string ImagePath(string reference)
    {var root=Path.Combine(Path.GetTempPath(),"legacy-ui-virtual");return Path.GetRelativePath(root,Path.GetFullPath(reference,Path.Combine(root,"storyboards"))).Replace('\\','/');}
    private static bool Truthy(JsonNode? value)=>value is not null&&(value.GetValueKind() switch
    {JsonValueKind.False or JsonValueKind.Null=>false,JsonValueKind.String=>value.GetValue<string>().Length>0,
        JsonValueKind.Number=>double.Parse(value.ToJsonString(),NumberStyles.Float,CultureInfo.InvariantCulture)!=0,
        JsonValueKind.Array=>value.AsArray().Count>0,JsonValueKind.Object=>value.AsObject().Count>0,_=>true});
    public static void AssertMethod(string method,DocumentationProjection projection)
    {
        var manifest=Manifest(projection);var screens=Screens(manifest).ToArray();
        switch(method)
        {
            case "test_current_control_and_state_contracts_are_explicit":
                foreach(var screen in screens)
                {
                    foreach(var pair in screen["control_specs"]!.AsObject())
                    {
                        var control=pair.Value!.AsObject();
                        Assert.True(control.ContainsKey("behavior"));
                        Assert.False(JsonNode.DeepEquals(JsonValue.Create("Exact named action; protected mutations open SCR09; reads never change authority"),control["behavior"]));
                        foreach(var field in new[]{"fields","success","error","cancel"})Assert.True(Truthy(control[field]),pair.Key+":"+field);
                        var gates=control["gates"]!.AsObject();Assert.True(gates.ContainsKey("remote_press_allowed"));Assert.False(Truthy(gates["remote_press_allowed"]));
                    }
                    foreach(var pair in screen["state_specs"]!.AsObject())
                    {
                        var state=pair.Value!.AsObject();
                        Assert.False(JsonNode.DeepEquals(JsonValue.Create("Read-only until exact protected commit; error/conflict never applies a partial mutation."),state["effect"]));
                        Assert.True(Truthy(state["effect"]));Assert.True(Truthy(state["recovery"]));
                    }
                }
                break;
            case "test_markdown_is_single_canonical_behavior_source":
                Assert.True(manifest.Remove("state_svg_files"));Assert.True(JsonNode.DeepEquals(DocumentationFixture.Current.Blueprint,manifest));break;
            case "test_every_state_has_matching_vector_variant_and_exact_control_enablement":
                foreach(var screen in screens)foreach(var pair in screen["state_specs"]!.AsObject())
                {
                    var root=DocumentationFixture.Svg(projection,"docs/screens/states/"+pair.Key+".svg");var spec=pair.Value!.AsObject();
                    Assert.Equal(pair.Key,(string?)root.Attribute("data-screen-state"));
                    foreach(var flag in new[]{"enabled","disabled"})
                    {
                        var observed=root.DescendantsAndSelf().Where(element=>(string?)element.Attribute("data-enabled")== (flag=="enabled"?"true":"false"))
                            .Select(element=>(string?)element.Attribute("data-control-id")??"").ToHashSet(StringComparer.Ordinal);
                        Assert.True(observed.SetEquals(DocumentationFixture.Values(spec[flag+"_controls"])));
                    }
                }
                var stopped=DocumentationFixture.Content(projection,"docs/screens/states/SCR01.S3.svg");
                Assert.Contains("Service Stopped",stopped,StringComparison.Ordinal);Assert.DoesNotContain("Service Running",stopped,StringComparison.Ordinal);break;
            case "test_storyboard_images_match_named_state_and_branch_success_does_not_force_error":
                var steps=manifest["storyboards"]!.AsArray().SelectMany(story=>story!["steps"]!.AsArray()).Select(step=>step!.AsObject()).ToArray();
                var images=Regex.Matches(DocumentationFixture.Content(projection,"storyboards/storyboard.md"),@"!\[Screen]\(([^)]+)\)");
                Assert.Equal(steps.Length,images.Count);
                var index=new Dictionary<string,JsonObject>(StringComparer.Ordinal);foreach(var step in steps)index[DocumentationFixture.Text(step,"step_id")]=step;
                for(var position=0;position<steps.Length;position++)
                {
                    var step=steps[position];var relative=ImagePath(images[position].Groups[1].Value);
                    var root=DocumentationFixture.Svg(projection,relative);
                    Assert.Equal(DocumentationFixture.Text(step,"state_id"),(string?)root.Attribute("data-screen-state"));
                    Assert.Equal(DocumentationFixture.Text(step,"control_id"),(string?)root.Attribute("data-focus-control"));
                    if(DocumentationFixture.Text(step,"interaction")=="control")
                    {
                        var screen=screens.Single(value=>DocumentationFixture.Text(value,"id")==DocumentationFixture.Text(step,"screen_id"));
                        Assert.Contains(DocumentationFixture.Text(step,"control_id"),DocumentationFixture.Values(screen["state_specs"]![DocumentationFixture.Text(step,"state_id")]!["enabled_controls"]));
                    }
                }
                foreach(var step in steps)
                {
                    if(DocumentationFixture.Text(step,"branch")=="main"&&index.TryGetValue(DocumentationFixture.Text(step,"next_success"),out var next))Assert.Equal("main",DocumentationFixture.Text(next,"branch"));
                    foreach(var destination in step["error_branches"]!.AsObject().Select(pair=>pair.Value!.GetValue<string>()))Assert.True(index.ContainsKey(destination));
                }
                break;
            case "test_every_rendered_state_health_uses_closed_domains_and_integer_revision":
                foreach(var screen in screens)foreach(var state in screen["state_specs"]!.AsObject())
                {
                    var root=DocumentationFixture.Svg(projection,"docs/screens/states/"+state.Key+".svg");
                    var raw=root.DescendantsAndSelf().Select(element=>(string?)element.Attribute("data-health-snapshot")).First(value=>!string.IsNullOrEmpty(value));
                    var health=JsonNode.Parse(raw!)!.AsObject();
                    foreach(var domain in manifest["health_domains"]!.AsObject())Assert.Contains(domain.Value!.AsArray(),value=>JsonNode.DeepEquals(value,health[domain.Key]));
                    var revision=health["revision"]!;
                    // Python bool subclasses int; preserve that original predicate.
                    Assert.True(revision.GetValueKind() is JsonValueKind.True or JsonValueKind.False||revision.GetValueKind()==JsonValueKind.Number&&BigInteger.TryParse(revision.ToJsonString(),NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out _));
                    Assert.Contains(health["desired_state"]!.GetValue<string>(),new[]{"running","stopped"});
                }
                break;
            default:throw new InvalidOperationException("Unknown exact original method");
        }
    }
    public static DocumentationProjection ChangeManifest(DocumentationProjection projection,Action<JsonObject> change)=>DocumentationFixture.Change(projection,"docs/ui-asset-manifest.json",text=>
    {var value=JsonNode.Parse(text)!.AsObject();change(value);return value.ToJsonString();});
    public static DocumentationProjection Wrong(string method,string mutation)
    {
        var correct=DocumentationFixture.Current.Correct;
        if(method==Names[0])return ChangeManifest(correct,manifest=>
        {
            var screen=Screens(manifest).First();var control=screen["control_specs"]!.AsObject().First().Value!;
            if(mutation=="placeholder")control["behavior"]="Exact named action; protected mutations open SCR09; reads never change authority";
            else if(mutation=="remote")control["gates"]!["remote_press_allowed"]=true;
            else if(mutation=="missing-remote")control["gates"]!.AsObject().Remove("remote_press_allowed");
            else if(mutation=="missing-behavior")control.AsObject().Remove("behavior");
            else if(mutation=="state-template")screen["state_specs"]!.AsObject().First().Value!["effect"]="Read-only until exact protected commit; error/conflict never applies a partial mutation.";
            else if(mutation.StartsWith("state-",StringComparison.Ordinal))screen["state_specs"]!.AsObject().First().Value![mutation[6..]]="";
            else control[mutation]="";
        });
        if(method==Names[1])return ChangeManifest(correct,manifest=>{if(mutation=="missing-map")manifest.Remove("state_svg_files");else manifest["schema_version"]="wrong";});
        var firstState=DocumentationFixture.Current.Screens[0]["state_specs"]!.AsObject().First().Key;
        if(method==Names[2])
        {
            if(mutation=="stopped")return DocumentationFixture.Change(correct,"docs/screens/states/SCR01.S3.svg",text=>text.Replace("Service Stopped","Service Running",StringComparison.Ordinal));
            if(mutation is "enabled" or "disabled")firstState=DocumentationFixture.Current.Screens.SelectMany(screen=>screen["state_specs"]!.AsObject())
                .First(pair=>pair.Value![mutation+"_controls"]!.AsArray().Count>0).Key;
            var path="docs/screens/states/"+firstState+".svg";_=DocumentationFixture.Get(correct,path);
            return DocumentationFixture.ChangeSvg(correct,path,root=>
            {if(mutation is "enabled" or "disabled"){var flag=mutation=="enabled"?"true":"false";root.DescendantsAndSelf().First(element=>(string?)element.Attribute("data-enabled")==flag).SetAttributeValue("data-enabled",flag=="true"?"false":"true");}else root.SetAttributeValue("data-screen-state","wrong");});
        }
        if(method==Names[3])
        {
            if(mutation=="focus"||mutation=="step-state")
            {
                var image=Regex.Match(DocumentationFixture.Content(correct,"storyboards/storyboard.md"),@"!\[Screen]\(([^)]+)\)");
                Assert.True(image.Success);var path=ImagePath(image.Groups[1].Value);_=DocumentationFixture.Get(correct,path);
                return DocumentationFixture.ChangeSvg(correct,path,root=>root.SetAttributeValue(mutation=="focus"?"data-focus-control":"data-screen-state","wrong"));
            }
            if(mutation is "disabled-control" or "main-error" or "unknown-error")return ChangeManifest(correct,manifest=>
            {
                var steps=manifest["storyboards"]!.AsArray().SelectMany(story=>story!["steps"]!.AsArray()).Select(value=>value!.AsObject()).ToArray();
                if(mutation=="disabled-control"){
                    var step=steps.First(value=>DocumentationFixture.Text(value,"interaction")=="control");
                    var screen=Screens(manifest).Single(value=>DocumentationFixture.Text(value,"id")==DocumentationFixture.Text(step,"screen_id"));
                    var enabled=screen["state_specs"]![DocumentationFixture.Text(step,"state_id")]!["enabled_controls"]!.AsArray();
                    enabled.RemoveAt(enabled.Select((value,index)=>(value,index)).First(pair=>pair.value!.GetValue<string>()==DocumentationFixture.Text(step,"control_id")).index);
                }else if(mutation=="main-error"){
                    var main=steps.First(value=>DocumentationFixture.Text(value,"branch")=="main");
                    main["next_success"]=DocumentationFixture.Text(steps.First(value=>DocumentationFixture.Text(value,"branch")!="main"),"step_id");
                }else steps.First(value=>value["error_branches"]!.AsObject().Count>0)["error_branches"]!.AsObject().First().Value!.ReplaceWith("absent-target");
            });
            return DocumentationFixture.Change(correct,"storyboards/storyboard.md",text=>text.Replace("![Screen]","![Other]",StringComparison.Ordinal));
        }
        return DocumentationFixture.ChangeSvg(correct,"docs/screens/states/"+firstState+".svg",root=>
        {
            var element=root.DescendantsAndSelf().First(value=>value.Attribute("data-health-snapshot")is not null);var health=JsonNode.Parse(element.Attribute("data-health-snapshot")!.Value)!.AsObject();
            if(mutation=="domain")health[Manifest(correct)["health_domains"]!.AsObject().First().Key]="wrong-domain";
            else if(mutation=="desired")health["desired_state"]="paused";
            else if(mutation=="missing-health"){element.Attribute("data-health-snapshot")!.Remove();return;}
            else health["revision"]=1.5;
            element.SetAttributeValue("data-health-snapshot",health.ToJsonString());
        });
    }
}
[Trait("Gate","Mock"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class LegacyUiOriginalMethodMockTests
{
    public static IEnumerable<object[]> Methods=>LegacyUiOriginalMethods.Names.Select(name=>new object[]{name});
    [Theory,MemberData(nameof(Methods))]
    public void ExactOriginalMethodsAcceptIndependentPinnedOutput(string method)=>LegacyUiOriginalMethods.AssertMethod(method,DocumentationFixture.Current.Correct);
    [Theory,MemberData(nameof(Methods))]
    public void EachOriginalMethodRejectsItsIncorrectOutput(string method)
    {var wrong=LegacyUiOriginalMethods.Wrong(method,"fields");Assert.NotNull(Record.Exception(()=>LegacyUiOriginalMethods.AssertMethod(method,wrong)));}
    [Theory]
    [InlineData("placeholder")][InlineData("fields")][InlineData("success")][InlineData("error")][InlineData("cancel")][InlineData("remote")][InlineData("missing-remote")][InlineData("missing-behavior")][InlineData("state-effect")][InlineData("state-recovery")][InlineData("state-template")]
    public void EveryControlStatePredicateHasAnIndependentNegativeControl(string mutation)
    {var wrong=LegacyUiOriginalMethods.Wrong(LegacyUiOriginalMethods.Names[0],mutation);Assert.NotNull(Record.Exception(()=>LegacyUiOriginalMethods.AssertMethod(LegacyUiOriginalMethods.Names[0],wrong)));}
    [Theory]
    [InlineData(1,"missing-map")][InlineData(2,"enabled")][InlineData(2,"disabled")][InlineData(2,"stopped")]
    [InlineData(3,"focus")][InlineData(3,"step-state")][InlineData(3,"disabled-control")][InlineData(3,"main-error")][InlineData(3,"unknown-error")]
    [InlineData(4,"domain")][InlineData(4,"desired")][InlineData(4,"missing-health")]
    public void EveryRemainingOriginalPredicateRejectsIncorrectOutput(int index,string mutation)
    {var wrong=LegacyUiOriginalMethods.Wrong(LegacyUiOriginalMethods.Names[index],mutation);Assert.NotNull(Record.Exception(()=>LegacyUiOriginalMethods.AssertMethod(LegacyUiOriginalMethods.Names[index],wrong)));}
    [Fact]
    public void OriginalIgnoredManifestFieldHasNoAdditionalConstraint()
    {var changed=LegacyUiOriginalMethods.ChangeManifest(DocumentationFixture.Current.Correct,manifest=>manifest["state_svg_files"]=false);LegacyUiOriginalMethods.AssertMethod(LegacyUiOriginalMethods.Names[1],changed);}
    [Theory,InlineData(true),InlineData(false)]
    public void PythonBooleanRevisionRetainsOriginalIntegerSubclassSemantics(bool revision)
    {
        var state=DocumentationFixture.Current.Screens[0]["state_specs"]!.AsObject().First().Key;
        var changed=DocumentationFixture.ChangeSvg(DocumentationFixture.Current.Correct,"docs/screens/states/"+state+".svg",root=>
        {var element=root.DescendantsAndSelf().First(value=>value.Attribute("data-health-snapshot")is not null);var health=JsonNode.Parse(element.Attribute("data-health-snapshot")!.Value)!.AsObject();health["revision"]=revision;element.SetAttributeValue("data-health-snapshot",health.ToJsonString());});
        LegacyUiOriginalMethods.AssertMethod(LegacyUiOriginalMethods.Names[4],changed);
    }
}
