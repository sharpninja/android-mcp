using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Publication;

public sealed record SnapshotProjectionCase(string Id, string StatusMode = "downloaded",
    bool MissingBundle = false, bool Archive = false, bool GZip = false, int? Limit = null,
    AcquisitionCompatibilityProfile Profile = AcquisitionCompatibilityProfile.CanonicalRestore,
    bool MissingGit = false, string? Error = null)
{
    public override string ToString() => Id;
}
public interface ISnapshotProjectionHarness
{
    IRehydrationSnapshotPlanner Planner { get; }
    IRehydrationSeedPlanner SeedPlanner { get; }
    int RecipeParseCalls { get; }
}
public static class RehydrationSnapshotCases
{
    private static readonly string Sha = new('a', 64), Git = new('b', 40);
    public static IEnumerable<object[]> Cases => new SnapshotProjectionCase[] {
        new("SNAP-EXACT-ALIAS-ROWS"), new("SNAP-STATUS-CASE", StatusMode:"Downloaded"),
        new("SNAP-STATUS-MISSING", StatusMode:"missing"), new("SNAP-STATUS-NULL", StatusMode:"null"),
        new("SNAP-STATUS-EMPTY", StatusMode:""), new("SNAP-STATUS-NUMBER", StatusMode:"number"),
        new("SNAP-STATUS-TRUE", StatusMode:"true"), new("SNAP-STATUS-FALSE", StatusMode:"false"),
        new("SNAP-STATUS-ARRAY", StatusMode:"array"), new("SNAP-STATUS-OBJECT", StatusMode:"object"),
        new("SNAP-MISSING-BUNDLE", MissingBundle:true),
        new("SNAP-ARCHIVE-PREREQUISITES", Archive:true), new("SNAP-GZIP", GZip:true),
        new("SNAP-GENERIC", Profile:AcquisitionCompatibilityProfile.GenericRehydrate),
        new("SNAP-LIMIT-ZERO", Limit:0), new("SNAP-MISSING-GIT", MissingGit:true),
        new("SNAP-BAD-PIN", Error:"INVALID_PIN")
    }.Select(c => new object[] { c });

    public static RehydrationPlanRequest Request(SnapshotProjectionCase c) => RehydrationContractCases.Request(root => {
        JsonObject Row(int i) => new() {
            ["path"]=$"api/row{i}.java", ["local_path"]=i==2?"blobs/other":"blobs/shared",
            ["sha256"]=Sha, ["size_bytes"]=i==0?null:3, ["git_blob"]=Git,
            ["url"]=$"https://example.invalid/row{i}", ["revision"]=$"revision-{i}",
            ["status"]=i==0?"failed":"downloaded", ["kind"]="java-source",
            ["transport"]=i==2?null:new JsonObject {
                ["immutable_url"]=false, ["required_local_snapshot"]=$"snapshots/row{i}",
                ["kind"]=i==1?"gitiles-text":"raw"
            }
        };
        var first=Row(0); var middle=Row(1); var last=Row(2);
        if(c.StatusMode=="missing") middle.Remove("status");
        else middle["status"]=c.StatusMode switch {
            "null"=>null,"number"=>JsonValue.Create(7),"true"=>JsonValue.Create(true),"false"=>JsonValue.Create(false),
            "array"=>new JsonArray("downloaded"),"object"=>new JsonObject {["status"]="downloaded"},
            _=>JsonValue.Create(c.StatusMode)
        };
        if(c.MissingBundle) middle["transport"]!["required_local_snapshot"]=null;
        if(c.MissingGit) middle.Remove("git_blob");
        if(c.Archive) {
            var t=middle["transport"]!.AsObject(); t["archive_url"]="https://example.invalid/archive.zip";
            t["archive_member"]="member/row1.java";
        }
        if(c.Error is not null) middle["sha256"]="bad";
        root["inventory"]!["sources"]=new JsonArray(first,middle,last);
    }, gzip:c.GZip, limit:c.Limit) with { Profile=c.Profile };

