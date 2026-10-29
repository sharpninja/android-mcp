using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Publication;

public sealed record RehydrationCase(string Id,RehydrationPlanRequest Request,string? Error,int Rows=2,int Unique=2,long KnownBytes=3,int UnknownSizes=1);
public static class RehydrationContractCases
{
    public static JsonObject Recipe()
    {
        var a=new JsonObject{["path"]="api/A.txt",["local_path"]="blobs/a",["sha256"]=PublicationTestData.Hash(PublicationTestData.Bytes("abc")),["size_bytes"]=3,["url"]="https://example.invalid/a",["metadata_cache"]=new JsonObject{["path"]="cache/a.gz",["sha256"]=new string('1',64),["parser_sha256"]=new string('2',64),["source_sha256"]=PublicationTestData.Hash(PublicationTestData.Bytes("abc"))}};
        var b=new JsonObject{["path"]="snapshot/api.txt",["local_path"]="blobs/b",["sha256"]=PublicationTestData.Hash(PublicationTestData.Bytes("def")),["url"]="https://example.invalid/mutable",["transport"]=new JsonObject{["immutable_url"]=false,["required_local_snapshot"]="snapshots/api.txt"}};
        return new(){["recipe_version"]=1,["release"]="fixture-release",["inventory"]=new JsonObject{["sources"]=new JsonArray(a,b)},["producer"]=new JsonObject{["path"]="producer",["files"]=new JsonObject{["tool.cs"]=new string('3',64)}},["expected"]=new JsonObject{["global_complete"]=false,["entries"]=17},["environment"]=new JsonObject{["legacy_only"]="preserve evidence, never execute"},["provenance"]=new JsonObject{["origin"]="immutable fixture"},["limits"]=new JsonObject{["historical_byte_identity"]="not established"}};
    }
    public static JsonObject Source(JsonObject recipe,int index)=>recipe["inventory"]!["sources"]![index]!.AsObject();
    public static RehydrationPlanRequest Request(Action<JsonObject>? mutate=null,Action<JsonObject>? lockMutate=null,bool gzip=false,int? limit=null,string? expected=null,bool allowFile=false)
    {
        var root=Recipe();mutate?.Invoke(root);
        var bytes=JsonSerializer.SerializeToUtf8Bytes(root);
        if(gzip){using var output=new MemoryStream();using(var zip=new GZipStream(output,CompressionLevel.SmallestSize,true))zip.Write(bytes);bytes=output.ToArray();}
        var path="rehydration/fixture.json"+(gzip?".gz":"");
        var pin=new JsonObject{["recipe_file"]=path,["sha256"]=PublicationTestData.Hash(bytes),["source_rows"]=root["inventory"]!["sources"]!.AsArray().Count,["producer_files"]=root["producer"]!["files"]!.AsObject().Count};
        lockMutate?.Invoke(pin);
        return new(new(path,bytes,"application/json"),new("rehydration/fixture.lock.json",JsonSerializer.SerializeToUtf8Bytes(pin),"application/json"),expected,limit,true,allowFile);
    }
    public static IEnumerable<object[]> Cases=>new[]
    {
        new RehydrationCase("REH-01",Request(),null),
        new RehydrationCase("REH-02",Request(gzip:true),null),
        new RehydrationCase("REH-03",Request(limit:1),null),
        new RehydrationCase("REH-04",Request(lockMutate:l=>l["sha256"]=new string('0',64)),"RECIPE_PIN_MISMATCH"),
        new RehydrationCase("REH-05",Request(r=>r["recipe_version"]=2),"UNSUPPORTED_RECIPE"),
        new RehydrationCase("REH-06",Request(r=>r["inventory"]!["sources"]=new JsonArray()),"EMPTY_RECIPE"),
        new RehydrationCase("REH-07",Request(r=>Source(r,0).Remove("sha256")),"INVALID_PIN"),
        new RehydrationCase("REH-08",Request(r=>Source(r,0)["local_path"]="../escape"),"UNSAFE_PATH"),
        new RehydrationCase("REH-09",Request(r=>Source(r,0)["metadata_cache"]!["path"]="a\\b"),"UNSAFE_PATH"),
        new RehydrationCase("REH-10",Request(r=>Source(r,0)["metadata_cache"]!["parser_sha256"]="bad"),"INVALID_PIN"),
        new RehydrationCase("REH-11",Request(r=>Source(r,0)["metadata_cache"]!["source_sha256"]=new string('0',64)),"CACHE_SOURCE_MISMATCH"),
        new RehydrationCase("REH-12",Request(r=>r["producer"]!["path"]="/outside"),"UNSAFE_PATH"),
        new RehydrationCase("REH-13",Request(r=>r["producer"]!["files"]!["tool.cs"]="bad"),"INVALID_PIN"),
        new RehydrationCase("REH-14",Request(r=>Source(r,1)["local_path"]="blobs/a"),"CONFLICTING_SOURCE_ALIAS"),
        new RehydrationCase("REH-15",Request(expected:"other-release"),"RELEASE_MISMATCH"),
        new RehydrationCase("REH-16",Request(limit:-1),"INVALID_LIMIT"),
        new RehydrationCase("REH-17",Request(r=>Source(r,0)["url"]="file:///fixture/a"),"UNSUPPORTED_TRANSPORT"),
        new RehydrationCase("REH-18",Request(r=>Source(r,0)["url"]="file:///fixture/a",allowFile:true),null),
        new RehydrationCase("REH-19",Request(r=>Source(r,0)["url"]="https://user:password@example.invalid/a"),"UNSUPPORTED_TRANSPORT"),
        new RehydrationCase("REH-20",Request(r=>Source(r,0)["size_bytes"]=-1),"INVALID_LENGTH"),
        new RehydrationCase("REH-21",Request(lockMutate:l=>l["source_rows"]=99),"LOCK_COUNT_MISMATCH"),
        new RehydrationCase("REH-22",Request(r=>Source(r,1)["transport"]!["required_local_snapshot"]="../outside"),"UNSAFE_PATH"),
        new RehydrationCase("REH-23",Request(lockMutate:l=>l["recipe_file"]="different.json"),"LOCK_PATH_MISMATCH"),
        new RehydrationCase("REH-24",Request(r=>r["producer"]!["files"]!["../outside"]=new string('0',64)),"UNSAFE_PATH")
    }.Select(x=>new object[]{x});
    public static RehydrationPlanResult Correct(RehydrationCase c)
    {
        if(c.Error is not null)return new(null,[new(c.Error,"")]);
        var digest=PublicationTestData.Hash(c.Request.Recipe.Bytes);
        var manifest=JsonSerializer.SerializeToUtf8Bytes(new {recipe_sha256=digest,source_rows=c.Rows,unique_source_paths=c.Unique,selected_paths=Math.Min(c.Request.Limit??c.Unique,c.Unique),network=false,complete=false,historical_byte_identity_claimed=false,expected=new{global_complete=false,entries=17},environment=new{legacy_only="preserve evidence, never execute"},provenance=new{origin="immutable fixture"},limits=new{historical_byte_identity="not established"}});
        return new(new("fixture-release",digest,c.Rows,c.Unique,c.KnownBytes,c.UnknownSizes,[new("snapshot/api.txt","blobs/b","snapshots/api.txt",PublicationTestData.Hash(PublicationTestData.Bytes("def")),null)],
            [new(Category.Sources,Operation.Fetch),new(Category.Sources,Operation.Extract),new(Category.Catalog,Operation.Build),new(Category.Validate,Operation.Catalog),new(Category.Catalog,Operation.Export)],
            new("rehydration-plan.json",manifest,"application/json"),false,false,false,["Owner-supplied mutable snapshots and historical byte identity remain explicit limitations."]),[]);
    }
    public static void AssertCase(RehydrationCase c,IRehydrationPlanner provider)
    {
        var before=c.Request.Recipe.Bytes.ToArray();
        var actual=provider.Plan(c.Request);
        Assert.Equal(before,c.Request.Recipe.Bytes.ToArray());
        Assert.Equal(c.Error is null,actual.IsValid);
        Assert.Equal(c.Error is null?Array.Empty<string>():[c.Error],actual.Issues.Select(x=>x.Code));
        if(c.Error is not null){Assert.Null(actual.Plan);return;}
        var plan=Assert.IsType<RehydrationPlan>(actual.Plan);var expected=Correct(c).Plan!;
        Assert.Equal(expected.Release,plan.Release);Assert.Equal(expected.RecipeSha256,plan.RecipeSha256);
        Assert.Equal(c.Rows,plan.SourceRows);Assert.Equal(c.Unique,plan.UniqueSourcePaths);
        Assert.Equal(c.KnownBytes,plan.KnownSourceBytes);Assert.Equal(c.UnknownSizes,plan.UnknownSizePaths);
        Assert.Equal(expected.RequiredSnapshots,plan.RequiredSnapshots);Assert.Equal(expected.Operations,plan.Operations);
        Assert.False(plan.Network);Assert.False(plan.Complete);Assert.False(plan.HistoricalByteIdentityClaimed);Assert.NotEmpty(plan.Caveats);
        Assert.Equal("rehydration-plan.json",plan.Manifest.RelativePath);
        using var manifest=JsonDocument.Parse(plan.Manifest.Bytes);var root=manifest.RootElement;
        Assert.Equal(plan.RecipeSha256,root.GetProperty("recipe_sha256").GetString());
        Assert.Equal(c.Rows,root.GetProperty("source_rows").GetInt32());Assert.Equal(c.Unique,root.GetProperty("unique_source_paths").GetInt32());
        Assert.Equal(Math.Min(c.Request.Limit??c.Unique,c.Unique),root.GetProperty("selected_paths").GetInt32());
        Assert.False(root.GetProperty("network").GetBoolean());Assert.False(root.GetProperty("complete").GetBoolean());Assert.False(root.GetProperty("historical_byte_identity_claimed").GetBoolean());
        Assert.Equal(17,root.GetProperty("expected").GetProperty("entries").GetInt32());Assert.False(root.GetProperty("expected").GetProperty("global_complete").GetBoolean());
        Assert.Equal("preserve evidence, never execute",root.GetProperty("environment").GetProperty("legacy_only").GetString());
        Assert.Equal("immutable fixture",root.GetProperty("provenance").GetProperty("origin").GetString());
        Assert.Equal("not established",root.GetProperty("limits").GetProperty("historical_byte_identity").GetString());
    }
}
public sealed class ScriptedRehydrationPlanner(RehydrationPlanResult response):IRehydrationPlanner
{
    public RehydrationPlanResult Plan(RehydrationPlanRequest request)=>response;
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class RehydrationMockTests
{
    [Theory,MemberData(nameof(RehydrationContractCases.Cases),MemberType=typeof(RehydrationContractCases))]
    public void CorrectMocksPass(RehydrationCase c)=>RehydrationContractCases.AssertCase(c,new ScriptedRehydrationPlanner(RehydrationContractCases.Correct(c)));
    [Theory,MemberData(nameof(RehydrationContractCases.Cases),MemberType=typeof(RehydrationContractCases))]
    public void IndividualNegativeControlsRejectWrongPinsCountsFlagsAndManifest(RehydrationCase c)
    {
        var correct=RehydrationContractCases.Correct(c);
        var wrong=new List<RehydrationPlanResult>{correct with{Issues=[new("WRONG","")]}};
        if(correct.Plan is { } p)
        {
            foreach(var changed in new[]{p with{Release="other"},p with{RecipeSha256="wrong"},p with{SourceRows=p.SourceRows+1},p with{UniqueSourcePaths=p.UniqueSourcePaths+1},p with{KnownSourceBytes=p.KnownSourceBytes+1},p with{UnknownSizePaths=p.UnknownSizePaths+1},p with{RequiredSnapshots=[]},p with{Operations=[]},p with{Network=true},p with{Complete=true},p with{HistoricalByteIdentityClaimed=true},p with{Caveats=[]},p with{Manifest=new("wrong",p.Manifest.Bytes,p.Manifest.MediaType)}})wrong.Add(correct with{Plan=changed});
            using var document=JsonDocument.Parse(p.Manifest.Bytes);
            foreach(var property in document.RootElement.EnumerateObject())
            {
                var manifest=JsonNode.Parse(p.Manifest.Bytes.Span)!.AsObject();manifest.Remove(property.Name);
                wrong.Add(correct with{Plan=p with{Manifest=p.Manifest with{Bytes=JsonSerializer.SerializeToUtf8Bytes(manifest)}}});
            }
        }
        else wrong.Add(correct with{Issues=[]});
        foreach(var response in wrong)Assert.NotNull(Record.Exception(()=>RehydrationContractCases.AssertCase(c,new ScriptedRehydrationPlanner(response))));
    }
}
