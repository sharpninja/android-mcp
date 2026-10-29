using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Publication;

public sealed record SeedProjectionCase(string Id, bool GZip = false, int? Limit = null,
    AcquisitionCompatibilityProfile Profile = AcquisitionCompatibilityProfile.CanonicalRestore,
    bool FirstSnapshot = true, bool LastSnapshot = true, bool MissingLastBundle = false,
    bool FirstUnknownSize = false, bool LastGit = false, string? Error = null)
{ public override string ToString() => Id; }
public interface ISeedProjectionHarness
{
    IRehydrationSeedPlanner Planner { get; }
    int RecipeParseCalls { get; }
}
public static class RehydrationSeedCases
{
    private static readonly string A = new('a', 64), B = new('b', 64);
    public static IEnumerable<object[]> Cases => new SeedProjectionCase[] {
        new("SEED-LAST-ALIAS"), new("SEED-LAST-REMOVES-SNAPSHOT", LastSnapshot:false),
        new("SEED-MISSING-LAST-BUNDLE", MissingLastBundle:true), new("SEED-LAST-ADDS-SNAPSHOT", FirstSnapshot:false),
        new("SEED-GZIP", GZip:true), new("SEED-GENERIC", Profile:AcquisitionCompatibilityProfile.GenericRehydrate),
        new("SEED-LIMIT-ZERO", Limit:0), new("SEED-LIMIT-ONE", Limit:1),
        new("SEED-LAST-SIZE", FirstUnknownSize:true), new("SEED-LAST-GIT", LastGit:true),
        new("SEED-BAD-PIN", Error:"INVALID_PIN"), new("SEED-CONFLICT", Error:"CONFLICTING_SOURCE_ALIAS")
    }.Select(c => new object[] { c });

    public static RehydrationPlanRequest Request(SeedProjectionCase c) => RehydrationContractCases.Request(root =>
    {
        JsonObject Row(string logical, string local, string hash, string revision, string url, bool snapshot, string? bundle) => new() {
            ["path"]=logical, ["local_path"]=local, ["sha256"]=hash, ["size_bytes"]=3,
            ["revision"]=revision, ["url"]=url, ["kind"]="java-source",
            ["selection_reasons"]=new JsonArray("row-correlated"),
            ["transport"]=snapshot ? new JsonObject { ["immutable_url"]=false, ["required_local_snapshot"]=bundle } : null
        };
        var first = Row("api/first.java", "blobs/a", A, "first", "https://example.invalid/first", c.FirstSnapshot, "snapshots/first");
        var other = Row("api/b.java", "blobs/b", B, "other", "https://example.invalid/b", false, null);
        var last = Row("api/last.java", "blobs/a", A, "last", "https://example.invalid/last", c.LastSnapshot, c.MissingLastBundle ? null : "snapshots/last");
        if(c.FirstUnknownSize) first["size_bytes"]=null;
        if(c.LastGit) { last["git_blob"]=new string('a',40); last["transport"]!.AsObject()["kind"]="gitiles-text"; }
        if(c.Error=="INVALID_PIN") last["sha256"]="bad";
        if(c.Error=="CONFLICTING_SOURCE_ALIAS") last["sha256"]=B;
        root["inventory"]!["sources"]=new JsonArray(first,other,last);
    }, gzip:c.GZip, limit:c.Limit) with { Profile=c.Profile };

