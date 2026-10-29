using System.Text;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed record SourceSnapshotCase(string Id,string Mode="Restore",bool Download=false,string? Change=null,
    string? Error=null,int Exit=0,int Existing=0,int Missing=2,int Downloaded=0,int Batches=0,int Network=0,int Writes=0,int PlannerCalls=1);
public sealed record SourceSnapshotObservation(string? Error,int? Exit,int? EligibleRows,int? Groups,int? Existing,int? Missing,int? Downloaded,
    int Batches,int Network,int Writes,int PlannerCalls,bool OriginalsRetained,IReadOnlyList<string> MissingPaths,IReadOnlyList<string> RequestUrls,
    IReadOnlyDictionary<string,byte[]> Output,bool CompleteSeal);
public interface ISourceSnapshotProbe { Task<SourceSnapshotObservation> RunAsync(SourceSnapshotCase test); }
public static class SourceSnapshotCases
{
    public static readonly byte[] First=Encoding.UTF8.GetBytes("fixture\n");
    public static readonly byte[] Second=Encoding.UTF8.GetBytes("second fixture\n");
    public const string FirstUrl="https://android.googlesource.com/platform/fixture/+/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/first?format=TEXT";
    public const string SecondUrl="https://android.googlesource.com/platform/fixture/+/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/second?format=TEXT";
    public static IEnumerable<object[]> All=>new[]{
        new SourceSnapshotCase("restore-original-default-dry-run"),
        new SourceSnapshotCase("fetch-original-default-dry-run",Mode:"Fetch",Exit:1),
        new SourceSnapshotCase("restore-existing-verified",Change:"existing-first",Existing:1,Missing:1),
        new SourceSnapshotCase("restore-all-existing",Change:"existing-all",Existing:2,Missing:0),
        new SourceSnapshotCase("fetch-existing-not-verified",Mode:"Fetch",Change:"corrupt-first",Exit:1,Existing:1,Missing:1),
        new SourceSnapshotCase("restore-existing-corrupt-retained",Change:"corrupt-first",Error:"SOURCE_HASH"),
        new SourceSnapshotCase("restore-download-first-git-winner",Download:true,Downloaded:2,Batches:1,Network:2,Writes:3),
        new SourceSnapshotCase("fetch-download-missing-only",Mode:"Fetch",Download:true,Change:"corrupt-first",Existing:1,Missing:1,Downloaded:1,Batches:1,Network:1,Writes:2),
        new SourceSnapshotCase("restore-download-empty-missing",Download:true,Change:"existing-all",Existing:2,Missing:0),
        new SourceSnapshotCase("restore-no-downloaded-rows",Change:"none",Missing:0),
        new SourceSnapshotCase("restore-missing-git",Change:"missing-git",Error:"SNAPSHOT_GIT_BLOB"),
        new SourceSnapshotCase("restore-wrong-recipe-lock",Change:"lock",Error:"SNAPSHOT_PLAN_INVALID"),
        new SourceSnapshotCase("restore-cancelled",Change:"cancel",Error:"CANCELLED",PlannerCalls:0),
        new SourceSnapshotCase("restore-invalid-mode",Mode:"Invalid",Error:"SNAPSHOT_MODE",PlannerCalls:0),
        new SourceSnapshotCase("restore-limit-not-silent",Change:"limit",Error:"SNAPSHOT_LIMIT",PlannerCalls:0),
        new SourceSnapshotCase("restore-download-failure-no-seal",Download:true,Change:"transfer",Error:"SOURCE_HASH",Batches:1,Network:2,Writes:1),
        new SourceSnapshotCase("restore-invalid-typed-projection",Change:"projection",Error:"SNAPSHOT_PROJECTION"),
        new SourceSnapshotCase("fetch-rejects-nongitiles-transport",Mode:"Fetch",Download:true,Change:"fetch-transport",Error:"SNAPSHOT_FETCH_TRANSPORT"),
        new SourceSnapshotCase("fetch-rejects-immutable-bundle-fallback",Mode:"Fetch",Download:true,Change:"fetch-bundle",Error:"SNAPSHOT_FETCH_TRANSPORT"),
        new SourceSnapshotCase("fetch-rejects-generic-retry-profile",Mode:"Fetch",Download:true,Change:"fetch-profile",Error:"SNAPSHOT_FETCH_TRANSPORT"),
        new SourceSnapshotCase("restore-missing-status-is-not-silently-skipped",Change:"missing-status",Error:"SNAPSHOT_STATUS"),
        new SourceSnapshotCase("restore-ordinary-absent-kind",Download:true,Change:"implicit",Downloaded:2,Batches:1,Network:2,Writes:3),
        new SourceSnapshotCase("fetch-ordinary-absent-kind",Mode:"Fetch",Download:true,Change:"implicit",Downloaded:2,Batches:1,Network:2,Writes:3),
        new SourceSnapshotCase("restore-unstripped-wire-whitespace",Download:true,Change:"implicit-whitespace",Error:"BASE64",Batches:1,Network:1),
        new SourceSnapshotCase("fetch-permissive-wire-whitespace",Mode:"Fetch",Download:true,Change:"implicit-whitespace",Downloaded:2,Batches:1,Network:2,Writes:3),
        new SourceSnapshotCase("restore-wire-junk",Download:true,Change:"implicit-junk",Error:"BASE64",Batches:1,Network:1),
        new SourceSnapshotCase("fetch-permissive-wire-junk",Mode:"Fetch",Download:true,Change:"implicit-junk",Downloaded:2,Batches:1,Network:2,Writes:3),
        new SourceSnapshotCase("fetch-malformed-wire-no-source-write",Mode:"Fetch",Download:true,Change:"implicit-padding",Error:"BASE64",Batches:1,Network:1)
    }.Select(x=>new object[]{x});
    public static SourceSnapshotObservation Expected(SourceSnapshotCase test)
    {
        var success=test.Error is null;var none=test.Change=="none";
        var missing=success?(test.Change=="existing-all"||none?[]:test.Existing==1?new[]{"raw/second"}:new[]{"raw/first","raw/second"}):[];
        var requests=test.Network==0?Array.Empty<string>():test.Network==1?[test.Existing==1?SecondUrl:FirstUrl]:[FirstUrl,SecondUrl];
        var output=new Dictionary<string,byte[]>();
        if(test.Change is "existing-first" or "existing-all")output["raw/first"]=First;
        if(test.Change=="corrupt-first")output["raw/first"]=[0];
        if(test.Change=="existing-all")output["raw/second"]=Second;
        if(test.Network==2)output["raw/first"]=First;
        if(success&&test.Downloaded>0)output["raw/second"]=Second;
        return new(test.Error,success?test.Exit:null,success?(none?0:3):null,success?(none?0:2):null,
            success?test.Existing:null,success?test.Missing:null,success?test.Downloaded:null,test.Batches,test.Network,test.Writes,test.PlannerCalls,
            true,missing,requests,output,success&&test.Batches>0);
    }
    public static async Task AssertCase(SourceSnapshotCase test,ISourceSnapshotProbe probe)
    {
        var expected=Expected(test);var actual=await probe.RunAsync(test);
        Assert.Equal(expected.Error,actual.Error);Assert.Equal(expected.Exit,actual.Exit);Assert.Equal(expected.EligibleRows,actual.EligibleRows);
        Assert.Equal(expected.Groups,actual.Groups);Assert.Equal(expected.Existing,actual.Existing);Assert.Equal(expected.Missing,actual.Missing);
        Assert.Equal(expected.Downloaded,actual.Downloaded);Assert.Equal(expected.Batches,actual.Batches);Assert.Equal(expected.Network,actual.Network);
        Assert.Equal(expected.Writes,actual.Writes);Assert.Equal(expected.PlannerCalls,actual.PlannerCalls);Assert.True(actual.OriginalsRetained);
        Assert.Equal(expected.MissingPaths,actual.MissingPaths);Assert.Equal(expected.RequestUrls,actual.RequestUrls);
        Assert.Equal(expected.Output.Keys.Order(),actual.Output.Keys.Order());foreach(var item in expected.Output)Assert.Equal(item.Value,actual.Output[item.Key]);
        Assert.Equal(expected.CompleteSeal,actual.CompleteSeal);
    }
}
public sealed class ScriptedSourceSnapshotProbe(string? mutation=null):ISourceSnapshotProbe
{
    public Task<SourceSnapshotObservation> RunAsync(SourceSnapshotCase test)
    {
        var value=SourceSnapshotCases.Expected(test);
        return Task.FromResult(mutation switch{
            "error"=>value with{Error="wrong"},"exit"=>value with{Exit=9},"eligible"=>value with{EligibleRows=99},
            "groups"=>value with{Groups=99},"existing"=>value with{Existing=99},"missing"=>value with{Missing=99},
            "downloaded"=>value with{Downloaded=99},"batches"=>value with{Batches=99},"network"=>value with{Network=99},
            "writes"=>value with{Writes=99},"planner"=>value with{PlannerCalls=99},"retained"=>value with{OriginalsRetained=false},
            "paths"=>value with{MissingPaths=["wrong"]},"urls"=>value with{RequestUrls=["wrong"]},
            "output"=>value with{Output=new Dictionary<string,byte[]>{{"wrong",[0]}}},"seal"=>value with{CompleteSeal=!value.CompleteSeal},_=>value});
    }
}
[Trait("Gate","Mock")]
public sealed class SourceSnapshotMockTests
{
    [Theory,MemberData(nameof(SourceSnapshotCases.All),MemberType=typeof(SourceSnapshotCases))]
    public Task CorrectOriginalCallerCases(SourceSnapshotCase test)=>SourceSnapshotCases.AssertCase(test,new ScriptedSourceSnapshotProbe());
    [Theory,MemberData(nameof(SourceSnapshotCases.All),MemberType=typeof(SourceSnapshotCases))]
    public async Task EveryOriginalCounterOutputAndEffectRejectsIndependentMutation(SourceSnapshotCase test)
    {
        foreach(var mutation in new[]{"error","exit","eligible","groups","existing","missing","downloaded","batches","network","writes","planner","retained","paths","urls","output","seal"})
            Assert.NotNull(await Record.ExceptionAsync(()=>SourceSnapshotCases.AssertCase(test,new ScriptedSourceSnapshotProbe(mutation))));
    }
}
