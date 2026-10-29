using System.Text;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Publication;

public static class PublicationTestData
{
    public static byte[] Bytes(string text)=>Encoding.UTF8.GetBytes(text);
    public static string Hash(ReadOnlyMemory<byte> bytes)=>ContentHash.Sha256(bytes.Span);
    public const string StagePolicy="{\"maximum_file_bytes\":100,\"allow\":[\"*.md\"],\"deny\":[\"private/*\"]}";
    public static StageRequest Request()=>new("fixture",Bytes(StagePolicy),[
        new("input/a","a.md",3,Hash(Bytes("abc")),"text/markdown"),
        new("input/b","b.md",3,Hash(Bytes("def")),"text/markdown")]);
    public static string Output(string file)=>"generations/fixture/files/"+file;
    public const string Commit="generations/fixture/manifest.json";
}
public sealed record StageCase(string Id,string Fault,string[] Errors,string[] Copied,bool Committed);
public sealed record StageObservation(PublicationStageResult Result,string[] Writes,bool OriginalsPreserved,
    bool PreflightBeforeCopy,bool MarkerLast,bool ExistingPreserved,bool LeaseReleased);
public interface IStageCaseProvider { Task<StageObservation> RunAsync(StageCase test); }
public static class StageContractCases
{
    public static IEnumerable<object[]> Cases=>new[]
    {
        new StageCase("STAGE-01","none",[],["a.md","b.md"],true),
        new StageCase("STAGE-02","bad-last-source",["SOURCE_PIN_MISMATCH"],[],false),
        new StageCase("STAGE-03","existing-conflict",["DESTINATION_CONFLICT"],[],false),
        new StageCase("STAGE-04","missing-last-source",["SOURCE_MISSING"],[],false),
        new StageCase("STAGE-05","duplicate-target",["DUPLICATE_PATH"],[],false),
        new StageCase("STAGE-06","unsafe-target",["UNSAFE_PATH"],[],false),
        new StageCase("STAGE-07","not-allowed",["NOT_ALLOWED"],[],false),
        new StageCase("STAGE-08","race-conflict",["STAGE_IO_ERROR"],["a.md"],false),
        new StageCase("STAGE-09","cancel-after-first",["CANCELLED"],["a.md"],false),
        new StageCase("STAGE-10","source-changed-at-commit",["SOURCE_PIN_MISMATCH"],["a.md","b.md"],false),
        new StageCase("STAGE-11","output-changed-at-commit",["DESTINATION_PIN_MISMATCH"],["a.md","b.md"],false),
        new StageCase("STAGE-12","identical-existing",[],["b.md"],true),
        new StageCase("STAGE-13","old-marker-conflict",["GENERATION_CONFLICT"],[],false),
        new StageCase("STAGE-14","unsafe-generation",["UNSAFE_GENERATION"],[],false),
        new StageCase("STAGE-15","invalid-pin",["INVALID_PIN"],[],false),
        new StageCase("STAGE-16","unsafe-source",["UNSAFE_PATH"],[],false),
        new StageCase("STAGE-17","committed-resume",[],[],true),
        new StageCase("STAGE-18","committed-output-missing",["INCOMPLETE_COMMITTED_GENERATION"],[],false),
        new StageCase("STAGE-19","cancel-before-copy",["CANCELLED"],[],false),
        new StageCase("STAGE-20","wrong-source-size",["SOURCE_PIN_MISMATCH"],[],false)
    }.Select(x=>new object[]{x});
    public static readonly DateTimeOffset Time=DateTimeOffset.Parse("2026-10-28T00:00:00+00:00");
    public static StageObservation Correct(StageCase test)
    {
        var request=PublicationTestData.Request();
        var outputs=request.Files.Where(f=>test.Copied.Contains(f.DestinationPath) || test.Fault is "identical-existing" or "committed-resume")
            .Select(f=>new StoredArtifact(PublicationTestData.Output(f.DestinationPath),f.Sha256,f.Length,f.MediaType)).ToArray();
        StoredArtifact? marker=test.Committed ? new(PublicationTestData.Commit,"fixture-marker",1,"application/json") : null;
        return new(new("fixture",test.Committed,marker,outputs,test.Errors.Select(x=>new PublicationIssue(x,"")).ToArray(),Time,Time),
            test.Copied.Select(PublicationTestData.Output).Concat(test.Committed && test.Fault!="committed-resume"?[PublicationTestData.Commit]:Array.Empty<string>()).ToArray(),
            true,true,true,true,true);
    }
    public static async Task AssertCase(StageCase test,IStageCaseProvider provider)
    {
        var o=await provider.RunAsync(test);
        Assert.Equal(test.Committed,o.Result.Committed);
        Assert.Equal(test.Committed,o.Result.Manifest is not null);
        if(o.Result.Manifest is not null)Assert.Equal(PublicationTestData.Commit,o.Result.Manifest.RelativePath);
        Assert.Equal(test.Errors.Order(StringComparer.Ordinal),o.Result.Issues.Select(x=>x.Code).Order(StringComparer.Ordinal));
        Assert.Equal(Correct(test).Result.Outputs.Select(x=>(x.RelativePath,x.Sha256,x.Length,x.MediaType)),o.Result.Outputs.Select(x=>(x.RelativePath,x.Sha256,x.Length,x.MediaType)));
        Assert.Equal(Correct(test).Writes,o.Writes);
        Assert.True(o.OriginalsPreserved);
        Assert.True(o.PreflightBeforeCopy);
        Assert.True(o.MarkerLast);
        Assert.True(o.ExistingPreserved);
        Assert.True(o.LeaseReleased);
        Assert.Equal(Time,o.Result.StartedAt);Assert.Equal(Time,o.Result.EndedAt);
        Assert.DoesNotContain(o.Writes,x=>x.Contains(".git",StringComparison.Ordinal));
    }
}
public sealed class ScriptedStageCaseProvider(StageObservation response):IStageCaseProvider
{
    public Task<StageObservation> RunAsync(StageCase test)=>Task.FromResult(response);
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class StageMockTests
{
    [Theory,MemberData(nameof(StageContractCases.Cases),MemberType=typeof(StageContractCases))]
    public Task CorrectMocksPass(StageCase test)=>StageContractCases.AssertCase(test,new ScriptedStageCaseProvider(StageContractCases.Correct(test)));
    [Theory,MemberData(nameof(StageContractCases.Cases),MemberType=typeof(StageContractCases))]
    public async Task IndividualNegativeControlsRejectWrongCommitEffectsAndErrors(StageCase test)
    {
        var good=StageContractCases.Correct(test);
        var wrong=new[]{good with{Result=good.Result with{Committed=!good.Result.Committed}},
            good with{Result=good.Result with{Issues=[new("WRONG","")]}},good with{Writes=[".git/index"]},
            good with{OriginalsPreserved=false},good with{PreflightBeforeCopy=false},good with{MarkerLast=false},
            good with{ExistingPreserved=false},good with{LeaseReleased=false},good with{Result=good.Result with{EndedAt=good.Result.EndedAt.AddSeconds(1)}}};
        foreach(var response in wrong)
            Assert.NotNull(await Record.ExceptionAsync(()=>StageContractCases.AssertCase(test,new ScriptedStageCaseProvider(response))));
        if(good.Result.Outputs.Count>0)
            Assert.NotNull(await Record.ExceptionAsync(()=>StageContractCases.AssertCase(test,new ScriptedStageCaseProvider(good with{Result=good.Result with{Outputs=[]}}))));
        if(test.Committed)
            Assert.NotNull(await Record.ExceptionAsync(()=>StageContractCases.AssertCase(test,new ScriptedStageCaseProvider(good with{Result=good.Result with{Manifest=null}}))));
        foreach(var error in test.Errors)
            Assert.NotNull(await Record.ExceptionAsync(()=>StageContractCases.AssertCase(test,new ScriptedStageCaseProvider(good with{Result=good.Result with{Issues=good.Result.Issues.Where(x=>x.Code!=error).ToArray()}}))));
    }
}
