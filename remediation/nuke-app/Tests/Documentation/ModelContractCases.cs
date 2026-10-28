using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Documentation;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record ModelContractCase(string Id,Action<ModelContractEnvironment>? Mutation,IReadOnlyList<string> Errors)
{public override string ToString()=>Id;public ModelContractInput Input(){var environment=new ModelContractEnvironment();Mutation?.Invoke(environment);return environment.Input();}}
public static class ModelContractCases
{
    public const string Scope="Design consistency only; no Android implementation or Google data assessment";
    public static readonly string[] InvocationOrder=["beginDispatch:","Invoke permitted backend only","Release dispatch permit"];
    public static readonly string[] RevocationOrder=["ownerCommands -> bridge.app.policy","policy -> bridge.app.catalog","catalog -> bridge.app.policy","policy -> bridge.app.auth","auth -> bridge.app.transport"];
    public static IEnumerable<object[]> Data=>Create().Select(test=>new object[]{test});
    public static IEnumerable<ModelContractCase> Create()
    {
        var fixture=ModelContractFixture.Current;
        yield return new("MODEL-CONTRACT-ORIGINAL",null,[]);
        yield return new("MODEL-CONTRACT-OPTIONAL-MODULE",e=>e.OmitModule=true,[]);
        yield return new("MODEL-CONTRACT-CANONICAL-COPIES-ABSENT",e=>e.Copies=e.Copies.Select(copy=>copy with{Canonical=null}).ToArray(),[]);
        for(var index=0;index<fixture.Copies.Count;index++)
        {
            var selected=index;var name=fixture.Copies[index].File;
            yield return new("COPY-SNAPSHOT-"+name,e=>e.Copies[selected]=e.Copies[selected] with{Snapshot=e.Copies[selected].Snapshot with{Bytes=new byte[]{0}}},["Pinned contract copy changed "+name]);
            yield return new("COPY-CANONICAL-"+name,e=>e.Copies[selected]=e.Copies[selected] with{Canonical=e.Copies[selected].Snapshot with{Bytes=new byte[]{0}}},["Refresh stale canonical contract snapshot "+name]);
        }
        foreach(var (text,reason) in new[]{("InvocationPlanner ..> AdmissionGate : beginDispatch","Missing dispatch caller"),("+beginDispatch(admission)","Missing atomic dispatch contract"),("+commitProtectedCommandCAS","Missing protected epoch barrier"),("SqliteCatalogRepository ..> OwnerCapabilityVerifier","Storage imports service-owned owner verifier port")})
            yield return new("CODE-REQUIRED-"+text,e=>e.CodeText(value=>value.Replace(text,"REMOVED",StringComparison.Ordinal)),[reason+": "+text]);
        yield return new("CODE-FORBIDDEN-STORAGE-APPLICATION",e=>e.CodeText(value=>value+"\nSqliteCatalogRepository ..> OwnerAuthenticationService\n"),["Concrete storage->application dependency"]);
        foreach(var text in new[]{"+requestChallenge(","+deliverPlatformAuthCompletion(","+describePinned(","+pauseSensitiveFrameTreeAndLogExposure()","+onCapabilityChanged(","+createManualBearer(","+loadAdmittedTargetAuthorization("})
            yield return new("CODE-MECHANISM-"+text,e=>e.CodeText(value=>value.Replace(text,"REMOVED",StringComparison.Ordinal)),["Missing modeled mechanism: "+text]);
        foreach(var (name,order,reason) in new[]{("InvocationFlow",InvocationOrder,"Dispatch ordering"),("CredentialRevocation",RevocationOrder,"Revocation ordering")})
        {
            yield return new("DYNAMIC-MISSING-"+name,e=>e.DslText(value=>value.Replace(" "+name+" \""," MissingDynamic \"",StringComparison.Ordinal)),new[]{"Missing dynamic "+name}.Concat(order.Select(item=>reason+": "+item)).ToArray());
            foreach(var item in order)yield return new("DYNAMIC-TOKEN-"+name+":"+item,e=>e.DslText(value=>value.Replace(item,"REMOVED",StringComparison.Ordinal)),[reason+": "+item]);
            for(var index=0;index<order.Length-1;index++)
            {
                var first=order[index];var second=order[index+1];
                yield return new("DYNAMIC-ORDER-"+name+":"+index,e=>e.DslText(value=>value.Replace(first,"ORDER_SENTINEL",StringComparison.Ordinal).Replace(second,first,StringComparison.Ordinal).Replace("ORDER_SENTINEL",second,StringComparison.Ordinal)),[reason+": "+second]);
            }
        }
        yield return new("REVOCATION-BYPASS",e=>e.DslText(value=>
        {var start=value.IndexOf(" CredentialRevocation \"",StringComparison.Ordinal);var end=value.IndexOf("autoLayout",start,StringComparison.Ordinal);return value.Insert(end,"ownerCommands -> bridge.app.catalog\n");}),["Revocation bypasses shared gate"]);
        foreach(var edge in new[]{"bridge.app.adb -> bridge.app.device","bridge.app.invoke -> bridge.app.device"})
            yield return new("BACKEND-REFRESH-"+edge,e=>e.DslText(value=>value.Replace(edge,"REMOVED",StringComparison.Ordinal)),["Missing backend-driven refresh edge: "+edge]);
        var routes=Regex.Matches(DocumentationFixture.Utf8(fixture.Design),@"\| (GET|POST|PUT|DELETE) (/v1/catalog/[^ |]+) \|").Select(match=>(Method:match.Groups[1].Value,Path:match.Groups[2].Value)).ToArray();
        string[] RouteErrors(string method,string path)=>routes.Where(route=>route.Method==method&&route.Path==path).Select(route=>"Design route absent from OpenAPI "+method+" "+path)
            .Concat(path=="/v1/catalog/import-stages"&&(method is "GET" or "POST")?new[]{"Missing sealed stage route "+method.ToLowerInvariant()}:Array.Empty<string>()).ToArray();
        foreach(var route in routes.Distinct())yield return new("API-ROUTE-"+route.Method+":"+route.Path,e=>e.Api["paths"]![route.Path]!.AsObject().Remove(route.Method.ToLowerInvariant()),RouteErrors(route.Method,route.Path));
        var firstRoute=routes.First();
        foreach(var (id,value) in new (string,JsonNode)[]{("empty",JsonValue.Create("")!),("false",JsonValue.Create(false)!),("zero",JsonValue.Create(0)!)})
            yield return new("API-ROUTE-FALSY-"+id,e=>e.Api["paths"]![firstRoute.Path]![firstRoute.Method.ToLowerInvariant()]=value.DeepClone(),RouteErrors(firstRoute.Method,firstRoute.Path));
        yield return new("API-ROUTE-EMPTY-OBJECT-IS-PRESENT",e=>e.Api["paths"]![firstRoute.Path]![firstRoute.Method.ToLowerInvariant()]=new JsonObject(),[]);
        foreach(var method in new[]{"get","post"})yield return new("API-SEALED-STAGE-"+method,e=>e.Api["paths"]!["/v1/catalog/import-stages"]!.AsObject().Remove(method),RouteErrors(method.ToUpperInvariant(),"/v1/catalog/import-stages"));
        foreach(var field in new[]{"type","scheme"})yield return new("API-BEARER-"+field,e=>e.Api["components"]!["securitySchemes"]!["catalogAuth"]![field]="wrong",["Admin bearer contract drift"]);
        foreach(var name in fixture.Classes)
        {
            yield return new("C4-MISSING-"+name,e=>e.Module["components"]!.AsObject().Remove(name),["Missing C4 class mapping "+name]);
            var target="bridge.app.unknown_"+name;
            yield return new("C4-UNKNOWN-"+name,e=>e.Module["components"]![name]=target,["Unknown C4 component mapping "+name+" "+target]);
        }
        yield return new("C4-STALE-OWNER",e=>e.Module["classes"]!["StaleOwner"]="bridge.domain",["Stale class owner mapping StaleOwner"]);
        foreach(var screenNode in fixture.Blueprint["screens"]!.AsArray())
        {
            var screen=screenNode!.AsObject();var id=screen["id"]!.GetValue<string>();
            foreach(var control in screen["control_specs"]!.AsObject())
            {
                var cid=control.Key;
                yield return new("CONTROL-PLACEHOLDER-"+cid,e=>e.Screen(id)["control_specs"]![cid]!["behavior"]="Exact named action; protected mutations open SCR09; reads never change authority",["Placeholder action "+cid]);
                foreach(var field in new[]{"behavior","fields","success","error","cancel","gates"})
                    yield return new("CONTROL-REQUIRED-"+cid+":"+field,e=>e.Screen(id)["control_specs"]![cid]!.AsObject().Remove(field),["Missing control contract "+cid+" "+field]);
            }
            if(screen["state_specs"]!.AsObject().Count>1)
                foreach(var field in new[]{"effect","recovery"})yield return new("STATE-TEMPLATE-"+id+":"+field,e=>{foreach(var state in e.Screen(id)["state_specs"]!.AsObject())state.Value![field]="Identical fixture template";},["Template states "+id+" "+field]);
        }
        var firstScreen=fixture.Blueprint["screens"]![0]!.AsObject();var firstScreenId=firstScreen["id"]!.GetValue<string>();var firstControl=firstScreen["control_specs"]!.AsObject().First().Key;
        foreach(var field in new[]{"behavior","fields","success","error","cancel","gates"})
            foreach(var (id,value) in new (string,JsonNode)[]{("empty",JsonValue.Create("")!),("false",JsonValue.Create(false)!),("zero",JsonValue.Create(0)!)})
                yield return new("CONTROL-FALSY-"+field+":"+id,e=>e.Screen(firstScreenId)["control_specs"]![firstControl]![field]=value.DeepClone(),["Missing control contract "+firstControl+" "+field]);
        var steps=fixture.Blueprint["storyboards"]!.AsArray().SelectMany(story=>story!["steps"]!.AsArray()).Select(step=>step!.AsObject()).ToArray();
        foreach(var step in steps)
        {
            var sid=step["step_id"]!.GetValue<string>();
            yield return new("STORY-ANCHOR-"+sid,e=>e.Step(sid)["state_id"]="UnknownState",["Bad storyboard anchor "+sid]);
        }
        var controls=steps.Where(step=>step["interaction"]!.GetValue<string>()=="control").Select(step=>(Screen:step["screen_id"]!.GetValue<string>(),State:step["state_id"]!.GetValue<string>(),Control:step["control_id"]!.GetValue<string>())).Distinct().ToArray();
        foreach(var control in controls)
        {
            var affected=steps.Where(step=>step["interaction"]!.GetValue<string>()=="control"&&step["screen_id"]!.GetValue<string>()==control.Screen&&step["state_id"]!.GetValue<string>()==control.State&&step["control_id"]!.GetValue<string>()==control.Control).Select(step=>"Disabled storyboard action "+step["step_id"]!.GetValue<string>()).ToArray();
            yield return new("STORY-DISABLED-"+control.State+":"+control.Control,e=>
            {var enabled=e.Screen(control.Screen)["state_specs"]![control.State]!["enabled_controls"]!.AsArray();for(var index=enabled.Count-1;index>=0;index--)if(enabled[index]!.GetValue<string>()==control.Control)enabled.RemoveAt(index);},affected);
        }
        foreach(var story in fixture.Blueprint["storyboards"]!.AsArray())
        {
            var alternative=story!["steps"]!.AsArray().FirstOrDefault(step=>step!["branch"]!.GetValue<string>()!="main");if(alternative is null)continue;
            var alternativeId=alternative["step_id"]!.GetValue<string>();
            foreach(var step in story["steps"]!.AsArray().Where(step=>step!["branch"]!.GetValue<string>()=="main"))
            {var id=step!["step_id"]!.GetValue<string>();yield return new("STORY-HAPPY-ALTERNATIVE-"+id,e=>e.Step(id)["next_success"]=alternativeId,["Happy path forces error alternative "+id]);}
        }
    }
}