    public static RehydrationPlanResult Base(SeedProjectionCase c)
    {
        var request=Request(c);
        return RehydrationContractCases.Correct(new(c.Id,request,c.Error,Rows:3,Unique:2,KnownBytes:6,UnknownSizes:0));
    }
    public static RehydrationSeedPlanResult Correct(SeedProjectionCase c)
    {
        var baseline=Base(c); if(c.Error is not null) return new(baseline,null);
        var request=Request(c); var digest=PublicationTestData.Hash(request.Recipe.Bytes);
        var bundle=c.LastSnapshot && !c.MissingLastBundle ? "snapshots/last" : null;
        var pin=new SourcePin("blobs/a",new("https://example.invalid/last"),"last",A,3,c.LastGit?new string('a',40):null,
            c.LastGit?SourceWireEncoding.GitilesText:SourceWireEncoding.Raw,bundle,null,c.Profile);
        var snapshot=c.LastSnapshot ? new SnapshotRequirement("api/last.java","blobs/a",bundle,A,3) : null;
        var candidates=new List<RehydrationSeedCandidate> { new(0,RehydrationSeedCandidateKind.LocalPath,"blobs/a") };
        if(bundle is not null)candidates.Add(new(1,RehydrationSeedCandidateKind.RequiredLocalSnapshot,bundle));
        var other=new SourcePin("blobs/b",new("https://example.invalid/b"),"other",B,3,Compatibility:c.Profile);
        return new(baseline,new("fixture-release",digest,Array.AsReadOnly(new RehydrationSeedSource[] {
            new(0,2,pin,snapshot,Array.AsReadOnly(candidates.ToArray())),
            new(1,1,other,null,Array.AsReadOnly(new RehydrationSeedCandidate[] {new(0,RehydrationSeedCandidateKind.LocalPath,"blobs/b")}))
        })));
    }
    public static void AssertCase(SeedProjectionCase c, ISeedProjectionHarness harness)
    {
        var request=Request(c); var before=request.Recipe.Bytes.ToArray();
        var baseline=harness.Planner.Plan(request); var parseBefore=harness.RecipeParseCalls;
        var actual=harness.Planner.PlanSeed(request);
        Assert.Equal(1,harness.RecipeParseCalls-parseBefore);
        Assert.Equal(before,request.Recipe.Bytes.ToArray());
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(baseline),JsonSerializer.SerializeToUtf8Bytes(actual.BaseResult));
        Assert.Equal(c.Error is null,actual.IsValid);
        Assert.Equal(c.Error is null?Array.Empty<string>():new[]{c.Error},actual.BaseResult.Issues.Select(x=>x.Code));
        if(c.Error is not null) { Assert.Null(actual.SeedProjection);return; }
        Assert.Equal(3,actual.BaseResult.Plan!.SourceRows); Assert.Equal(2,actual.BaseResult.Plan.UniqueSourcePaths);
        Assert.False(actual.BaseResult.Plan.Network);Assert.False(actual.BaseResult.Plan.Complete);
        var expected=Correct(c).SeedProjection!; var projection=Assert.IsType<RehydrationSeedProjection>(actual.SeedProjection);
        Assert.Equal(expected.Release,projection.Release);Assert.Equal(expected.RecipeSha256,projection.RecipeSha256);
        Assert.Equal(expected.Sources.Count,projection.Sources.Count);
        for(var index=0;index<expected.Sources.Count;index++) {
            var e=expected.Sources[index];var a=projection.Sources[index];
            Assert.Equal(e.PhysicalSourceIndex,a.PhysicalSourceIndex);Assert.Equal(e.WinningSelectionRowIndex,a.WinningSelectionRowIndex);
            Assert.Equal(e.Pin,a.Pin);Assert.Equal(e.RequiredSnapshot,a.RequiredSnapshot);Assert.Equal(e.BundleCandidates,a.BundleCandidates);
        }
        if(projection.Sources is IList<RehydrationSeedSource> sources)Assert.Throws<NotSupportedException>(()=>sources[0]=sources[1]);
        if(projection.Sources[0].BundleCandidates is IList<RehydrationSeedCandidate> candidates)
            Assert.Throws<NotSupportedException>(()=>candidates[0]=new(99,RehydrationSeedCandidateKind.LocalPath,"wrong"));
    }
    public static IEnumerable<(string Id,RehydrationSeedPlanResult Result)> Wrong(SeedProjectionCase c)
    {
        var good=Correct(c);
        yield return ("base",good with { BaseResult=good.BaseResult with { Issues=[new("WRONG","")] } });
        if(c.Error is not null) { yield return ("error",new(new(null,[]),null)); yield break; }
        var p=good.SeedProjection!;
        yield return ("missing",good with { SeedProjection=null });
        yield return ("release",good with { SeedProjection=p with { Release="wrong" } });
        yield return ("recipe",good with { SeedProjection=p with { RecipeSha256="wrong" } });
        yield return ("source-omitted",good with { SeedProjection=p with { Sources=p.Sources.Skip(1).ToArray() } });
        for(var index=0;index<p.Sources.Count;index++) {
            var source=p.Sources[index];
            var alternatives=new List<RehydrationSeedSource> {
                source with { PhysicalSourceIndex=99 }, source with { WinningSelectionRowIndex=99 },
                source with { Pin=source.Pin with { Revision="first" } }, source with { Pin=source.Pin with { Url=new("https://example.invalid/wrong") } },
                source with { Pin=source.Pin with { Sha256="wrong" } }, source with { Pin=source.Pin with { SizeBytes=null } },
                source with { Pin=source.Pin with { GitBlob="wrong" } }, source with { Pin=source.Pin with { Encoding=(SourceWireEncoding)99 } },
                source with { Pin=source.Pin with { Compatibility=(AcquisitionCompatibilityProfile)99 } },
                source with { Pin=source.Pin with { RequiredLocalSnapshot="snapshots/first" } }, source with { BundleCandidates=[] },
                source with { BundleCandidates=Array.AsReadOnly(new[]{new RehydrationSeedCandidate(99,RehydrationSeedCandidateKind.LocalPath,source.Pin.LocalPath)}) }
            };
            if(source.RequiredSnapshot is {} snapshot) {
                alternatives.Add(source with { RequiredSnapshot=null });
                alternatives.Add(source with { RequiredSnapshot=snapshot with { SourcePath="api/first.java" } });
                alternatives.Add(source with { RequiredSnapshot=snapshot with { BundlePath="snapshots/first" } });
            } else alternatives.Add(source with { RequiredSnapshot=new("wrong","wrong",null,A,null) });
            if(source.BundleCandidates.Count>1)alternatives.Add(source with { BundleCandidates=Array.AsReadOnly(source.BundleCandidates.Reverse().ToArray()) });
            foreach(var alternative in alternatives)yield return ($"source-{index}",good with { SeedProjection=p with { Sources=Array.AsReadOnly(p.Sources.Select((x,i)=>i==index?alternative:x).ToArray()) } });
        }
    }
}
public sealed class ScriptedSeedHarness(SeedProjectionCase c, RehydrationSeedPlanResult? wrong=null, bool doubleParse=false) : ISeedProjectionHarness, IRehydrationSeedPlanner
{
    public IRehydrationSeedPlanner Planner=>this;
    public int RecipeParseCalls { get;private set; }
    public RehydrationPlanResult Plan(RehydrationPlanRequest request) { RecipeParseCalls++; return RehydrationSeedCases.Base(c); }
    public RehydrationSeedPlanResult PlanSeed(RehydrationPlanRequest request)
    { RecipeParseCalls+=doubleParse?2:1;return wrong??RehydrationSeedCases.Correct(c); }
}
[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-007")]
public sealed class RehydrationSeedMockTests
{
    [Theory,MemberData(nameof(RehydrationSeedCases.Cases),MemberType=typeof(RehydrationSeedCases))]
    public void CorrectScriptedProjectionPasses(SeedProjectionCase c)=>RehydrationSeedCases.AssertCase(c,new ScriptedSeedHarness(c));
    [Theory,MemberData(nameof(RehydrationSeedCases.Cases),MemberType=typeof(RehydrationSeedCases))]
    public void IndividualNegativeControlsRejectWrongRowsPinsAndParseCount(SeedProjectionCase c)
    {
        foreach(var wrong in RehydrationSeedCases.Wrong(c))
            Assert.NotNull(Record.Exception(()=>RehydrationSeedCases.AssertCase(c,new ScriptedSeedHarness(c,wrong.Result))));
        Assert.NotNull(Record.Exception(()=>RehydrationSeedCases.AssertCase(c,new ScriptedSeedHarness(c,doubleParse:true))));
    }
}
