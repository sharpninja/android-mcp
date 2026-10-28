using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record ContractCall(string Operation,JsonObject Entry);
public sealed record ContractReply(string Outcome,string? Kind=null,string? SchemaHash=null,string? Contract=null);
public sealed record ContractCase(string Id,ContractCall Call,ContractReply Expected);
public interface IContractCaseProvider { ContractReply Execute(ContractCall call); }
public static class CatalogContractCases
{
    public const string Body="{\"arguments\":[{\"codec_status\":\"unknown\",\"status\":\"primitive\",\"text\":\"int\"}],\"canonical_signature\":\"java:a.A#x(int)\",\"codec_identity\":null,\"codec_status\":\"unknown\",\"contract_format_version\":3,\"hidl_results\":[],\"kind\":\"method\",\"oneway\":null,\"parameter_semantics\":[{\"direction\":null,\"wire_annotations\":[]}],\"return_type\":{\"codec_status\":\"unknown\",\"status\":\"primitive\",\"text\":\"void\"},\"wire_annotations\":[]}";
    public static JsonObject Entry()=>JsonNode.Parse("""
    {"canonical_signature":"java:a.A#x(int)","kind":"method","arguments":[{"text":"int","status":"primitive","reason":"source reason","codec_status":"unknown"}],"return_type":{"text":"void","status":"primitive","reason":"source reason","codec_status":"unknown"},"codec_status":"unknown","permission":{"op":"unknown","reason":"source only"},"extensions":{"language":"java","parameters":[{"direction":null,"annotations":[]}],"annotations":[]}}
    """)!.AsObject();
    public static ContractReply Contract(string body=Body)=>new("ok",SchemaHash:Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body))),Contract:body);
    private static ContractCase Change(string id,Action<JsonObject> change,string body=Body)
    { var input=Entry();change(input);return new(id,new("contract",input),Contract(body)); }
    public static IEnumerable<object[]> All=>Generate().Select(value=>new object[]{value});
    public static IEnumerable<ContractCase> Generate()
    {
        yield return new("CONTRACT-01-literal-version3-body",new("contract",Entry()),Contract());
        yield return Change("CONTRACT-02-permission-and-provenance-not-codec",entry=>
        { entry["permission"]=new JsonObject{["op"]="permission",["name"]="fixture.READ"};entry["provenance"]=new JsonArray(new JsonObject{["line"]=99});entry["extensions"]!["runtime_notes"]="No authority inferred"; });
        yield return Change("CONTRACT-03-type-ref-reason-ignored",entry=>entry["arguments"]![0]!["reason"]="Changed source evidence");
        yield return Change("CONTRACT-04-argument-type-is-contract",entry=>entry["arguments"]![0]!["text"]="long",Body.Replace("\"text\":\"int\"","\"text\":\"long\"",StringComparison.Ordinal));
        yield return Change("CONTRACT-05-return-type-is-contract",entry=>entry["return_type"]!["text"]="int",Body.Replace("\"text\":\"void\"","\"text\":\"int\"",StringComparison.Ordinal));
        yield return Change("CONTRACT-06-codec-pin-is-contract",entry=>entry["arguments"]![0]!["codec_id"]="fixture-codec",
            Body.Replace("\"arguments\":[{\"codec_status\"","\"arguments\":[{\"codec_id\":\"fixture-codec\",\"codec_status\"",StringComparison.Ordinal));
        yield return Change("CONTRACT-07-permission-annotation-filtered",entry=>entry["extensions"]!["annotations"]=new JsonArray(new JsonObject{["name"]="RequiresPermission",["arguments"]="fixture.READ"}));
        yield return Change("CONTRACT-08-wire-nullability-retained",entry=>entry["extensions"]!["annotations"]=new JsonArray(new JsonObject{["name"]="android.annotation.Nullable",["arguments"]=""}),
            Body[..^3]+"[{\"arguments\":\"\",\"name\":\"android.annotation.Nullable\"}]}" );
        yield return Change("CONTRACT-09-parameter-direction-retained",entry=>entry["extensions"]!["parameters"]![0]!["direction"]="out",Body.Replace("\"direction\":null","\"direction\":\"out\"",StringComparison.Ordinal));
        foreach(var (id,own,inherited) in new (string,bool?,bool?)[]{("own",true,false),("inherited",false,true),("false",false,false),("explicit-null",null,null)})
            yield return Change("CONTRACT-10-oneway-"+id,entry=>{entry["extensions"]!["oneway"]=own;entry["extensions"]!["interface_oneway"]=inherited;},
                Body.Replace("\"oneway\":null","\"oneway\":"+(own==true || inherited==true ? "true" : "false"),StringComparison.Ordinal));
        var typeBody="{\"arguments\":[],\"canonical_signature\":\"aidl:a.A\",\"codec_identity\":null,\"codec_status\":\"unknown\",\"contract_format_version\":3,\"declared_type_shape\":{\"fields\":[{\"constant\":null,\"declaration_order\":0,\"name\":\"x\",\"type\":{\"codec_status\":\"unknown\",\"status\":\"primitive\",\"text\":\"int\"},\"wire_annotations\":[]}],\"kind\":\"aidl-parcelable\",\"qualified_name\":\"a.A\",\"supertypes\":[]},\"hidl_results\":[],\"kind\":\"aidl-parcelable\",\"oneway\":null,\"parameter_semantics\":[],\"return_type\":null,\"wire_annotations\":[]}";
        JsonObject Type()=>JsonNode.Parse("""
        {"canonical_signature":"aidl:a.A","kind":"aidl-parcelable","arguments":[],"codec_status":"unknown","extensions":{"declared_type_shape":{"kind":"aidl-parcelable","qualified_name":"a.A","header":"parcelable A","fields":[{"name":"x","type":{"text":"int","status":"primitive","codec_status":"unknown","reason":"source only"},"constant":null,"declaration_order":0,"wire_annotations":[]}],"supertypes":[]}}}
        """)!.AsObject();
        yield return new("CONTRACT-11-structured-shape",new("contract",Type()),Contract(typeBody));
        var header=Type();header["extensions"]!["declared_type_shape"]!["header"]="different source header";
        yield return new("CONTRACT-12-source-header-not-wire-contract",new("contract",header),Contract(typeBody));
        var field=Type();field["extensions"]!["declared_type_shape"]!["fields"]![0]!["type"]!["text"]="long";
        yield return new("CONTRACT-13-structured-field-affects-contract",new("contract",field),Contract(typeBody.Replace("\"text\":\"int\"","\"text\":\"long\"",StringComparison.Ordinal)));
        foreach(var kind in new[]{"class","interface","enum","annotation","type","aidl-interface","aidl-parcelable","aidl-union","aidl-enum","package","unsupported","enum-constant","constructor","method","field","property","aidl-member"})
        {
            var input=Entry();input["kind"]=kind;
            var expected=new[]{"class","interface","enum","annotation","type","aidl-interface","aidl-parcelable","aidl-union","aidl-enum"}.Contains(kind) ? "type" :
                new[]{"package","unsupported","enum-constant"}.Contains(kind) ? "other" : "binding";
            yield return new("KIND-01-"+kind,new("kind",input),new("ok",Kind:expected));
        }
        foreach(var flag in new[]{"aidl_declaration_kind","enum_constant","language"})
        {
            var input=Entry();input["kind"]="field";input["extensions"]![flag]=flag=="enum_constant" ? JsonValue.Create(true) : JsonValue.Create(flag=="language" ? "hidl" : "field");
            yield return new("KIND-02-field-"+flag,new("kind",input),new("ok",Kind:"other"));
        }
        foreach(var kind in new string?[]{null,"invented"})
        { var input=Entry();input["kind"]=kind;yield return new("KIND-03-invalid-"+(kind ?? "null"),new("kind",input),new("invalid")); }
    }
    public static void AssertCase(ContractCase test,IContractCaseProvider provider)
    { var before=test.Call.Entry.ToJsonString();Assert.Equal(test.Expected,provider.Execute(test.Call));Assert.Equal(before,test.Call.Entry.ToJsonString()); }
}
public sealed class ScriptedContractProvider(ContractCase expected,int corrupt=-1):IContractCaseProvider
{
    public ContractReply Execute(ContractCall call)
    {
        Assert.Same(expected.Call,call);var result=expected.Expected;
        return corrupt switch{0=>result with{Outcome="wrong"},1=>result with{Kind="wrong"},2=>result with{SchemaHash="wrong"},3=>result with{Contract="wrong"},_=>result};
    }
}
[Trait("Gate","Mock")]
public sealed class CatalogContractMockTests
{
    [Theory,MemberData(nameof(CatalogContractCases.All),MemberType=typeof(CatalogContractCases))]
    public void CorrectMocksPass(ContractCase test)=>CatalogContractCases.AssertCase(test,new ScriptedContractProvider(test));
    [Theory,MemberData(nameof(CatalogContractCases.All),MemberType=typeof(CatalogContractCases))]
    public void EveryWrongReplyFieldRejected(ContractCase test)
    { for(var field=0;field<4;field++) Assert.NotNull(Record.Exception(()=>CatalogContractCases.AssertCase(test,new ScriptedContractProvider(test,field)))); }
}
