using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources.Java;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

public sealed record JvmCase(string Id, string? Type, JsonObject? Declaration, string? Expected,IReadOnlyDictionary<string,string>? LegacyOwners=null)
{
    public IReadOnlyDictionary<string,string> KnownOwners=>LegacyOwners??JvmContractCases.Owners;
}
public static class JvmContractCases
{
    public static readonly IReadOnlyDictionary<string,string> Owners = new Dictionary<string,string>
        { ["a.C"]="a/C", ["a.C.Inner"]="a/C$Inner", ["java.lang.String"]="java/lang/String", ["java.util.List"]="java/util/List" };
    public static readonly IReadOnlyList<JvmCase> All = Create();
    public static IEnumerable<object[]> Cases => All.Select(x => new object[] { x.Id });
    public static JvmCase Get(string id) => All.Single(x => x.Id == id);
    public static JsonObject Declaration(string kind, string? owner="a.C", string? binary="a/C", string? result="void", params string[] parameters) =>
        JavaContractCases.Object(("kind",kind),("owner",owner),("name","go"),("return_type",result),
            ("parameters",JavaContractCases.Array(parameters.Select(x => (JsonNode)JavaContractCases.Object(("type",x))).ToArray())),
            ("extras",JavaContractCases.Object(("owner_binary_name",binary))));
    private static IReadOnlyList<JvmCase> Create()
    {
        var result = new List<JvmCase>();
        foreach(var (type,descriptor) in new[]{("byte","B"),("char","C"),("double","D"),("float","F"),("int","I"),("long","J"),("short","S"),("boolean","Z"),("void","V")})
            result.Add(new("ENC-"+type,type,null,descriptor));
        foreach(var (id,type,expected) in new (string,string?,string?)[] {
            ("array"," int [] [] ","[[I"),("varargs","int...","[I"),("reference","java.lang.String[]","[Ljava/lang/String;"),
            ("nested","a.C.Inner","La/C$Inner;"),("generic","java.util.List<java.lang.String>","Ljava/util/List;"),
            ("nestedgeneric","java.util.List<java.util.List<int[]>>[]","[Ljava/util/List;"),
            ("null",null,null),("empty","",null),("voidarray","void[]",null),("T","T",null),("String","String",null),
            ("wildcard","? extends java.lang.String",null),("unclosed","java.util.List<T",null),("negative","java.util.List>T<",null),
            ("unknownouter","other.List<java.lang.String>",null),("partial","C.Inner",null) }) result.Add(new("ENC-"+id,type,null,expected));
        result.Add(new("KEY-primitives",null,Declaration("method",parameters:["java.lang.String[]","int"]),"a/C#go([Ljava/lang/String;I)V",new Dictionary<string,string>{{"a.C","a/C"},{"java.lang.String","java/lang/String"}}));
        result.Add(new("KEY-T",null,Declaration("method",result:"T"),null,new Dictionary<string,string>{{"a.C","a/C"}}));
        result.Add(new("KEY-field",null,JavaContractCases.Object(("kind","field"),("owner","a.C.Inner"),("name","X"),("extras",JavaContractCases.Object(("owner_binary_name","a/C$Inner")))),null,new Dictionary<string,string>{{"a.C.Inner","a/C$Inner"}}));
        var unqualified=result.FindIndex(x=>x.Id=="ENC-String");result[unqualified]=result[unqualified] with{LegacyOwners=new Dictionary<string,string>{{"java.lang.String","java/lang/String"}}};
        foreach(var kind in new[]{"class","interface","enum","annotation"}) result.Add(new("KEY-"+kind,null,Declaration(kind),"a/C"));
        result.Add(new("KEY-constructor",null,Declaration("constructor",parameters:["int","a.C.Inner"]),"a/C#<init>(ILa/C$Inner;)V"));
        result.Add(new("KEY-voidargument",null,Declaration("method",parameters:["void"]),null));
        result.Add(new("KEY-unknownargument",null,Declaration("method",parameters:["String"]),null));
        result.Add(new("KEY-owner-mismatch",null,Declaration("method",binary:"a/Other"),null));
        result.Add(new("KEY-owner-unknown",null,Declaration("method","a.Other","a/Other"),null));
        result.Add(new("KEY-owner-null",null,Declaration("method",binary:null),null));
        result.Add(new("KEY-unsupported",null,Declaration("unsupported"),null));
        return result;
    }
    public static void AssertContract(JvmCase test, IJvmSourceLinker provider) => Assert.Equal(test.Expected,
        test.Declaration is null ? provider.Encode(test.Type,"a.C",test.KnownOwners) : provider.Key((JsonObject)test.Declaration.DeepClone(),test.KnownOwners));
}

public sealed record JavaContextCase(string Id, string? Failure)
{
    public JsonArray Sources()
    {
        var sources=JavaContractCases.Array(JavaContractCases.Object(("kind","java-source"),("path","C.java"),("metadata_cache",JavaContractCases.Object(("path","cache.gz"),("sha256","abc"),("parser_sha256","def")))));
        if(Id=="CTX-nonjava")sources.Add(JavaContractCases.Object(("kind","aidl-source"),("path","I.aidl")));
        return sources;
    }
    public JsonArray Reports()
    {
        var report=JavaContractCases.Object(("cache_path","cache.gz"),("cache_sha256","abc"),("parser_sha256","def"),("package","a"),
            ("imports",JavaContractCases.Object(("List","java.util.List"))),("wildcard_imports",JavaContractCases.Array(JsonValue.Create("other"))),
            ("parser","tested"),("parse_errors",0));
        if(Failure=="cache") report=JavaContractCases.Object(("cache_path","cache.gz"),("cache_sha256","wrong"),("parser_sha256","def"));
        if(Failure=="parser") report["parser_sha256"]="wrong";
        if(Failure=="missing") return new();
        return JavaContractCases.Array(report);
    }
}
public static class JavaContextCases
{
    public static readonly IReadOnlyList<JavaContextCase> All=[new("CTX-imports",null),new("CTX-cache","cache"),new("CTX-parser","parser"),new("CTX-missing","missing"),new("CTX-nonjava",null)];
    public static IEnumerable<object[]> Cases=>All.Select(x=>new object[]{x.Id});
    public static JavaContextCase Get(string id)=>All.Single(x=>x.Id==id);
    public static void AssertContract(JavaContextCase test,IJavaFileContext provider)
    {
        var sources=test.Sources(); var reports=test.Reports(); var reportsBefore=reports.ToJsonString();
        if(test.Failure is not null) Assert.Contains("context cache mismatch",Assert.Throws<InvalidDataException>(()=>provider.Attach(sources,reports)).Message);
        else
        {
            provider.Attach(sources,reports);
            SourceExtractionContractCases.AssertSubset(JavaContractCases.Object(("package","a"),("imports",JavaContractCases.Object(("List","java.util.List"))),
                ("wildcard_imports",JavaContractCases.Array(JsonValue.Create("other"))),("parser","tested"),("parse_errors",0),("excluded_implementation_nodes",null),
                ("static_import_resolution","Static/type imports not separately classified; original source retained. No unresolved wildcard/type-variable identity inferred.")),sources[0]!["java_file_context"]);
            if(sources.Count>1)Assert.False(sources[1]!.AsObject().ContainsKey("java_file_context"));
            sources[0]!["java_file_context"]!["imports"]!["List"]="mutated";
        }
        Assert.Equal(reportsBefore,reports.ToJsonString());
    }
}
