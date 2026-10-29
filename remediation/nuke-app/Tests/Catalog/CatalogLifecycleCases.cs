using System.Text.Json.Nodes;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record LifecycleCase(string Id,JsonObject Entry,JsonObject Source,string Expected);
public interface ILifecycleCaseProvider { string Project(JsonObject entry,JsonObject source); }
public static class CatalogLifecycleCases
{
    public const string SourceId="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    public static JsonObject Entry()=>new(){["extensions"]=new JsonObject{["source_kind"]="sdk-api-index",["api_lifecycle"]=new JsonObject{["since"]="37.1"},["container_api_lifecycle"]=new JsonObject{["since"]="36.0",["removed"]="40"}}};
    public static JsonObject Source()=>new(){["source_id"]=SourceId,["platform_api_version"]="37.1"};
    public const string Platform="{\"alternatives\":[{\"platform\":{\"introduced\":[37,1],\"removed\":[40,0]}}],\"evidence_ids\":[\""+SourceId+"\"]}";
    private static LifecycleCase Change(string id,Action<JsonObject,JsonObject> change,string expected)
    {var entry=Entry();var source=Source();change(entry,source);return new(id,entry,source,expected);}
    public static string Unknown(string reason,string? version="37.1",string? api="{\"since\":\"37.1\"}")=>
        "{\"unknown_reason\":\""+reason+"\",\"x-raw\":{\"api_lifecycle\":"+(api ?? "null")+",\"platform_api_version\":"+(version is null ? "null" : "\""+version+"\"")+",\"source_id\":\""+SourceId+"\"}}";
    public static IEnumerable<object[]> All=>Generate().Select(value=>new object[]{value});
    public static IEnumerable<LifecycleCase> Generate()
    {
        yield return new("LIFE-01-own-lifecycle-overrides-container",Entry(),Source(),Platform);
        yield return Change("LIFE-02-leading-zero-version",(entry,_)=>entry["extensions"]!["api_lifecycle"]!["since"]="0037.01",Platform);
        yield return Change("LIFE-03-explicit-unknown-retains-raw",(entry,_)=>entry["extensions"]!["lifecycle_unknown_reason"]="Fixture source gap",Unknown("Fixture source gap"));
        yield return Change("LIFE-04-removed-before-introduced",(entry,_)=>entry["extensions"]!["api_lifecycle"]!["removed"]="37.0",Unknown("Malformed source lifecycle interval",api:"{\"removed\":\"37.0\",\"since\":\"37.1\"}"));
        yield return Change("LIFE-05-version-syntax-unknown",(entry,_)=>entry["extensions"]!["api_lifecycle"]!["since"]="preview",Unknown("Unknown platform version syntax",api:"{\"since\":\"preview\"}"));
        yield return Change("LIFE-06-version-bound",(entry,_)=>entry["extensions"]!["api_lifecycle"]!["since"]="2147483648",Unknown("Platform version out of range",api:"{\"since\":\"2147483648\"}"));
        var extensionRule="{\"alternatives\":[{\"platform\":{\"introduced\":[37,1],\"removed\":[40,0]}},{\"extensions\":{\"30\":4},\"platform\":{\"introduced\":[0,0],\"removed\":[40,0]}}],\"evidence_ids\":[\""+SourceId+"\"]}";
        yield return Change("LIFE-07-sdk-extension-alternative",(entry,_)=>entry["extensions"]!["sdk_extensions"]=new JsonArray(
            new JsonObject{["sdk_id"]="0",["minimum_version"]="37.1"},new JsonObject{["sdk_id"]="30",["minimum_version"]="4"}),extensionRule);
        yield return Change("LIFE-08-inconsistent-explicit-platform-evidence",(entry,_)=>entry["extensions"]!["sdk_extensions"]=new JsonArray(new JsonObject{["sdk_id"]="0",["minimum_version"]="36"}),Unknown("Inconsistent explicit since/sdk platform evidence"));
        yield return Change("LIFE-09-extension-does-not-invent-platform-alternative",(entry,_)=>
        {entry["extensions"]!["api_lifecycle"]=new JsonObject();entry["extensions"]!["sdk_extensions"]=new JsonArray(new JsonObject{["sdk_id"]="30",["minimum_version"]="4"});},
            "{\"alternatives\":[{\"extensions\":{\"30\":4},\"platform\":{\"introduced\":[0,0],\"removed\":[40,0]}}],\"evidence_ids\":[\""+SourceId+"\"]}");
        yield return Change("LIFE-10-extension-bound",(entry,_)=>entry["extensions"]!["sdk_extensions"]=new JsonArray(
            new JsonObject{["sdk_id"]="0",["minimum_version"]="37.1"},new JsonObject{["sdk_id"]="30",["minimum_version"]="2147483648"}),Unknown("Extension minimum out of range"));
        yield return Change("LIFE-11-nonsdk-exact-platform-point",(entry,_)=>entry["extensions"]!["source_kind"]="source-signature",
            "{\"alternatives\":[{\"platform_point\":[37,1]}],\"evidence_ids\":[\""+SourceId+"\"]}");
        yield return Change("LIFE-12-major-alone-retains-unknown",(entry,source)=>{entry["extensions"]!["source_kind"]="source-signature";source["platform_api_version"]="37";},Unknown("Source API major alone does not prove minor applicability","37"));
        yield return Change("LIFE-13-missing-platform-retains-unknown",(entry,source)=>{entry["extensions"]!["source_kind"]="source-signature";source.Remove("platform_api_version");},Unknown("Source API major alone does not prove minor applicability",null));
    }
    public static void AssertCase(LifecycleCase test,ILifecycleCaseProvider provider)
    {var entry=test.Entry.ToJsonString();var source=test.Source.ToJsonString();Assert.Equal(test.Expected,provider.Project(test.Entry,test.Source));Assert.Equal(entry,test.Entry.ToJsonString());Assert.Equal(source,test.Source.ToJsonString());}
}
public sealed class ScriptedLifecycleProvider(LifecycleCase test,string? wrong=null):ILifecycleCaseProvider
{
    public string Project(JsonObject entry,JsonObject source)
    {Assert.Same(test.Entry,entry);Assert.Same(test.Source,source);return wrong ?? test.Expected;}
}
[Trait("Gate","Mock")]
public sealed class CatalogLifecycleMockTests
{
    [Theory,MemberData(nameof(CatalogLifecycleCases.All),MemberType=typeof(CatalogLifecycleCases))]
    public void CorrectRepliesPass(LifecycleCase test)
    {Assert.NotNull(JsonNode.Parse(test.Expected));CatalogLifecycleCases.AssertCase(test,new ScriptedLifecycleProvider(test));}
    [Theory,MemberData(nameof(CatalogLifecycleCases.All),MemberType=typeof(CatalogLifecycleCases))]
    public void ChangedRuleOrReasonIsRejected(LifecycleCase test)
    {
        var expected=JsonNode.Parse(test.Expected)!.AsObject();
        foreach(var property in expected.ToArray())
        {
            var changed=expected.DeepClone().AsObject();changed[property.Key]="wrong";
            Assert.NotNull(Record.Exception(()=>CatalogLifecycleCases.AssertCase(test,new ScriptedLifecycleProvider(test,changed.ToJsonString()))));
            var omitted=expected.DeepClone().AsObject();omitted.Remove(property.Key);
            Assert.NotNull(Record.Exception(()=>CatalogLifecycleCases.AssertCase(test,new ScriptedLifecycleProvider(test,omitted.ToJsonString()))));
        }
        if(expected["x-raw"] is JsonObject raw)
        foreach(var property in raw.ToArray())
        {
            var changed=expected.DeepClone().AsObject();changed["x-raw"]![property.Key]="wrong";
            Assert.NotNull(Record.Exception(()=>CatalogLifecycleCases.AssertCase(test,new ScriptedLifecycleProvider(test,changed.ToJsonString()))));
        }
    }
}
