using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

// The HTMLParser boundary is supplied independently for mocks; native binding
// observes the generated HTML through the shared test-only reader below.
public sealed record Design022DeckObservation(IReadOnlyList<string?> Images,IReadOnlyList<string> LiteralImages);
public sealed record Design022Evidence(string Design,string Code,string Dsl,DocumentationProjection Ui,Design022DeckObservation Deck);
public static class LegacyDesign022Methods
{
    public static readonly string[] Names=["test_slides_contain_actual_vector_images_not_raw_markdown","test_slideshow_uses_standard_svg_images",
        "test_error_status_fixture_uses_actual_closed_health_domains","test_controls_states_and_external_actors_have_explicit_contracts",
        "test_arc_input_lifetime_matches_pinned_singleton_not_unary_socket","test_arc_backend_and_health_paths_exist_across_views","test_unknown_state_has_phase_specific_cleanup_exits"];
    public static readonly string[][] CodeTokens=[[],[],["desiredState"],[],["ArcInputLeaseRegistry \"1\" *-- \"0..1\" ArcInputDispatcher"],["ArcTargetBackend ..|> BackendAdapter","+arcListener"],[]];
    public static readonly string[][] DesignTokens=[[],[],[],[],["press in requestA, move inB, release inC"],[],["uncertain_phase=cleanup) + confirmed unregister success","uncertain_phase=cleanup) + confirmed unregister failure","| Reserved |"]];
    public static Design022Evidence Correct
    {
        get {
            var fixture=DocumentationFixture.Current;var projection=fixture.Correct;
            var images=fixture.Steps.Select(step=>"data:image/svg+xml;base64,"+Convert.ToBase64String(DocumentationFixture.Get(projection,"docs/screens/steps/"+DocumentationFixture.Text(step,"step_id")+".svg").Bytes.Span)).ToArray();
            return new(string.Join('\n',DesignTokens.SelectMany(value=>value)),string.Join('\n',CodeTokens.SelectMany(value=>value)),"bridge.app.invoke -> bridge.app.arcController",projection,new(images,[]));
        }
    }
    public static void AssertOriginal(string method,Design022Evidence evidence)
    {
        var index=Array.IndexOf(Names,method);Assert.InRange(index,0,Names.Length-1);
        var manifest=LegacyUiOriginalMethods.Manifest(evidence.Ui);var screens=manifest["screens"]!.AsArray().Select(value=>value!.AsObject()).ToArray();
        var steps=manifest["storyboards"]!.AsArray().SelectMany(story=>story!["steps"]!.AsArray()).Select(value=>value!.AsObject()).ToArray();
        foreach(var token in CodeTokens[index])Assert.Contains(token,evidence.Code,StringComparison.Ordinal);
        foreach(var token in DesignTokens[index])Assert.Contains(token,evidence.Design,StringComparison.Ordinal);
        switch(index){
            case 0:
                Assert.Empty(evidence.Deck.LiteralImages);Assert.Equal(steps.Length,evidence.Deck.Images.Count);
                foreach(var (image,step) in evidence.Deck.Images.Zip(steps)){
                    var encoded=Regex.Match(image!,@"data:image/svg\+xml;base64,([A-Za-z0-9+/=]+)");Assert.True(encoded.Success);
                    var bytes=Convert.FromBase64String(encoded.Groups[1].Value);
                    Assert.Equal(DocumentationFixture.Get(evidence.Ui,"docs/screens/steps/"+DocumentationFixture.Text(step,"step_id")+".svg").Bytes.ToArray(),bytes);
                    var root=XElement.Parse(Encoding.UTF8.GetString(bytes));Assert.Equal(DocumentationFixture.Text(step,"state_id"),(string?)root.Attribute("data-screen-state"));
                    Assert.Equal(DocumentationFixture.Text(step,"control_id"),(string?)root.Attribute("data-focus-control"));
                }break;
            case 1:
                var source=DocumentationFixture.Content(evidence.Ui,"storyboards/storyboard.md");Assert.DoesNotContain("![bg ",source,StringComparison.Ordinal);
                Assert.Equal(107,Regex.Matches(source,Regex.Escape("![Screen](")).Count);break;
            case 2:
                var health=screens.First(screen=>DocumentationFixture.Text(screen,"id")=="SCR10")["health_example"]!;
                foreach(var (field,value) in new[]{("service","Error"),("mcp","error"),("catalog","quarantined"),("device_view","stale")})Assert.Equal(value,health[field]!.GetValue<string>());
                var chip=DocumentationFixture.Svg(evidence.Ui,"ui-svg/SCR10.svg").DescendantsAndSelf().First(element=>(string?)element.Attribute("data-status-id")=="SCR10.STATUS");
                // ElementTree.itertext includes descendant text and tails in document order.
                Assert.DoesNotContain("Running",chip.Value,StringComparison.Ordinal);break;
            case 3:
                var byId=screens.ToDictionary(screen=>DocumentationFixture.Text(screen,"id"),StringComparer.Ordinal);
                foreach(var screen in screens){
                    var id=DocumentationFixture.Text(screen,"id");var controls=screen["controls"]!.AsObject().Select(value=>value.Key).ToHashSet(StringComparer.Ordinal);
                    var rendered=DocumentationFixture.Svg(evidence.Ui,"ui-svg/"+id+".svg").DescendantsAndSelf().Select(element=>(string?)element.Attribute("data-control-id")).Where(value=>!string.IsNullOrEmpty(value)).Select(value=>value!).ToHashSet(StringComparer.Ordinal);
                    Assert.True(controls.SetEquals(rendered));Assert.True(controls.SetEquals(screen["control_specs"]!.AsObject().Select(value=>value.Key)));
                    foreach(var state in screen["state_specs"]!.AsObject()){
                        var enabled=DocumentationFixture.Values(state.Value!["enabled_controls"]).ToHashSet(StringComparer.Ordinal);var disabled=DocumentationFixture.Values(state.Value!["disabled_controls"]).ToHashSet(StringComparer.Ordinal);
                        Assert.True(controls.SetEquals(enabled.Union(disabled)));Assert.Empty(enabled.Intersect(disabled));
                    }
                }
                Assert.Contains(steps,step=>DocumentationFixture.Text(step,"actor")=="External MCP client"&&DocumentationFixture.Text(step,"interaction")=="external_event");
                foreach(var step in steps){Assert.True(PythonTruthy(step["error"])&&PythonTruthy(step["cancel"]));Assert.True(JsonNode.DeepEquals(byId[DocumentationFixture.Text(step,"screen_id")]["controls"]![DocumentationFixture.Text(step,"control_id")],step["action"]));}
                Assert.Contains("SCR09.C01",DocumentationFixture.Values(byId["SCR09"]["state_specs"]!["SCR09.S1"]!["disabled_controls"]));
                Assert.Contains("SCR07.C07",DocumentationFixture.Values(byId["SCR07"]["state_specs"]!["SCR07.S0"]!["disabled_controls"]));break;
            case 4:
                Assert.DoesNotContain("ArcConnectionSession \"1\" *-- \"0..1\" ArcInputDispatcher",evidence.Code,StringComparison.Ordinal);
                Assert.DoesNotContain("stale node/frame targets",evidence.Design,StringComparison.Ordinal);break;
            case 5:
                Assert.Contains("bridge.app.invoke -> bridge.app.arcController",evidence.Dsl,StringComparison.Ordinal);
                Assert.True(screens[7]["controls"]!.AsObject().ContainsKey("SCR08.C07"));break;
            case 6:Assert.DoesNotContain("kind CHECK mcp/admin;",evidence.Design,StringComparison.Ordinal);break;
        }
    }
    private static bool PythonTruthy(JsonNode? value)=>value is not null&&(value.GetValueKind() switch {
        System.Text.Json.JsonValueKind.False or System.Text.Json.JsonValueKind.Null=>false,
        System.Text.Json.JsonValueKind.String=>value.GetValue<string>().Length>0,
        System.Text.Json.JsonValueKind.Array=>value.AsArray().Count>0,System.Text.Json.JsonValueKind.Object=>value.AsObject().Count>0,
        System.Text.Json.JsonValueKind.Number=>double.Parse(value.ToJsonString(),System.Globalization.CultureInfo.InvariantCulture)!=0,_=>true});
    public static IEnumerable<object[]> Mutations
    {
        get {
            for(var index=0;index<Names.Length;index++){
                foreach(var token in CodeTokens[index])yield return new object[]{index,"code",token};
                foreach(var token in DesignTokens[index])yield return new object[]{index,"design",token};
            }
            foreach(var item in new[]{"literal","image-count","embedded","bytes","state","focus"})yield return new object[]{0,"deck",item};
            yield return new object[]{1,"deck-source","count"};yield return new object[]{1,"deck-source","background"};
            foreach(var item in new[]{"service","mcp","catalog","device_view","chip"})yield return new object[]{2,"health",item};
            foreach(var item in new[]{"rendered","specs","coverage","overlap","actor","interaction","error","cancel","action","owner-disabled","preview-disabled"})yield return new object[]{3,"control",item};
            yield return new object[]{4,"forbidden-code","ArcConnectionSession \"1\" *-- \"0..1\" ArcInputDispatcher"};
            yield return new object[]{4,"forbidden-design","stale node/frame targets"};
            yield return new object[]{5,"dsl","bridge.app.invoke -> bridge.app.arcController"};yield return new object[]{5,"control","arc-listener"};
            yield return new object[]{6,"forbidden-design","kind CHECK mcp/admin;"};
        }
    }
    public static Design022Evidence Wrong(int index,string field,string token)
    {
        var value=Correct;
        if(field=="code")return value with{Code=value.Code.Replace(token,"REMOVED",StringComparison.Ordinal)};
        if(field=="design")return value with{Design=value.Design.Replace(token,"REMOVED",StringComparison.Ordinal)};
        if(field=="dsl")return value with{Dsl="REMOVED"};
        if(field=="forbidden-code")return value with{Code=value.Code+token};
        if(field=="forbidden-design")return value with{Design=value.Design+token};
        if(field=="deck-source")return value with{Ui=DocumentationFixture.Change(value.Ui,"storyboards/storyboard.md",text=>token=="background"?text+"![bg ](x)":text.Replace("![Screen](","![Other](",StringComparison.Ordinal))};
        if(field=="deck"){
            if(token=="literal")return value with{Deck=value.Deck with{LiteralImages=["![Screen](raw)"]}};
            if(token=="image-count")return value with{Deck=value.Deck with{Images=value.Deck.Images.Skip(1).ToArray()}};
            var images=value.Deck.Images.ToArray();
            if(token=="embedded")images[0]="screens/step.svg";
            else if(token=="bytes")images[0]="data:image/svg+xml;base64,"+Convert.ToBase64String("<svg/>"u8);
            else {
                var path="docs/screens/steps/"+DocumentationFixture.Text(DocumentationFixture.Current.Steps[0],"step_id")+".svg";
                var ui=DocumentationFixture.ChangeSvg(value.Ui,path,root=>root.SetAttributeValue(token=="state"?"data-screen-state":"data-focus-control","wrong"));
                images[0]="data:image/svg+xml;base64,"+Convert.ToBase64String(DocumentationFixture.Get(ui,path).Bytes.Span);
                return value with{Ui=ui,Deck=value.Deck with{Images=images}};
            }
            return value with{Deck=value.Deck with{Images=images}};
        }
        if(field=="health"&&token=="chip")return value with{Ui=DocumentationFixture.ChangeSvg(value.Ui,"ui-svg/SCR10.svg",root=>root.DescendantsAndSelf().First(element=>(string?)element.Attribute("data-status-id")=="SCR10.STATUS").Value="Running")};
        if(field=="control"&&token=="rendered")return value with{Ui=DocumentationFixture.ChangeSvg(value.Ui,"ui-svg/SCR01.svg",root=>root.DescendantsAndSelf().First(element=>element.Attribute("data-control-id")is not null).SetAttributeValue("data-control-id","WRONG"))};
        var projection=LegacyUiOriginalMethods.ChangeManifest(value.Ui,manifest=>{
            var screens=manifest["screens"]!.AsArray();var first=screens[0]!;var steps=manifest["storyboards"]!.AsArray().SelectMany(story=>story!["steps"]!.AsArray()).ToArray();
            if(field=="health"){screens.First(screen=>screen!["id"]!.GetValue<string>()=="SCR10")!["health_example"]![token]="wrong";return;}
            var controls=first["controls"]!.AsObject();var state=first["state_specs"]!.AsObject().First().Value!;
            switch(token){
                case "specs":first["control_specs"]!.AsObject().Remove(controls.First().Key);break;
                case "coverage":state["enabled_controls"]!.AsArray().Clear();state["disabled_controls"]!.AsArray().Clear();break;
                case "overlap":state["enabled_controls"]!.AsArray().Add(controls.First().Key);state["disabled_controls"]!.AsArray().Add(controls.First().Key);break;
                case "actor":foreach(var step in steps)step!["actor"]="Other";break;
                case "interaction":foreach(var step in steps)step!["interaction"]="control";break;
                case "error":case "cancel":steps[0]![token]="";break;
                case "action":steps[0]!["action"]="wrong";break;
                case "owner-disabled":case "preview-disabled":
                    var sid=token=="owner-disabled"?"SCR09":"SCR07";var st=token=="owner-disabled"?"SCR09.S1":"SCR07.S0";var cid=token=="owner-disabled"?"SCR09.C01":"SCR07.C07";
                    var target=screens.First(screen=>screen!["id"]!.GetValue<string>()==sid)!["state_specs"]![st]!;
                    target["disabled_controls"]!.AsArray().Remove(target["disabled_controls"]!.AsArray().First(control=>control!.GetValue<string>()==cid));target["enabled_controls"]!.AsArray().Add(cid);break;
                case "arc-listener":screens[7]!["controls"]!.AsObject().Remove("SCR08.C07");break;
                default:throw new InvalidOperationException("Unknown mutation");
            }
        });
        return value with{Ui=projection};
    }
}

