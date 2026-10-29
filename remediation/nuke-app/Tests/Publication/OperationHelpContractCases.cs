using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Publication;

public sealed record HelpCase(string Id,OperationHelpRequest Request,string? Error,string Sdk="10.0.403",string Package="10.1.0");
public static class OperationHelpContractCases
{
    public static OperationHelpRequest Request(string sdk="10.0.403",string package="10.1.0")
    {
        var definitions=new OperationDefinition[]{new(new(Category.Publish,Operation.Stage),"Copy pinned bundle",["output-root","deliverable-manifest","git-checkout"],["source-root"],[]),new(new(Category.Publish,Operation.Check),"Check retained files",["git-checkout"],[],[])};
        return new(definitions,new("global.json",JsonSerializer.SerializeToUtf8Bytes(new{sdk=new{version=sdk,rollForward="disable"}}),"application/json"),
            new("Build.csproj",PublicationTestData.Bytes("<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>"),"application/xml"),
            new("packages.lock.json",JsonSerializer.SerializeToUtf8Bytes(new{version=1,dependencies=new Dictionary<string,object>{{"net10.0",new Dictionary<string,object>{{"Nuke.Common",new{type="Direct",resolved=package}},{"Transitive",new{type="Transitive",resolved="9.9.9"}}}}}}),"application/json"),["dotnet-host"]);
    }
    private static Artifact Json(Artifact original,Action<JsonObject> change)
    {var node=JsonNode.Parse(original.Bytes.Span)!.AsObject();change(node);return original with{Bytes=JsonSerializer.SerializeToUtf8Bytes(node)};}
    public static IEnumerable<object[]> Cases
    {
        get
        {
            var input=Request();
            return new[]{
                new HelpCase("HELP-01",input,null),
                new HelpCase("HELP-02",Request("10.0.404","10.2.0"),null,"10.0.404","10.2.0"),
                new HelpCase("HELP-03",input with{Definitions=[]},null),
                new HelpCase("HELP-04",input with{Definitions=[input.Definitions[0],input.Definitions[0]]},"DUPLICATE_OPERATION"),
                new HelpCase("HELP-05",input with{SdkConfiguration=Json(input.SdkConfiguration,j=>j["sdk"]!.AsObject().Remove("version"))},"UNPINNED_SDK"),
                new HelpCase("HELP-06",input with{SdkConfiguration=Json(input.SdkConfiguration,j=>j["sdk"]!["rollForward"]="latestMajor")},"UNPINNED_SDK"),
                new HelpCase("HELP-07",input with{PackageLock=Json(input.PackageLock,j=>j["dependencies"]!.AsObject().Clear())},"LOCK_FRAMEWORK_MISMATCH"),
                new HelpCase("HELP-08",input with{PackageLock=Json(input.PackageLock,j=>j["dependencies"]!["net10.0"]!["Nuke.Common"]!.AsObject().Remove("resolved"))},"UNRESOLVED_PACKAGE_PIN"),
                new HelpCase("HELP-09",input with{ProjectConfiguration=input.ProjectConfiguration with{Bytes=PublicationTestData.Bytes("<!DOCTYPE Project [<!ENTITY x SYSTEM 'file:///never-read'>]><Project>&x;</Project>")}},"INVALID_HELP_INPUT"),
                new HelpCase("HELP-10",input with{GlobalRequiredParameters=["bad;parameter"]},"UNSAFE_PARAMETER_NAME")
            }.Select(x=>new object[]{x});
        }
    }
    public static OperationHelpResult Correct(HelpCase c)
    {
        if(c.Error is not null)return new(null,null,[new(c.Error,"")]);
        var operations=c.Request.Definitions.OrderBy(x=>x.Key.Category.ToString(),StringComparer.Ordinal).ThenBy(x=>x.Key.Operation.ToString(),StringComparer.Ordinal).Select(x=>new{category=x.Key.Category.ToString(),operation=x.Key.Operation.ToString(),description=x.Description,required=x.RequiredParameters.Order(StringComparer.Ordinal),optional=x.OptionalParameters.Order(StringComparer.Ordinal)}).ToArray();
        var content=JsonSerializer.SerializeToUtf8Bytes(new{schema_version=1,sdk=c.Sdk,roll_forward="disable",framework="net10.0",packages=new[]{new{name="Nuke.Common",version=c.Package}},operations,
            global_required=c.Request.GlobalRequiredParameters.Order(StringComparer.Ordinal),network="No network for help, Check or pure rehydration planning. Restore/acquisition prerequisites are explicit.",disk="Output/cache/archive requirements need their own measured plan; no full-corpus estimate is asserted.",execution="Registry metadata is a planning reference; root execution-adapter readiness is authoritative.",sdk_sha256=PublicationTestData.Hash(c.Request.SdkConfiguration.Bytes),project_sha256=PublicationTestData.Hash(c.Request.ProjectConfiguration.Bytes),package_lock_sha256=PublicationTestData.Hash(c.Request.PackageLock.Bytes)});
        var text=$"# Native NUKE operation help\nSDK {c.Sdk}; Nuke.Common {c.Package}\n"+string.Join("\n",c.Request.Definitions.Select(d=>d.Description+"\n"+$"--target {d.Key.Category} --operation {d.Key.Operation} "+string.Join(" ",c.Request.GlobalRequiredParameters.Concat(d.RequiredParameters).Distinct().Select(p=>$"--{p} '<{p}>'"))+" "+string.Join(" ",d.OptionalParameters.Select(p=>$"[--{p} '<{p}>']"))))+"\nPlanning reference; no execution adapter claim. Network and disk prerequisites and historical limitations remain explicit.\n";
        return new(new("operation-help.json",content,"application/json"),new("operation-help.md",PublicationTestData.Bytes(text),"text/markdown"),[]);
    }
    public static void AssertCase(HelpCase c,IOperationHelpBuilder provider)
    {
        var before=c.Request.Definitions.Select(d=>string.Join("|",d.RequiredParameters)).ToArray();
        var result=provider.Build(c.Request);
        Assert.Equal(before,c.Request.Definitions.Select(d=>string.Join("|",d.RequiredParameters)));
        Assert.Equal(c.Error is null,result.IsValid);Assert.Equal(c.Error is null?Array.Empty<string>():[c.Error],result.Issues.Select(x=>x.Code));
        if(c.Error is not null){Assert.Null(result.Document);Assert.Null(result.Markdown);return;}
        Assert.Equal("operation-help.json",result.Document!.RelativePath);Assert.Equal("operation-help.md",result.Markdown!.RelativePath);
        using var doc=JsonDocument.Parse(result.Document.Bytes);var root=doc.RootElement;
        Assert.Equal(1,root.GetProperty("schema_version").GetInt32());Assert.Equal(c.Sdk,root.GetProperty("sdk").GetString());Assert.Equal("disable",root.GetProperty("roll_forward").GetString());Assert.Equal("net10.0",root.GetProperty("framework").GetString());
        var package=Assert.Single(root.GetProperty("packages").EnumerateArray());Assert.Equal("Nuke.Common",package.GetProperty("name").GetString());Assert.Equal(c.Package,package.GetProperty("version").GetString());
        Assert.Equal(PublicationTestData.Hash(c.Request.SdkConfiguration.Bytes),root.GetProperty("sdk_sha256").GetString());Assert.Equal(PublicationTestData.Hash(c.Request.ProjectConfiguration.Bytes),root.GetProperty("project_sha256").GetString());Assert.Equal(PublicationTestData.Hash(c.Request.PackageLock.Bytes),root.GetProperty("package_lock_sha256").GetString());
        Assert.Equal(c.Request.GlobalRequiredParameters.Order(StringComparer.Ordinal),root.GetProperty("global_required").EnumerateArray().Select(x=>x.GetString()));
        Assert.Contains("No network",root.GetProperty("network").GetString());Assert.Contains("measured plan",root.GetProperty("disk").GetString());Assert.Contains("planning reference",root.GetProperty("execution").GetString());
        var expected=c.Request.Definitions.OrderBy(x=>x.Key.Category.ToString(),StringComparer.Ordinal).ThenBy(x=>x.Key.Operation.ToString(),StringComparer.Ordinal).ToArray();var actual=root.GetProperty("operations").EnumerateArray().ToArray();Assert.Equal(expected.Length,actual.Length);
        var markdown=Encoding.UTF8.GetString(result.Markdown.Bytes.Span);Assert.Contains(c.Sdk,markdown);Assert.Contains(c.Package,markdown);Assert.Contains("Planning reference",markdown);Assert.Contains("Network",markdown);Assert.Contains("disk",markdown);
        for(var i=0;i<expected.Length;i++){Assert.Equal(expected[i].Key.Category.ToString(),actual[i].GetProperty("category").GetString());Assert.Equal(expected[i].Key.Operation.ToString(),actual[i].GetProperty("operation").GetString());Assert.Equal(expected[i].Description,actual[i].GetProperty("description").GetString());Assert.Equal(expected[i].RequiredParameters.Order(StringComparer.Ordinal),actual[i].GetProperty("required").EnumerateArray().Select(x=>x.GetString()));Assert.Equal(expected[i].OptionalParameters.Order(StringComparer.Ordinal),actual[i].GetProperty("optional").EnumerateArray().Select(x=>x.GetString()));Assert.Contains(expected[i].Description,markdown);Assert.Contains($"--target {expected[i].Key.Category} --operation {expected[i].Key.Operation}",markdown);foreach(var p in c.Request.GlobalRequiredParameters.Concat(expected[i].RequiredParameters))Assert.Contains($"--{p} '<{p}>'",markdown);foreach(var p in expected[i].OptionalParameters)Assert.Contains($"[--{p} '<{p}>']",markdown);}
    }
}
public sealed class ScriptedOperationHelp(OperationHelpResult result):IOperationHelpBuilder
{public OperationHelpResult Build(OperationHelpRequest request)=>result;}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class OperationHelpMockTests
{
    [Theory,MemberData(nameof(OperationHelpContractCases.Cases),MemberType=typeof(OperationHelpContractCases))]
    public void CorrectMocksPass(HelpCase c)=>OperationHelpContractCases.AssertCase(c,new ScriptedOperationHelp(OperationHelpContractCases.Correct(c)));
    [Theory,MemberData(nameof(OperationHelpContractCases.Cases),MemberType=typeof(OperationHelpContractCases))]
    public void IndividualOutputAndManifestNegativeControlsReject(HelpCase c)
    {
        var good=OperationHelpContractCases.Correct(c);var wrong=new List<OperationHelpResult>{good with{Issues=[new("WRONG","")]}};
        if(good.Document is not null)
        {
            wrong.Add(good with{Document=null});wrong.Add(good with{Markdown=null});wrong.Add(good with{Markdown=good.Markdown! with{Bytes=PublicationTestData.Bytes("wrong")}});
            using var doc=JsonDocument.Parse(good.Document.Bytes);
            foreach(var field in doc.RootElement.EnumerateObject()){var json=JsonNode.Parse(good.Document.Bytes.Span)!.AsObject();json.Remove(field.Name);wrong.Add(good with{Document=good.Document with{Bytes=JsonSerializer.SerializeToUtf8Bytes(json)}});}
            if(c.Request.Definitions.Count>0){var json=JsonNode.Parse(good.Document.Bytes.Span)!.AsObject();foreach(var field in new[]{"category","operation","description","required","optional"}){var clone=json.DeepClone().AsObject();clone["operations"]![0]![field]=field is "required" or "optional"?new JsonArray("wrong"):JsonValue.Create("wrong");wrong.Add(good with{Document=good.Document with{Bytes=JsonSerializer.SerializeToUtf8Bytes(clone)}});}}
        }
        else wrong.Add(good with{Issues=[]});
        foreach(var response in wrong)Assert.NotNull(Record.Exception(()=>OperationHelpContractCases.AssertCase(c,new ScriptedOperationHelp(response))));
    }
}
