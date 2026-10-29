using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Execution;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Tests.Publication;
using AndroidMcp.BuildTool.Tests.Sources.Acquisition;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed class SourcesSeedOperationFixture:IDisposable
{
    public string Mode{get;}
    public SourcesSeedRequest Request{get;}
    public RehydrationSeedPlanResult Planned{get;}
    public SourceSeedResult Expected{get;}
    public SourceBatchSeedRequest ExpectedSeedRequest{get;}
    public CancellationTokenSource Cancellation{get;}=new();
    public Exception Sentinel{get;}=new IOException("Literal seed capability failure");
    public List<RehydrationPlanRequest> OrdinaryPlans{get;}=[];
    public List<RehydrationPlanRequest> SeedPlans{get;}=[];
    public List<(SourceBatchSeedRequest Request,CancellationToken Token)> Seeds{get;}=[];
    public bool Success=>Mode is "normal" or "explicit-options" or "sealed-replay" or "absent" or "last-alias";
    public string? ErrorCode=>Mode switch{
        "invalid-plan"=>"SEED_PLAN_INVALID","missing-projection"=>"SEED_PROJECTION_REQUIRED",
        "missing-seed-projection"=>"SEED_CANDIDATES_REQUIRED",
        "non-pure-network" or "non-pure-complete" or "non-pure-historical"=>"SEED_PLAN_NOT_PURE",_=>null};
    public SourcesSeedOperationFixture(string mode)
    {
        Mode=mode;
        var id=mode switch{"sealed-replay"=>"sealed-replay","absent"=>"absent","last-alias"=>"different-alias-last-snapshot",_=>"local-import"};
        var counts=mode=="sealed-replay"?new SourceSeedCounts(0,1,0):mode=="absent"?new(0,0,1):new(1,0,0);
        var source=SourceSeedFixture.Create(new(id,null,counts,[],[],[]));
        Request=new(RehydrationContractCases.Request());
        if(mode=="explicit-options")Request=Request with{GenerationId="explicit-seed",ReceiptPath="receipts/explicit-seed.json",
            Recipe=Request.Recipe with{ExpectedRelease="fixture-release",Profile=AcquisitionCompatibilityProfile.GenericRehydrate}};
        var projection=source.Request.Acquisition.Projection with{Profile=Request.Recipe.Profile};
        ExpectedSeedRequest=new(new(Request.GenerationId,Request.ReceiptPath,projection),source.Request.SeedProjection);
        var plan=new RehydrationPlan(projection.Release,projection.RecipeSha256,projection.Rows.Count,projection.PhysicalSources.Count,3,0,[],[],
            new("rehydration-plan.json",Array.Empty<byte>(),"application/json"),false,false,false,[],SourceProjection:projection);
        Planned=new(new(plan,[]),source.Request.SeedProjection);
        if(mode=="invalid-plan")Planned=new(new(null,[new("LOCK_MISMATCH","rehydration/fixture.lock.json")]),null);
        if(mode=="missing-projection")Planned=Planned with{BaseResult=new(plan with{SourceProjection=null},[])};
        if(mode=="missing-seed-projection")Planned=Planned with{SeedProjection=null};
        if(mode=="non-pure-network")Planned=Planned with{BaseResult=new(plan with{Network=true},[])};
        if(mode=="non-pure-complete")Planned=Planned with{BaseResult=new(plan with{Complete=true},[])};
        if(mode=="non-pure-historical")Planned=Planned with{BaseResult=new(plan with{HistoricalByteIdentityClaimed=true},[])};
        var receipt=source.Expected.Receipt with{Acquisition=ExpectedSeedRequest.Acquisition,
            RequestSha256=SourceBatchCases.Canonical.Hash(JsonSerializer.SerializeToElement(ExpectedSeedRequest))};
        var bytes=SourceBatchCases.Canonical.Serialize(JsonSerializer.SerializeToElement(receipt));
        Expected=source.Expected with{Receipt=receipt,Manifest=new(Request.ReceiptPath,ContentHash.Sha256(bytes),bytes.Length,"application/json")};
        if(mode=="cancelled")Cancellation.Cancel();
    }
    public async Task AssertContract(ISourcesSeedOperation provider)
    {
        SourceSeedResult? result=null;Exception? error=null;
        try{result=await provider.RunAsync(Request,Cancellation.Token);}catch(Exception failure){error=failure;}
        if(Success){Assert.Null(error);Assert.Same(Expected,result);}
        else if(Mode is "cancelled" or "cancel-after-plan")Assert.IsAssignableFrom<OperationCanceledException>(error);
        else if(ErrorCode is not null){var typed=Assert.IsType<SourcesSeedException>(error);Assert.Equal(ErrorCode,typed.Code);
            Assert.Equal(Mode=="invalid-plan"?Planned.BaseResult.Issues:Array.Empty<PublicationIssue>(),typed.Issues);}
        else Assert.Same(Sentinel,error);
        Assert.Empty(OrdinaryPlans);Assert.Equal(Mode=="cancelled"?0:1,SeedPlans.Count);
        if(SeedPlans.Count>0)Assert.Same(Request.Recipe,SeedPlans[0]);
        var entered=Success||Mode=="seed-failure";Assert.Equal(entered?1:0,Seeds.Count);
        if(entered){var actual=Seeds[0];Assert.Equal(Request.GenerationId,actual.Request.Acquisition.GenerationId);
            Assert.Equal(Request.ReceiptPath,actual.Request.Acquisition.ReceiptPath);Assert.Same(Planned.BaseResult.Plan!.SourceProjection,actual.Request.Acquisition.Projection);
            Assert.Same(Planned.SeedProjection,actual.Request.SeedProjection);Assert.Equal(SourceBatchCachePolicy.PreserveAliases,actual.Request.Acquisition.CachePolicy);
            Assert.Equal(Cancellation.Token,actual.Token);}
        if(Mode=="last-alias"){
            Assert.Equal("snapshots/first",Planned.BaseResult.Plan!.SourceProjection!.PhysicalSources[0].Pin.RequiredLocalSnapshot);
            Assert.Equal("snapshots/last",Planned.SeedProjection!.Sources[0].Pin.RequiredLocalSnapshot);
            Assert.Same(Planned.SeedProjection.Sources[0].Pin,Seeds[0].Request.SeedProjection.Sources[0].Pin);
        }
        if(Mode=="sealed-replay"){
            Assert.Equal(new SourceSeedCounts(1,0,0),result!.Receipt.OriginalCounts);
            Assert.Equal(new SourceSeedCounts(0,1,0),result.CurrentCounts);Assert.True(result.Reused);
        }
        if(Mode=="absent"){Assert.False(result!.Receipt.RawComplete);Assert.Equal(1,result.CurrentCounts.Absent);}
    }
    public void Dispose()=>Cancellation.Dispose();
}
public sealed class SeedOperationPlannerProbe(SourcesSeedOperationFixture fixture):IRehydrationSeedPlanner
{
    public RehydrationPlanResult Plan(RehydrationPlanRequest request){fixture.OrdinaryPlans.Add(request);return fixture.Planned.BaseResult;}
    public RehydrationSeedPlanResult PlanSeed(RehydrationPlanRequest request){fixture.SeedPlans.Add(request);
        if(fixture.Mode=="planner-failure")throw fixture.Sentinel;if(fixture.Mode=="cancel-after-plan")fixture.Cancellation.Cancel();return fixture.Planned;}
}
public sealed class SeedOperationEngineProbe(SourcesSeedOperationFixture fixture):ISourceBatchSeeder
{
    public Task<SourceSeedResult> SeedOnlyAsync(SourceBatchSeedRequest request,CancellationToken cancellationToken=default){
        fixture.Seeds.Add((request,cancellationToken));if(fixture.Mode=="seed-failure")throw fixture.Sentinel;return Task.FromResult(fixture.Expected);}
}
public sealed class ScriptedSourcesSeedOperation(SourcesSeedOperationFixture fixture,string mutation=""):ISourcesSeedOperation
{
    public Task<SourceSeedResult> RunAsync(SourcesSeedRequest request,CancellationToken cancellationToken=default)
    {
        if(mutation!="ignore-cancel")cancellationToken.ThrowIfCancellationRequested();
        var planner=new SeedOperationPlannerProbe(fixture);
        if(mutation=="ordinary-plan")planner.Plan(request.Recipe);
        var planned=planner.PlanSeed(mutation=="recipe"?request.Recipe with{Limit=99}:request.Recipe);
        if(mutation=="double-plan")planner.PlanSeed(request.Recipe);
        if(mutation!="ignore-cancel")cancellationToken.ThrowIfCancellationRequested();
        if(!planned.BaseResult.IsValid&&mutation!="ignore-failure")throw new SourcesSeedException("SEED_PLAN_INVALID",planned.BaseResult.Issues);
        if(planned.BaseResult.Plan?.SourceProjection is null&&mutation!="ignore-failure")throw new SourcesSeedException("SEED_PROJECTION_REQUIRED",[]);
        if(planned.SeedProjection is null&&mutation!="ignore-failure")throw new SourcesSeedException("SEED_CANDIDATES_REQUIRED",[]);
        if(planned.BaseResult.Plan is{} plan&&(plan.Network||plan.Complete||plan.HistoricalByteIdentityClaimed)&&mutation!="ignore-failure")throw new SourcesSeedException("SEED_PLAN_NOT_PURE",[]);
        var baseProjection=planned.BaseResult.Plan?.SourceProjection??fixture.ExpectedSeedRequest.Acquisition.Projection;
        var seedProjection=planned.SeedProjection??fixture.ExpectedSeedRequest.SeedProjection;
        if(mutation=="first-pin-fallback")seedProjection=seedProjection with{Sources=seedProjection.Sources.Select(source=>source with{Pin=baseProjection.PhysicalSources.Single(item=>item.PhysicalSourceIndex==source.PhysicalSourceIndex).Pin}).ToArray()};
        var seed=new SourceBatchSeedRequest(new(mutation=="generation"?"wrong":request.GenerationId,mutation=="receipt"?"wrong.json":request.ReceiptPath,
            mutation=="base-projection"?baseProjection with{Rows=[]}:baseProjection,
            mutation=="policy"?SourceBatchCachePolicy.ImportLockedHistorical:SourceBatchCachePolicy.PreserveAliases),
            mutation=="seed-projection"?seedProjection with{Sources=[]}:seedProjection);
        var result=new SeedOperationEngineProbe(fixture).SeedOnlyAsync(seed,mutation=="token"?CancellationToken.None:cancellationToken);
        return mutation=="result"?Task.FromResult(fixture.Expected with{Reused=!fixture.Expected.Reused}):result;
    }
}
[Trait("Gate","Mock")][Trait("Requirement","TEST-BUILD-004")]
public sealed class SourcesSeedOperationMockTests
{
    public static IEnumerable<object[]> Modes()=>new[]{"normal","explicit-options","sealed-replay","absent","last-alias","invalid-plan","missing-projection","missing-seed-projection","non-pure-network","non-pure-complete","non-pure-historical","cancelled","cancel-after-plan","planner-failure","seed-failure"}.Select(mode=>new object[]{mode});
    [Theory][MemberData(nameof(Modes))]public async Task CorrectOnePassCapabilityContractPasses(string mode){using var f=new SourcesSeedOperationFixture(mode);await f.AssertContract(new ScriptedSourcesSeedOperation(f));}
    [Theory][InlineData("recipe")][InlineData("ordinary-plan")][InlineData("double-plan")][InlineData("generation")][InlineData("receipt")][InlineData("base-projection")][InlineData("seed-projection")][InlineData("policy")][InlineData("token")][InlineData("result")]
    public async Task EachPassedPropertyAndInteractionRejectsMutation(string mutation){using var f=new SourcesSeedOperationFixture("normal");Assert.NotNull(await Record.ExceptionAsync(()=>f.AssertContract(new ScriptedSourcesSeedOperation(f,mutation))));}
    [Theory][InlineData("invalid-plan")][InlineData("missing-projection")][InlineData("missing-seed-projection")][InlineData("non-pure-network")][InlineData("non-pure-complete")][InlineData("non-pure-historical")]
    public async Task RejectedPlanCannotEnterSeedEngine(string mode){using var f=new SourcesSeedOperationFixture(mode);Assert.NotNull(await Record.ExceptionAsync(()=>f.AssertContract(new ScriptedSourcesSeedOperation(f,"ignore-failure"))));}
    [Theory][InlineData("cancelled")][InlineData("cancel-after-plan")]
    public async Task CancellationCannotEnterLaterCapability(string mode){using var f=new SourcesSeedOperationFixture(mode);Assert.NotNull(await Record.ExceptionAsync(()=>f.AssertContract(new ScriptedSourcesSeedOperation(f,"ignore-cancel"))));}
    [Fact]public async Task WinningLastAliasCannotFallBackToFirstPin(){using var f=new SourcesSeedOperationFixture("last-alias");Assert.NotNull(await Record.ExceptionAsync(()=>f.AssertContract(new ScriptedSourcesSeedOperation(f,"first-pin-fallback"))));}
}
