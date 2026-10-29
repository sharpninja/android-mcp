using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

/// <summary>Original design consistency clauses over shared parsed authorities. No runtime or API completeness claim.</summary>
public sealed class ModelContractValidator(IContentHash hashes):IModelContractValidator
{
    private static readonly string[] ControlFields=["behavior","fields","success","error","cancel","gates"];
    public ModelContractValidationResult Validate(ModelContractInput input)
    {
        foreach(var copy in input.ContractCopies)
        {
            if(hashes.Sha256(copy.Snapshot.Bytes.Span)!=copy.ExpectedSha256)throw new ModelContractException(["Pinned contract copy changed "+copy.File]);
            if(copy.Canonical is {} canonical&&hashes.Sha256(canonical.Bytes.Span)!=copy.ExpectedSha256)
                throw new ModelContractException(["Refresh stale canonical contract snapshot "+copy.File]);
        }
        var api=JsonNode.Parse(input.ContractCopies.Single(copy=>copy.File=="openapi.json").Snapshot.Bytes.Span)!.AsObject();
        var code=Encoding.UTF8.GetString(input.CodeMarkdown.Bytes.Span);var dsl=Encoding.UTF8.GetString(input.WorkspaceDsl.Bytes.Span);
        var design=Encoding.UTF8.GetString(input.DesignMarkdown.Bytes.Span);var errors=new List<string>();
        void Require(string source,string text,string reason){if(!source.Contains(text,StringComparison.Ordinal))errors.Add(reason+": "+text);}
        Require(code,"InvocationPlanner ..> AdmissionGate : beginDispatch","Missing dispatch caller");
        Require(code,"+beginDispatch(admission)","Missing atomic dispatch contract");
        Require(code,"+commitProtectedCommandCAS","Missing protected epoch barrier");
        Require(code,"SqliteCatalogRepository ..> OwnerCapabilityVerifier","Storage imports service-owned owner verifier port");
        if(code.Contains("SqliteCatalogRepository ..> OwnerAuthenticationService",StringComparison.Ordinal))errors.Add("Concrete storage->application dependency");
        foreach(var member in new[]{"+requestChallenge(","+deliverPlatformAuthCompletion(","+describePinned(","+pauseSensitiveFrameTreeAndLogExposure()","+onCapabilityChanged(","+createManualBearer(","+loadAdmittedTargetAuthorization("})
            Require(code,member,"Missing modeled mechanism");
        string Dynamic(string name)
        {
            var start=dsl.IndexOf(" "+name+" \"",StringComparison.Ordinal);
            if(start<0){errors.Add("Missing dynamic "+name);return "";}
            var end=dsl.IndexOf("autoLayout",start,StringComparison.Ordinal);
            return dsl[start..(end<0?dsl.Length-1:end)];
        }
        var invocation=Dynamic("InvocationFlow");var revocation=Dynamic("CredentialRevocation");
        void Order(string text,IReadOnlyList<string> items,string reason)
        {var last=-1;foreach(var item in items){var current=text.IndexOf(item,StringComparison.Ordinal);if(current<0||current<=last)errors.Add(reason+": "+item);last=current;}}
        Order(invocation,["beginDispatch:","Invoke permitted backend only","Release dispatch permit"],"Dispatch ordering");
        Order(revocation,["ownerCommands -> bridge.app.policy","policy -> bridge.app.catalog","catalog -> bridge.app.policy","policy -> bridge.app.auth","auth -> bridge.app.transport"],"Revocation ordering");
        if(revocation.Contains("ownerCommands -> bridge.app.catalog",StringComparison.Ordinal))errors.Add("Revocation bypasses shared gate");
        foreach(var edge in new[]{"bridge.app.adb -> bridge.app.device","bridge.app.invoke -> bridge.app.device"})Require(dsl,edge,"Missing backend-driven refresh edge");
        var paths=Property(api,"paths");
        foreach(Match match in Regex.Matches(design,@"\| (GET|POST|PUT|DELETE) (/v1/catalog/[^ |]+) \|",RegexOptions.CultureInvariant))
            if(!Truthy(Property(Property(paths,match.Groups[2].Value),match.Groups[1].Value.ToLowerInvariant())))
                errors.Add("Design route absent from OpenAPI "+match.Groups[1].Value+" "+match.Groups[2].Value);
        var bearer=Property(Property(Property(api,"components"),"securitySchemes"),"catalogAuth");
        if(Text(bearer,"type")!="http"||Text(bearer,"scheme")!="bearer")errors.Add("Admin bearer contract drift");
        if(input.ModuleContract is {} modules)
        {
            var names=input.DeclaredClasses??throw new ArgumentException("Supply the existing module authority's declared class metadata",nameof(input));
            var defined=names.ToHashSet(StringComparer.Ordinal);
            foreach(var name in names)
            {
                var component=Property(Property(modules,"components"),name);
                if(!Truthy(component)){errors.Add("Missing C4 class mapping "+name);continue;}
                var value=component!.GetValue<string>();var alias=value.Split('.').Last();var kind=value.StartsWith("bridge.app.",StringComparison.Ordinal)?"component":"softwareSystem";
                if(!Regex.IsMatch(dsl,@"^\s*"+alias+@"\s*=\s*"+kind+@"\b",RegexOptions.Multiline|RegexOptions.CultureInvariant))
                    errors.Add("Unknown C4 component mapping "+name+" "+value);
            }
            foreach(var owner in modules["classes"]!.AsObject())if(!defined.Contains(owner.Key))errors.Add("Stale class owner mapping "+owner.Key);
        }
        foreach(var method in new[]{"get","post"})if(!Truthy(Property(Property(paths,"/v1/catalog/import-stages"),method)))errors.Add("Missing sealed stage route "+method);
        foreach(var screen in input.Model.Screens)
        {
            foreach(var pair in screen["control_specs"]!.AsObject())
            {
                var contract=pair.Value!.AsObject();
                if(Text(contract,"behavior")=="Exact named action; protected mutations open SCR09; reads never change authority")errors.Add("Placeholder action "+pair.Key);
                foreach(var field in ControlFields)if(!Truthy(Property(contract,field)))errors.Add("Missing control contract "+pair.Key+" "+field);
            }
            foreach(var field in new[]{"effect","recovery"})
            {
                var values=screen["state_specs"]!.AsObject().Select(pair=>
                {var state=pair.Value!.AsObject();var present=state.TryGetPropertyValue(field,out var value);return (Present:present,Value:value);}).ToArray();
                if(values.Length>1&&values.Skip(1).All(value=>SameValue(values[0],value)))errors.Add("Template states "+Text(screen,"id")+" "+field);
            }
        }
        foreach(var story in input.Model.Storyboards)
        {
            var steps=story["steps"]!.AsArray().Select(node=>node!.AsObject()).ToArray();var index=new Dictionary<string,JsonObject>(StringComparer.Ordinal);
            foreach(var step in steps)index[Text(step,"step_id")]=step;
            foreach(var step in steps)
            {
                var id=Text(step,"step_id");input.Model.ScreensById.TryGetValue(Text(step,"screen_id"),out var screen);
                var state=Property(Property(screen,"state_specs"),Text(step,"state_id"));var control=Text(step,"control_id");
                if(!Truthy(state)||!Truthy(Property(Property(screen,"controls"),control)))errors.Add("Bad storyboard anchor "+id);
                else if(Text(step,"interaction")=="control"&&!Property(state,"enabled_controls")!.AsArray().Any(node=>node!.GetValue<string>()==control))errors.Add("Disabled storyboard action "+id);
                if(Text(step,"branch")=="main"&&index.TryGetValue(Text(step,"next_success"),out var next)&&Text(next,"branch")!="main")errors.Add("Happy path forces error alternative "+id);
            }
        }
        if(errors.Count>0)throw new ModelContractException(errors);
        return new(true,"Design consistency only; no Android implementation or Google data assessment",input.Model.Screens.Count,input.Model.Screens.Sum(screen=>screen["states"]!.AsObject().Count));
    }
    private static JsonNode? Property(JsonNode? node,string key)=>node is JsonObject value?value[key]:null;
    private static string Text(JsonNode? node,string key)=>Property(node,key) is JsonValue value&&value.TryGetValue<string>(out var text)?text:"";
    private static double Number(JsonNode value)=>double.Parse(value.ToJsonString(),NumberStyles.Float,CultureInfo.InvariantCulture);
    private static bool Truthy(JsonNode? value)=>value is not null&&(value.GetValueKind() switch
    {JsonValueKind.False or JsonValueKind.Null=>false,JsonValueKind.String=>value.GetValue<string>().Length>0,JsonValueKind.Number=>Number(value)!=0,_=>true});
    private static bool SameValue((bool Present,JsonNode? Value) first,(bool Present,JsonNode? Value) second)
    {
        if(first.Present!=second.Present)return false;
        if(ReferenceEquals(first.Value,second.Value))return true;
        if(first.Value is null||second.Value is null)return false;
        var kind=first.Value.GetValueKind();if(kind!=second.Value.GetValueKind())return false;
        return kind switch
        {JsonValueKind.Number=>Number(first.Value)==Number(second.Value),JsonValueKind.String=>first.Value.GetValue<string>()==second.Value.GetValue<string>(),JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null=>true,_=>false};
    }
}
