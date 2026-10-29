using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources.Java;
using Xunit;
using static AndroidMcp.BuildTool.Tests.Sources.Java.JavaContractCases;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

public sealed record JavaContextBodyCase(string Id,JsonArray Sources,JsonArray Reports,JsonArray ExpectedSources,string? Failure=null,string? Detail=null);

// Original attach/encode clauses absent from the previous finite vectors.
// This is not a new original-test-method count or a historical identity claim.
public static class JavaHelperBodyCases
{
    public const string StaticResolution="Static/type imports not separately classified; original source retained. No unresolved wildcard/type-variable identity inferred.";
    public static readonly string[] ContextFields=["package","imports","wildcard_imports","parser","parse_errors","excluded_implementation_nodes","static_import_resolution"];
    private static JsonObject Source(string path="C.java",string? hash="abc",string? parser="def",string? cachePath="cache.gz")=>
        Object(("kind","java-source"),("path",path),("metadata_cache",Object(("path",cachePath),("sha256",hash),("parser_sha256",parser))),("java_file_context",Object(("stale",true))));
    private static JsonObject Report(string? hash="abc",string? parser="def",string? cachePath="cache.gz",string? marker=null)
    {
        var report=Object(("cache_path",cachePath),("cache_sha256",hash),("parser_sha256",parser));
        if(marker is not null)
        {
            var metadata=Context(marker);metadata.Remove("static_import_resolution");
            foreach(var field in metadata)report[field.Key]=field.Value?.DeepClone();
            report["ignored_extra"]="not projected";report["static_import_resolution"]="not trusted";
        }
        return report;
    }
    private static JsonObject Context(string? marker=null)=>Object(("package",marker is null?null:"a."+marker),
        ("imports",marker is null?null:Object(("List","java.util.List"))),("wildcard_imports",marker is null?null:Array(JsonValue.Create("other"))),
        ("parser",marker is null?null:"tested-"+marker),("parse_errors",marker is null?null:2),
        ("excluded_implementation_nodes",marker is null?null:Object(("block",3))),("static_import_resolution",StaticResolution));
    private static JsonArray Attached(JsonArray sources,int index,string? marker=null)
    {var expected=(JsonArray)sources.DeepClone();expected[index]!["java_file_context"]=Context(marker);return expected;}
    private static IReadOnlyList<JavaContextBodyCase> CreateContexts()
    {
        var result=new List<JavaContextBodyCase>();
        void Success(string id,JsonArray sources,JsonArray reports,string? marker=null)=>result.Add(new(id,sources,reports,Attached(sources,0,marker)));
        foreach(var (id,hash,parser) in new (string,string?,string?)[]{("empty-cache","","def"),("empty-parser","abc",""),("null-hashes",null,null)})
            Success("CTX-BODY-"+id,Array(Source(hash:hash,parser:parser)),Array(Report(hash:hash,parser:parser)));
        var source=Array(Source(hash:""));result.Add(new("CTX-BODY-empty-null-mismatch",source,Array(Report(hash:null)),(JsonArray)source.DeepClone(),"mismatch","C.java"));
        Success("CTX-BODY-last-report-wins",Array(Source()),Array(Report(hash:"wrong",marker:"first"),Report(marker:"last")),"last");
        source=Array(Source());result.Add(new("CTX-BODY-last-report-rejects",source,Array(Report(marker:"first"),Report(hash:"wrong",marker:"last")),(JsonArray)source.DeepClone(),"mismatch","C.java"));
        Success("CTX-BODY-six-fields-overwrite",Array(Source()),Array(Report(marker:"all")),"all");
        Success("CTX-BODY-missing-optionals",Array(Source()),Array(Report()));
        source=Array(Object(("kind","aidl-source"),("path","I.aidl"),("untouched",true)));
        result.Add(new("CTX-BODY-nonjava-no-cache-access",source,Array(Object(("cache_path","cache.gz"))),(JsonArray)source.DeepClone()));
        source=Array(Source("First.java",cachePath:"first"),Source("Bad.java",hash:"wrong",cachePath:"bad"),Source("Last.java",cachePath:"last"));
        result.Add(new("CTX-BODY-sequential-failure",source,Array(Report(cachePath:"first",marker:"first"),Report(cachePath:"bad"),Report(cachePath:"last",marker:"last")),Attached(source,0,"first"),"mismatch","Bad.java"));
        result.Add(new("CTX-BODY-empty",new(),new(),new()));
        result.Add(new("CTX-BODY-report-key-before-empty-sources",new(),Array(Object(("cache_sha256","abc"))),new(),"missing","cache_path"));
        source=Array(Object(("path","Missing.java")));result.Add(new("CTX-BODY-required-source-kind",source,new(),(JsonArray)source.DeepClone(),"missing","kind"));
        source=Array(Source());source[0]!["metadata_cache"]!.AsObject().Remove("sha256");
        result.Add(new("CTX-BODY-required-cache-hash",source,Array(Report()),(JsonArray)source.DeepClone(),"missing","sha256"));
        source=Array(Source(hash:"wrong"));source[0]!["metadata_cache"]!.AsObject().Remove("parser_sha256");
        var report=Report();report.Remove("parser_sha256");
        result.Add(new("CTX-BODY-mismatch-short-circuit",source,Array(report),(JsonArray)source.DeepClone(),"mismatch","C.java"));
        source=Array(Source());report=Report();report.Remove("parser_sha256");
        result.Add(new("CTX-BODY-required-report-parser",source,Array(report),(JsonArray)source.DeepClone(),"missing","parser_sha256"));
        Success("CTX-BODY-null-path",Array(Source(cachePath:null)),Array(Report(cachePath:null)));
        Success("CTX-BODY-empty-path",Array(Source(cachePath:"")),Array(Report(cachePath:"")));
        source=Array(Source(cachePath:null));result.Add(new("CTX-BODY-null-path-not-empty",source,Array(Report(cachePath:"")),(JsonArray)source.DeepClone(),"mismatch","C.java"));
        return result;
    }
    public static readonly IReadOnlyList<JavaContextBodyCase> Contexts=CreateContexts();
    public static IEnumerable<object[]> ContextCases=>Contexts.Select(test=>new object[]{test.Id});
    public static JavaContextBodyCase ContextCase(string id)=>Contexts.Single(test=>test.Id==id);
    public static void AssertContext(JavaContextBodyCase test,IJavaFileContext provider)
    {
        var sources=(JsonArray)test.Sources.DeepClone();var reports=(JsonArray)test.Reports.DeepClone();var before=reports.ToJsonString();
        if(test.Failure=="mismatch")Assert.Equal("Java context cache mismatch "+test.Detail,Assert.Throws<InvalidDataException>(()=>provider.Attach(sources,reports)).Message);
        else if(test.Failure=="missing")Assert.Equal(test.Detail,Assert.Throws<KeyNotFoundException>(()=>provider.Attach(sources,reports)).Message);
        else provider.Attach(sources,reports);
        Assert.True(JsonNode.DeepEquals(test.ExpectedSources,sources),"Exact post-call source state: "+test.Id);
        Assert.Equal(before,reports.ToJsonString());
    }
    internal static void CopyExpected(JavaContextBodyCase test,JsonArray sources)
    {sources.Clear();foreach(var source in test.ExpectedSources)sources.Add(source?.DeepClone());}
    internal static void ThrowExpected(JavaContextBodyCase test)
    {
        if(test.Failure=="mismatch")throw new InvalidDataException("Java context cache mismatch "+test.Detail);
        if(test.Failure=="missing")throw new KeyNotFoundException(test.Detail);
    }
    public static readonly IReadOnlyList<JvmCase> Whitespace=[
        new("JVM-BODY-strip-primitive","\u001cint\u001f",null,"I"),
        new("JVM-BODY-strip-arrays","\u001c int []\u001e []\u001f",null,"[[I"),
        new("JVM-BODY-strip-erased-outer","\u001cjava.util.List<T>\u001f",null,"Ljava/util/List;"),
        new("JVM-BODY-strip-before-empty-owner-lookup","\u001c\u001f",null,"Lempty;",new Dictionary<string,string>{{"","empty"}})
    ];
    public static IEnumerable<object[]> WhitespaceCases=>Whitespace.Select(test=>new object[]{test.Id});
    public static JvmCase WhitespaceCase(string id)=>Whitespace.Single(test=>test.Id==id);
}