    public static RehydrationPlanResult Baseline(SnapshotProjectionCase c) =>
        RehydrationContractCases.Correct(new(c.Id,Request(c),c.Error,Rows:3,Unique:2,KnownBytes:6,UnknownSizes:0));
    public static RehydrationSeedPlanResult Seed(SnapshotProjectionCase c) =>
        new(Baseline(c),c.Error is not null?null:new("fixture-release",PublicationTestData.Hash(Request(c).Recipe.Bytes),[]));
    private static JsonValueKind StatusKind(string mode) => mode switch {
        "missing"=>JsonValueKind.Undefined,"null"=>JsonValueKind.Null,"number"=>JsonValueKind.Number,
        "true"=>JsonValueKind.True,"false"=>JsonValueKind.False,"array"=>JsonValueKind.Array,
        "object"=>JsonValueKind.Object,_=>JsonValueKind.String
    };
    public static RehydrationSnapshotPlanResult Correct(SnapshotProjectionCase c)
    {
        var baseline=Baseline(c); if(c.Error is not null)return new(baseline,null);
        var rows=new List<RehydrationSnapshotRow>();
        for(var i=0;i<3;i++) {
            var local=i==2?"blobs/other":"blobs/shared";
            var bundle=i==2||i==1&&c.MissingBundle?null:$"snapshots/row{i}";
            SourceArchivePin? archive=i==1&&c.Archive?new(new("https://example.invalid/archive.zip"),"member/row1.java",Compatibility:c.Profile):null;
            var pin=new SourcePin(local,new($"https://example.invalid/row{i}"),$"revision-{i}",Sha,i==0?null:3,
                i==1&&c.MissingGit?null:Git,i==1?SourceWireEncoding.GitilesText:SourceWireEncoding.Raw,bundle,archive,c.Profile);
            var statusKind=i==1?StatusKind(c.StatusMode):JsonValueKind.String;
            var statusPresent=statusKind!=JsonValueKind.Undefined;
            var status=i==0?"failed":i==2?"downloaded":statusKind==JsonValueKind.String?c.StatusMode:null;
            var snapshots=i==2?Array.Empty<SnapshotRequirement>():new[]{new SnapshotRequirement($"api/row{i}.java",local,bundle,Sha,i==0?null:3)};
            var prerequisites=new List<RehydrationPrerequisite>();
            void Missing(RehydrationPrerequisiteCode code) => prerequisites.Add(new(code,Array.AsReadOnly(new[]{i}),local,"Missing pinned prerequisite; exact existing target may be reused."));
            if(i==1&&c.MissingBundle)Missing(RehydrationPrerequisiteCode.RequiredImmutableSnapshot);
            if(archive is not null&&c.Profile==AcquisitionCompatibilityProfile.CanonicalRestore) {
                Missing(RehydrationPrerequisiteCode.CanonicalArchiveETag);
                Missing(RehydrationPrerequisiteCode.CanonicalArchivePlatformVersion);
                Missing(RehydrationPrerequisiteCode.CanonicalArchiveRepositoryXml);
                Missing(RehydrationPrerequisiteCode.CanonicalArchiveSize);
            }
            rows.Add(new(i,status,pin,Array.AsReadOnly(snapshots),Array.AsReadOnly(prerequisites.ToArray()),statusPresent,statusKind));
        }
        return new(baseline,new("fixture-release",PublicationTestData.Hash(Request(c).Recipe.Bytes),c.Profile,
            Array.AsReadOnly(rows.ToArray()),true));
    }
    public static void AssertCase(SnapshotProjectionCase c,ISnapshotProjectionHarness harness)
    {
        var request=Request(c);var input=request.Recipe.Bytes.ToArray();var inputLock=request.Lock.Bytes.ToArray();
        var baseline=harness.Planner.Plan(request);var seed=harness.SeedPlanner.PlanSeed(request);
        var count=harness.RecipeParseCalls;var result=harness.Planner.PlanSnapshot(request);
        Assert.Equal(1,harness.RecipeParseCalls-count);
        Assert.Equal(input,request.Recipe.Bytes.ToArray());Assert.Equal(inputLock,request.Lock.Bytes.ToArray());
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(baseline),JsonSerializer.SerializeToUtf8Bytes(result.Baseline));
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(seed),JsonSerializer.SerializeToUtf8Bytes(harness.SeedPlanner.PlanSeed(request)));
        Assert.Equal(c.Error is null,result.IsValid);
        Assert.Equal(c.Error is null?Array.Empty<string>():new[]{c.Error},result.Baseline.Issues.Select(x=>x.Code));
        if(c.Error is not null){Assert.Null(result.SnapshotProjection);return;}
        var expected=Correct(c).SnapshotProjection!;var actual=Assert.IsType<RehydrationSnapshotProjection>(result.SnapshotProjection);
        Assert.Equal(expected.Release,actual.Release);Assert.Equal(expected.RecipeSha256,actual.RecipeSha256);
        Assert.Equal(expected.Profile,actual.Profile);Assert.Equal(expected.Resume,actual.Resume);Assert.Equal(3,actual.Rows.Count);
        for(var i=0;i<3;i++) {
            var a=actual.Rows[i];var e=expected.Rows[i];
            Assert.Equal(e.RowIndex,a.RowIndex);Assert.Equal(e.StatusPresent,a.StatusPresent);
            Assert.Equal(e.StatusKind,a.StatusKind);Assert.Equal(e.Status,a.Status);
            Assert.Equal(e.DownloadedEligible,a.DownloadedEligible);Assert.Equal(e.Pin,a.Pin);
            Assert.Equal(e.RequiredSnapshots,a.RequiredSnapshots);Assert.Equal(e.Prerequisites.Count,a.Prerequisites.Count);
            for(var j=0;j<e.Prerequisites.Count;j++) {
                var x=e.Prerequisites[j];var y=a.Prerequisites[j];
                Assert.Equal(x.Code,y.Code);Assert.Equal(x.Path,y.Path);Assert.Equal(x.Reason,y.Reason);
                Assert.Equal(x.SelectionRowIndices,y.SelectionRowIndices);
                if(y.SelectionRowIndices is IList<int> indices)Assert.Throws<NotSupportedException>(()=>indices[0]=99);
            }
            if(a.RequiredSnapshots is IList<SnapshotRequirement> snapshots&&snapshots.Count>0)
                Assert.Throws<NotSupportedException>(()=>snapshots[0]=snapshots[0] with{Sha256="wrong"});
            if(a.Prerequisites is IList<RehydrationPrerequisite> prerequisites&&prerequisites.Count>0)
                Assert.Throws<NotSupportedException>(()=>prerequisites[0]=prerequisites[0] with{Path="wrong"});
        }
        if(actual.Rows is IList<RehydrationSnapshotRow> rows)Assert.Throws<NotSupportedException>(()=>rows[0]=rows[1]);
    }
    public static IEnumerable<RehydrationSnapshotPlanResult> Wrong(SnapshotProjectionCase c)
    {
        var good=Correct(c);
        yield return good with{Baseline=good.Baseline with{Issues=[new("WRONG","")]}};
        if(c.Error is not null){yield return new(new(null,[]),null);yield break;}
        var p=good.SnapshotProjection!;
        yield return good with{SnapshotProjection=null};
        yield return good with{SnapshotProjection=p with{Release="wrong"}};
        yield return good with{SnapshotProjection=p with{RecipeSha256="wrong"}};
        yield return good with{SnapshotProjection=p with{Profile=(AcquisitionCompatibilityProfile)99}};
        yield return good with{SnapshotProjection=p with{Resume=false}};
        yield return good with{SnapshotProjection=p with{Rows=Array.AsReadOnly(p.Rows.Take(2).ToArray())}};
        yield return good with{SnapshotProjection=p with{Rows=Array.AsReadOnly(p.Rows.Reverse().ToArray())}};
        yield return good with{SnapshotProjection=p with{Rows=p.Rows.ToArray()}};
        for(var i=0;i<p.Rows.Count;i++) {
            var row=p.Rows[i];var alternatives=new List<RehydrationSnapshotRow> {
                row with{RowIndex=99},row with{Status=row.Status=="downloaded"?"failed":"downloaded"},
                row with{StatusPresent=!row.StatusPresent},
                row with{StatusKind=row.StatusKind==JsonValueKind.String?JsonValueKind.Null:JsonValueKind.String},
                row with{Pin=row.Pin with{LocalPath="blobs/wrong"}},row with{Pin=row.Pin with{Url=new("https://example.invalid/wrong")}},
                row with{Pin=row.Pin with{Revision="wrong"}},row with{Pin=row.Pin with{Sha256="wrong"}},
                row with{Pin=row.Pin with{SizeBytes=row.Pin.SizeBytes is null?3:null}},
                row with{Pin=row.Pin with{GitBlob=row.Pin.GitBlob is null?Git:null}},
                row with{Pin=row.Pin with{Encoding=(SourceWireEncoding)99}},
                row with{Pin=row.Pin with{RequiredLocalSnapshot="snapshots/wrong"}},
                row with{Pin=row.Pin with{Compatibility=(AcquisitionCompatibilityProfile)99}}
            };
            if(!row.StatusPresent)alternatives.Add(row with{StatusPresent=true,StatusKind=JsonValueKind.Null,Status=null});
            if(row.StatusKind==JsonValueKind.Null)alternatives.Add(row with{StatusPresent=false,StatusKind=JsonValueKind.Undefined,Status=null});
            if(row.RequiredSnapshots.Count>0) {
                alternatives.Add(row with{RequiredSnapshots=[]});
                var snapshot=row.RequiredSnapshots[0];
                foreach(var wrong in new[]{snapshot with{SourcePath="api/wrong"},snapshot with{LocalPath="blobs/wrong"},
                    snapshot with{BundlePath="snapshots/wrong"},snapshot with{Sha256="wrong"},
                    snapshot with{Length=snapshot.Length is null?3:null}})
                    alternatives.Add(row with{RequiredSnapshots=Array.AsReadOnly(new[]{wrong})});
            } else alternatives.Add(row with{RequiredSnapshots=Array.AsReadOnly(new[]{new SnapshotRequirement("wrong","wrong",null,Sha,null)})});
            if(row.Pin.Archive is {} archive)alternatives.Add(row with{Pin=row.Pin with{Archive=archive with{Member="member/wrong"}}});
            else alternatives.Add(row with{Pin=row.Pin with{Archive=new(new("https://example.invalid/wrong"),"wrong")}});
            if(row.Prerequisites.Count>0) {
                alternatives.Add(row with{Prerequisites=[]});
                for(var j=0;j<row.Prerequisites.Count;j++) {
                    var index=j;var required=row.Prerequisites[j];
                    foreach(var wrong in new[]{required with{Code=(RehydrationPrerequisiteCode)99},required with{Path="wrong"},
                        required with{SelectionRowIndices=Array.AsReadOnly(new[]{99})},required with{Reason="wrong"}})
                        alternatives.Add(row with{Prerequisites=Array.AsReadOnly(row.Prerequisites.Select((x,k)=>k==index?wrong:x).ToArray())});
                }
            } else alternatives.Add(row with{Prerequisites=Array.AsReadOnly(new[]{new RehydrationPrerequisite(RehydrationPrerequisiteCode.SourceRevision,Array.AsReadOnly(new[]{i}),"wrong","wrong")})});
            var indexRow=i;
            foreach(var alternative in alternatives)yield return good with{SnapshotProjection=p with{Rows=Array.AsReadOnly(p.Rows.Select((x,k)=>k==indexRow?alternative:x).ToArray())}};
        }
    }
}
public sealed class ScriptedSnapshotHarness(SnapshotProjectionCase c,RehydrationSnapshotPlanResult? wrong=null,bool doubleParse=false)
    : ISnapshotProjectionHarness,IRehydrationSnapshotPlanner,IRehydrationSeedPlanner
{
    public IRehydrationSnapshotPlanner Planner=>this;
    public IRehydrationSeedPlanner SeedPlanner=>this;
    public int RecipeParseCalls {get;private set;}
    public RehydrationPlanResult Plan(RehydrationPlanRequest request){RecipeParseCalls++;return RehydrationSnapshotCases.Baseline(c);}
    public RehydrationSeedPlanResult PlanSeed(RehydrationPlanRequest request){RecipeParseCalls++;return RehydrationSnapshotCases.Seed(c);}
    public RehydrationSnapshotPlanResult PlanSnapshot(RehydrationPlanRequest request)
    {RecipeParseCalls+=doubleParse?2:1;return wrong??RehydrationSnapshotCases.Correct(c);}
}
[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-007")]
public sealed class RehydrationSnapshotMockTests
{
    [Theory,MemberData(nameof(RehydrationSnapshotCases.Cases),MemberType=typeof(RehydrationSnapshotCases))]
    public void CorrectTypedRowsPreserveBaselines(SnapshotProjectionCase c)=>
        RehydrationSnapshotCases.AssertCase(c,new ScriptedSnapshotHarness(c));
    [Theory,MemberData(nameof(RehydrationSnapshotCases.Cases),MemberType=typeof(RehydrationSnapshotCases))]
    public void IndividualWrongRowsPinsEligibilityAndParseCountsAreRejected(SnapshotProjectionCase c)
    {
        foreach(var wrong in RehydrationSnapshotCases.Wrong(c))
            Assert.NotNull(Record.Exception(()=>RehydrationSnapshotCases.AssertCase(c,new ScriptedSnapshotHarness(c,wrong))));
        Assert.NotNull(Record.Exception(()=>RehydrationSnapshotCases.AssertCase(c,new ScriptedSnapshotHarness(c,doubleParse:true))));
    }
}
