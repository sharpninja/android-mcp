using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Publication;

public sealed record RehydrationBoundaryCase(string Id,RehydrationPlanRequest Request,string? Error,ArchiveRequirement[] Archives);
public static class RehydrationBoundaryCases
{
    private static RehydrationPlanRequest Archive(Action<JsonObject>? change=null,bool pin=true)=>RehydrationContractCases.Request(r=>
    {
        var source=RehydrationContractCases.Source(r,0);
        var transport=new JsonObject{["archive_url"]="https://example.invalid/sdk.zip",["archive_member"]="platforms/android.jar"};
        if(pin)transport["archive_sha256"]=new string('4',64);
        source["transport"]=transport;change?.Invoke(r);
    });
    private static RehydrationPlanRequest Raw(RehydrationPlanRequest input,byte[] bytes)
    {
        var pin=JsonNode.Parse(input.Lock.Bytes.Span)!.AsObject();pin["sha256"]=PublicationTestData.Hash(bytes);
        return input with{Recipe=input.Recipe with{Bytes=bytes},Lock=input.Lock with{Bytes=JsonSerializer.SerializeToUtf8Bytes(pin)}};
    }
    public static IEnumerable<object[]> Cases
    {
        get
        {
            ArchiveRequirement Expected(string? hash)=>new("api/A.txt","blobs/a","https://example.invalid/sdk.zip","platforms/android.jar",hash,PublicationTestData.Hash(PublicationTestData.Bytes("abc")));
            var duplicate=RehydrationContractCases.Request();var raw=System.Text.Encoding.UTF8.GetString(duplicate.Recipe.Bytes.Span);
            var badGzip=RehydrationContractCases.Request(gzip:true);
            return new[]{
                new RehydrationBoundaryCase("BOUND-01",Archive(),null,[Expected(new string('4',64))]),
                new RehydrationBoundaryCase("BOUND-02",Archive(pin:false),null,[Expected(null)]),
                new RehydrationBoundaryCase("BOUND-03",Archive(r=>RehydrationContractCases.Source(r,0)["transport"]!["archive_member"]="../escape"),"UNSAFE_PATH",[]),
                new RehydrationBoundaryCase("BOUND-04",Archive(r=>RehydrationContractCases.Source(r,0)["transport"]!["archive_member"]="/escape"),"UNSAFE_PATH",[]),
                new RehydrationBoundaryCase("BOUND-05",Archive(r=>RehydrationContractCases.Source(r,0)["transport"]!["archive_member"]="a\\b"),"UNSAFE_PATH",[]),
                new RehydrationBoundaryCase("BOUND-06",Archive(r=>RehydrationContractCases.Source(r,0)["transport"]!["archive_sha256"]="bad"),"INVALID_PIN",[]),
                new RehydrationBoundaryCase("BOUND-07",Archive(r=>RehydrationContractCases.Source(r,0)["transport"]!.AsObject().Remove("archive_member")),"INVALID_ARCHIVE_TRANSPORT",[]),
                new RehydrationBoundaryCase("BOUND-08",Archive(r=>RehydrationContractCases.Source(r,0)["transport"]!.AsObject().Remove("archive_url")),"INVALID_ARCHIVE_TRANSPORT",[]),
                new RehydrationBoundaryCase("BOUND-09",Archive(r=>RehydrationContractCases.Source(r,0)["transport"]!["archive_url"]="http://example.invalid/sdk.zip"),"UNSUPPORTED_TRANSPORT",[]),
                new RehydrationBoundaryCase("BOUND-10",RehydrationContractCases.Request(r=>
                {
                    var second=RehydrationContractCases.Source(r,1);second["metadata_cache"]=RehydrationContractCases.Source(r,0)["metadata_cache"]!.DeepClone();
                    second["metadata_cache"]!["sha256"]=new string('9',64);second["metadata_cache"]!["source_sha256"]=second["sha256"]!.GetValue<string>();
                }),"CONFLICTING_CACHE_ALIAS",[]),
                new RehydrationBoundaryCase("BOUND-11",RehydrationContractCases.Request(r=>RehydrationContractCases.Source(r,0)["local_path"]="cache/a.gz"),"PATH_ROLE_CONFLICT",[]),
                new RehydrationBoundaryCase("BOUND-12",RehydrationContractCases.Request(r=>RehydrationContractCases.Source(r,0)["metadata_cache"]!["sha256"]="bad"),"INVALID_PIN",[]),
                new RehydrationBoundaryCase("BOUND-13",RehydrationContractCases.Request(r=>RehydrationContractCases.Source(r,0)["path"]="../escape"),"UNSAFE_PATH",[]),
                new RehydrationBoundaryCase("BOUND-14",Raw(duplicate,PublicationTestData.Bytes("{\"recipe_version\":1,"+raw[1..])),"INVALID_RECIPE",[]),
                new RehydrationBoundaryCase("BOUND-15",Raw(badGzip,PublicationTestData.Bytes("not gzip")),"INVALID_RECIPE",[]),
                new RehydrationBoundaryCase("BOUND-16",RehydrationContractCases.Request(),null,[])
            }.Select(x=>new object[]{x});
        }
    }
    public static RehydrationPlanResult Correct(RehydrationBoundaryCase c)
    {
        if(c.Error is not null)return new(null,[new(c.Error,"")]);
        var baseline=RehydrationContractCases.Correct(new(c.Id,c.Request,null)).Plan!;
        var manifest=JsonNode.Parse(baseline.Manifest.Bytes.Span)!.AsObject();
        manifest["archive_members"]=JsonSerializer.SerializeToNode(c.Archives.Select(a=>new{source_path=a.SourcePath,local_path=a.LocalPath,url=a.Url,member=a.Member,archive_sha256=a.ArchiveSha256,member_sha256=a.MemberSha256,archive_verified=false}));
        manifest["required_snapshots"]=JsonSerializer.SerializeToNode(baseline.RequiredSnapshots.Select(s=>new{source_path=s.SourcePath,local_path=s.LocalPath,bundle_path=s.BundlePath,sha256=s.Sha256,length=s.Length}));
        return new(baseline with{ArchiveMembers=c.Archives,Manifest=baseline.Manifest with{Bytes=JsonSerializer.SerializeToUtf8Bytes(manifest)},Caveats=baseline.Caveats.Concat(["Archive bytes are not verified by planning.","Container SHA256 may be unavailable; member identity remains pinned."]).ToArray()},[]);
    }
    public static void AssertCase(RehydrationBoundaryCase c,IRehydrationPlanner provider)
    {
        var before=c.Request.Recipe.Bytes.ToArray();var result=provider.Plan(c.Request);Assert.Equal(before,c.Request.Recipe.Bytes.ToArray());
        Assert.Equal(c.Error is null,result.IsValid);Assert.Equal(c.Error is null?Array.Empty<string>():[c.Error],result.Issues.Select(x=>x.Code));
        if(c.Error is not null){Assert.Null(result.Plan);return;}
        var plan=Assert.IsType<RehydrationPlan>(result.Plan);Assert.NotNull(plan.ArchiveMembers);Assert.Equal(c.Archives,plan.ArchiveMembers);
        Assert.False(plan.Network);Assert.False(plan.Complete);Assert.False(plan.HistoricalByteIdentityClaimed);
        using var doc=JsonDocument.Parse(plan.Manifest.Bytes);var archives=doc.RootElement.GetProperty("archive_members").EnumerateArray().ToArray();Assert.Equal(c.Archives.Length,archives.Length);
        for(var i=0;i<c.Archives.Length;i++){var expected=c.Archives[i];var a=archives[i];Assert.Equal(expected.SourcePath,a.GetProperty("source_path").GetString());Assert.Equal(expected.LocalPath,a.GetProperty("local_path").GetString());Assert.Equal(expected.Url,a.GetProperty("url").GetString());Assert.Equal(expected.Member,a.GetProperty("member").GetString());Assert.Equal(expected.ArchiveSha256,a.GetProperty("archive_sha256").GetString());Assert.Equal(expected.MemberSha256,a.GetProperty("member_sha256").GetString());Assert.False(a.GetProperty("archive_verified").GetBoolean());}
        if(c.Archives.Length>0)Assert.Contains(plan.Caveats,x=>x.Contains("not verified",StringComparison.OrdinalIgnoreCase));
        if(c.Archives.Any(x=>x.ArchiveSha256 is null))Assert.Contains(plan.Caveats,x=>x.Contains("Container",StringComparison.OrdinalIgnoreCase));
        var snapshot=Assert.Single(doc.RootElement.GetProperty("required_snapshots").EnumerateArray());Assert.Equal("snapshot/api.txt",snapshot.GetProperty("source_path").GetString());Assert.Equal("blobs/b",snapshot.GetProperty("local_path").GetString());Assert.Equal("snapshots/api.txt",snapshot.GetProperty("bundle_path").GetString());Assert.Equal(PublicationTestData.Hash(PublicationTestData.Bytes("def")),snapshot.GetProperty("sha256").GetString());Assert.Equal(JsonValueKind.Null,snapshot.GetProperty("length").ValueKind);
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class RehydrationBoundaryMockTests
{
    [Theory,MemberData(nameof(RehydrationBoundaryCases.Cases),MemberType=typeof(RehydrationBoundaryCases))]
    public void CorrectMocksPass(RehydrationBoundaryCase c)=>RehydrationBoundaryCases.AssertCase(c,new ScriptedRehydrationPlanner(RehydrationBoundaryCases.Correct(c)));
    [Theory,MemberData(nameof(RehydrationBoundaryCases.Cases),MemberType=typeof(RehydrationBoundaryCases))]
    public void IndividualMetadataAndVerificationMutantsReject(RehydrationBoundaryCase c)
    {
        var good=RehydrationBoundaryCases.Correct(c);var wrong=new List<RehydrationPlanResult>{good with{Issues=[new("WRONG","")]}};
        if(good.Plan is { } plan)
        {
            wrong.Add(good with{Plan=plan with{ArchiveMembers=null}});wrong.Add(good with{Plan=plan with{Network=true}});wrong.Add(good with{Plan=plan with{Complete=true}});wrong.Add(good with{Plan=plan with{HistoricalByteIdentityClaimed=true}});
            foreach(var rootKey in new[]{"archive_members","required_snapshots"}){var json=JsonNode.Parse(plan.Manifest.Bytes.Span)!.AsObject();json.Remove(rootKey);wrong.Add(good with{Plan=plan with{Manifest=plan.Manifest with{Bytes=JsonSerializer.SerializeToUtf8Bytes(json)}}});}
            if(c.Archives.Length>0)
            {
                wrong.Add(good with{Plan=plan with{ArchiveMembers=[]}});wrong.Add(good with{Plan=plan with{Caveats=[]}});
                var a=c.Archives[0];foreach(var changed in new[]{a with{SourcePath="wrong"},a with{LocalPath="wrong"},a with{Url="wrong"},a with{Member="wrong"},a with{ArchiveSha256="wrong"},a with{MemberSha256="wrong"}})wrong.Add(good with{Plan=plan with{ArchiveMembers=[changed]}});
                foreach(var key in new[]{"source_path","local_path","url","member","archive_sha256","member_sha256","archive_verified"}){var json=JsonNode.Parse(plan.Manifest.Bytes.Span)!.AsObject();json["archive_members"]![0]![key]=key=="archive_verified"?JsonValue.Create(true):JsonValue.Create("wrong");wrong.Add(good with{Plan=plan with{Manifest=plan.Manifest with{Bytes=JsonSerializer.SerializeToUtf8Bytes(json)}}});}
            }
            foreach(var key in new[]{"source_path","local_path","bundle_path","sha256","length"}){var json=JsonNode.Parse(plan.Manifest.Bytes.Span)!.AsObject();json["required_snapshots"]![0]!.AsObject().Remove(key);wrong.Add(good with{Plan=plan with{Manifest=plan.Manifest with{Bytes=JsonSerializer.SerializeToUtf8Bytes(json)}}});}
        }
        else wrong.Add(good with{Issues=[]});
        foreach(var response in wrong)Assert.NotNull(Record.Exception(()=>RehydrationBoundaryCases.AssertCase(c,new ScriptedRehydrationPlanner(response))));
    }
}