[Trait("Gate","Mock"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class LegacyDesign022MethodMockTests
{
    public static IEnumerable<object[]> Methods=>LegacyDesign022Methods.Names.Select(method=>new object[]{method});
    [Theory,MemberData(nameof(Methods))]
    public void CorrectIndependentMockPassesEveryOriginalMethod(string method)=>LegacyDesign022Methods.AssertOriginal(method,LegacyDesign022Methods.Correct);
    [Theory,MemberData(nameof(LegacyDesign022Methods.Mutations),MemberType=typeof(LegacyDesign022Methods))]
    public void EachOriginalPredicateRejectsItsOwnWrongMock(int index,string field,string token)=>
        Assert.NotNull(Record.Exception(()=>LegacyDesign022Methods.AssertOriginal(LegacyDesign022Methods.Names[index],LegacyDesign022Methods.Wrong(index,field,token))));
}

public sealed record LegacyDeckParserCase(string Id,string Html,Design022DeckObservation Expected)
{public override string ToString()=>Id;}
public static class LegacyDeckParserCases
{
    public static IEnumerable<object[]> Cases=>new[]{
        new LegacyDeckParserCase("empty","",new([],[])),
        new("image","<img alt=\"Screen\" src=\"first\">",new(["first"],[])),
        new("case","<IMG ALT='Screen' SRC=first/>",new(["first/"],[])),
        new("alt-value-case","<img alt=screen src=first><image alt=Screen src=second>",new([],[])),
        new("missing-src","<img alt=Screen><img src=x>",new([""],[])),
        new("empty-src","<img alt=Screen src=''>",new([""],[])),
        new("valueless-src","<img alt=Screen src>",new([null],[])),
        new("duplicate-attribute","<img alt=Other alt=Screen src=old src='last'>",new(["last"],[])),
        new("entity-attribute","<img alt='Scr&#101;en' src='a&amp;b'>",new(["a&b"],[])),
        new("ordered-images","<img src='one' alt='Screen'/><img alt=Other src=skip><img alt=Screen src=two>",new(["one","two"],[])),
        new("comment-declaration","<!doctype html><!-- <img alt=Screen src=skip> ![x](y) --><img alt=Screen src=one>",new(["one"],[])),
        new("data","before ![Screen](raw) after",new([],["before ![Screen](raw) after"])),
        new("entity-data","!&#91;Screen](raw)",new([],["![Screen](raw)"])),
        new("split-data","![Screen<b>](raw)</b>",new([],[])),
        new("raw-script","<script>![Screen](raw)<img alt=Screen src=skip></script><img alt=Screen src=one>",new(["one"],["![Screen](raw)<img alt=Screen src=skip>"])),
        new("raw-style","<style>![Screen](raw)</style>",new([],["![Screen](raw)"])),
        new("unfinished-script-feed","<script>![Screen](pending)",new([],[])),
        new("truncate-data",new string('a',70)+"![Screen](raw)"+new string('b',90),new([],[new string('a',70)+"![Screen]("])),
        new("quoted-angle","<img alt='Screen' title='a>b' src='vector'>",new(["vector"],[]))
    }.Select(value=>new object[]{value});
    public static void AssertObservation(LegacyDeckParserCase test,Func<string,Design022DeckObservation> read)
    {var actual=read(test.Html);Assert.Equal(test.Expected.Images,actual.Images);Assert.Equal(test.Expected.LiteralImages,actual.LiteralImages);}
}
[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-005")]
public sealed class LegacyDesign022DeckParserMockTests
{
    [Theory,MemberData(nameof(LegacyDeckParserCases.Cases),MemberType=typeof(LegacyDeckParserCases))]
    public void IndependentParserObservationsPass(LegacyDeckParserCase test)=>LegacyDeckParserCases.AssertObservation(test,html=>{Assert.Equal(test.Html,html);return test.Expected;});
    [Theory,MemberData(nameof(LegacyDeckParserCases.Cases),MemberType=typeof(LegacyDeckParserCases))]
    public void EveryImageAndLiteralObservationRejectsWrongValues(LegacyDeckParserCase test)
    {
        Assert.NotNull(Record.Exception(()=>LegacyDeckParserCases.AssertObservation(test,_=>test.Expected with{Images=["wrong"]})));
        Assert.NotNull(Record.Exception(()=>LegacyDeckParserCases.AssertObservation(test,_=>test.Expected with{LiteralImages=["wrong"]})));
    }
}
