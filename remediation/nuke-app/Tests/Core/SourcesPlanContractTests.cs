using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Execution;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Tests.Publication;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;

public interface ISourcesPlanProbe
{
    Task<SourcesPlanResult> RunAsync(RehydrationPlanRequest request,CancellationToken token);
    IReadOnlyList<RehydrationPlanRequest> PlannerRequests{get;}
    IReadOnlyList<(Artifact Artifact,CancellationToken Token)> Writes{get;}
    int NetworkCalls{get;}
    int ProcessCalls{get;}
}
public sealed class SourcesPlanCase
{
    public string Mode{get;}
    public RehydrationPlanRequest Input{get;}
    public RehydrationPlanResult Planned{get;}
    public CancellationTokenSource Cancellation{get;}=new();
    private static ProjectionCase Projection(string mode)=>new("plan-facade",Limit:mode=="explicit-options"?1:null,
        Profile:mode=="explicit-options"?AcquisitionCompatibilityProfile.GenericRehydrate:AcquisitionCompatibilityProfile.CanonicalRestore,MissingCacheCount:true);
    public static SourcesPlanCase Create(string mode)
    {
        var fixture=Projection(mode);
        var planned=RehydrationProjectionCases.Correct(fixture);
        return new(mode,RehydrationProjectionCases.Request(fixture),mode switch {
            "invalid-plan"=>new(null,[new("INVALID_RECIPE","rehydration/fixture.json")]),
            "missing-projection"=>planned with{Plan=planned.Plan! with{SourceProjection=null}},
            "network-claim"=>planned with{Plan=planned.Plan! with{Network=true}},
            "complete-claim"=>planned with{Plan=planned.Plan! with{Complete=true}},
            "historical-claim"=>planned with{Plan=planned.Plan! with{HistoricalByteIdentityClaimed=true}},_=>planned});
    }
    private SourcesPlanCase(string mode,RehydrationPlanRequest input,RehydrationPlanResult planned)
    {Mode=mode;Input=input;Planned=planned;if(mode=="cancelled")Cancellation.Cancel();}
    public static IEnumerable<object[]> Cases=>new[]{"normal","explicit-options","invalid-plan","missing-projection","network-claim","complete-claim","historical-claim","cancelled","write-failure"}.Select(x=>new object[]{Create(x)});
    public async Task AssertCase(ISourcesPlanProbe provider)
    {
        var success=Mode is "normal" or "explicit-options";
        SourcesPlanResult? result=null;
        if(Mode=="cancelled")await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>provider.RunAsync(Input,Cancellation.Token));
        else if(Mode=="write-failure")await Assert.ThrowsAsync<IOException>(()=>provider.RunAsync(Input,Cancellation.Token));
        else if(!success)
        {
            var error=await Assert.ThrowsAsync<SourcesPlanException>(()=>provider.RunAsync(Input,Cancellation.Token));
            Assert.Equal(Mode=="invalid-plan"?"PLAN_INVALID":Mode=="missing-projection"?"PLAN_PROJECTION_REQUIRED":"PLAN_NOT_PURE",error.Code);
            Assert.Equal(Mode=="invalid-plan"?Planned.Issues:Array.Empty<PublicationIssue>(),error.Issues);
        }
        else result=await provider.RunAsync(Input,Cancellation.Token);
        Assert.Equal(Mode=="cancelled"?0:1,provider.PlannerRequests.Count);
        if(provider.PlannerRequests.Count>0)Assert.Same(Input,provider.PlannerRequests.Single());
        Assert.Equal(success||Mode=="write-failure"?1:0,provider.Writes.Count);
        Assert.Equal(0,provider.NetworkCalls);Assert.Equal(0,provider.ProcessCalls);
        if(provider.Writes.Count>0){Assert.Same(Planned.Plan!.Manifest,provider.Writes[0].Artifact);Assert.Equal(Cancellation.Token,provider.Writes[0].Token);}
        if(!success)return;
        Assert.NotNull(result);Assert.Same(Planned.Plan,result.Plan);
        var manifest=Planned.Plan!.Manifest;
        Assert.Equal(manifest.RelativePath,result.Manifest.RelativePath);Assert.Equal(ContentHash.Sha256(manifest.Bytes.Span),result.Manifest.Sha256);
        Assert.Equal(manifest.Bytes.Length,result.Manifest.Length);Assert.Equal(manifest.MediaType,result.Manifest.MediaType);
        Assert.False(result.Plan.Network);Assert.False(result.Plan.Complete);Assert.False(result.Plan.HistoricalByteIdentityClaimed);
        var projection=Assert.IsType<RehydrationSourceProjection>(result.Plan.SourceProjection);
        Assert.Equal(5,projection.Rows.Count);Assert.Equal(4,projection.PhysicalSources.Count);
        Assert.Equal(Mode=="explicit-options"?1:4,projection.SelectedPhysicalSourceIndices.Count);
        var required=projection.PhysicalSources.SelectMany(x=>x.Prerequisites).Select(x=>x.Code).ToArray();
        Assert.Contains(RehydrationPrerequisiteCode.RequiredImmutableSnapshot,required);
        Assert.Equal(Mode=="explicit-options"?1:5,required.Length);
        if(Mode!="explicit-options")foreach(var code in new[]{RehydrationPrerequisiteCode.CanonicalArchiveETag,RehydrationPrerequisiteCode.CanonicalArchivePlatformVersion,RehydrationPrerequisiteCode.CanonicalArchiveRepositoryXml,RehydrationPrerequisiteCode.CanonicalArchiveSize})Assert.Contains(code,required);
        Assert.Contains(projection.CacheAliases.SelectMany(x=>x.Prerequisites),x=>x.Code==RehydrationPrerequisiteCode.HistoricalCacheRecordCount);
        Assert.Contains(projection.PhysicalSources.SelectMany(x=>x.RequiredSnapshots),x=>x.BundlePath is null);
    }
}
public sealed class ScriptedSourcesPlanProbe(SourcesPlanCase test,string? mutation=null):ISourcesPlanProbe
{
    private readonly List<RehydrationPlanRequest> requests=[];
    private readonly List<(Artifact Artifact,CancellationToken Token)> writes=[];
    public IReadOnlyList<RehydrationPlanRequest> PlannerRequests=>requests;
    public IReadOnlyList<(Artifact Artifact,CancellationToken Token)> Writes=>writes;
    public int NetworkCalls=>mutation=="network"?1:0;
    public int ProcessCalls=>mutation=="process"?1:0;
    public Task<SourcesPlanResult> RunAsync(RehydrationPlanRequest request,CancellationToken token)
    {
        if(test.Mode=="cancelled"&&mutation!="cancel")token.ThrowIfCancellationRequested();
        requests.Add(mutation=="recipe"?request with{Limit=99}:request);if(mutation=="double-plan")requests.Add(request);
        var expected=test.Mode=="invalid-plan"?"PLAN_INVALID":test.Mode=="missing-projection"?"PLAN_PROJECTION_REQUIRED":"PLAN_NOT_PURE";
        if(test.Mode is not ("normal" or "explicit-options" or "write-failure" or "cancelled")&&mutation!="early-write")throw new SourcesPlanException(mutation=="error-code"?"wrong":expected,mutation=="issues"?[new("wrong","wrong")]:test.Mode=="invalid-plan"?test.Planned.Issues:[]);
        var plan=test.Planned.Plan??RehydrationProjectionCases.Correct(new("fallback",MissingCacheCount:true)).Plan!;
        if(mutation!="no-write")writes.Add((mutation=="artifact"?plan.Manifest with{RelativePath="wrong"}:plan.Manifest,mutation=="token"?CancellationToken.None:token));
        if(mutation=="double-write")writes.Add((plan.Manifest,token));
        if(test.Mode=="write-failure"&&mutation!="ignore-write-error")throw new IOException("Scripted retained output failure.");
        return Task.FromResult(new SourcesPlanResult(mutation=="plan"?plan with{SourceProjection=null}:plan,new(
            mutation=="path"?"wrong":plan.Manifest.RelativePath,mutation=="hash"?"wrong":ContentHash.Sha256(plan.Manifest.Bytes.Span),mutation=="length"?-1:plan.Manifest.Bytes.Length,mutation=="media"?"wrong":plan.Manifest.MediaType)));
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
public sealed class SourcesPlanMockTests
{
    [Theory,MemberData(nameof(SourcesPlanCase.Cases),MemberType=typeof(SourcesPlanCase))]
    public Task CorrectInvocationAndTruthfulMetadata(SourcesPlanCase test)=>test.AssertCase(new ScriptedSourcesPlanProbe(test));
    [Theory,MemberData(nameof(SourcesPlanCase.Cases),MemberType=typeof(SourcesPlanCase))]
    public async Task EachWrongArgumentEffectOrMetadataIsRejected(SourcesPlanCase test)
    {
        var mutations=test.Mode is "normal" or "explicit-options"?new[]{"recipe","double-plan","artifact","token","no-write","double-write","plan","path","hash","length","media","network","process"}
            :test.Mode=="cancelled"?new[]{"cancel","network","process"}:test.Mode=="write-failure"?new[]{"recipe","double-plan","artifact","token","no-write","double-write","ignore-write-error","network","process"}
            :new[]{"recipe","double-plan","early-write","error-code","issues","network","process"};
        foreach(var mutation in mutations)Assert.NotNull(await Record.ExceptionAsync(()=>test.AssertCase(new ScriptedSourcesPlanProbe(test,mutation))));
    }
}
