using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Execution;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Tests.Publication;
using AndroidMcp.BuildTool.Tests.Sources.Acquisition;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;

public sealed class FetchPlannerProbe(RehydrationPlanResult response) : IRehydrationPlanner
{
    public List<RehydrationPlanRequest> Requests { get; }=[];
    public RehydrationPlanResult Plan(RehydrationPlanRequest request){Requests.Add(request);return response;}
}
public sealed class FetchBatchProbe(SourceBatchResult result) : ISourceBatch
{
    public List<(SourceBatchRequest Request,CancellationToken Token)> Requests { get; }=[];
    public Task<SourceBatchResult> RunAsync(SourceBatchRequest request,CancellationToken token=default)
    {Requests.Add((request,token));return Task.FromResult(result);}
}
public sealed class SourcesFetchHarness
{
    public SourcesFetchRequest Input { get; }
    public RehydrationPlanResult Planned { get; }
    public SourceBatchResult Expected { get; }
    public FetchPlannerProbe Planner { get; }
    public FetchBatchProbe Batch { get; }
    public CancellationTokenSource Cancellation { get; }=new();
    public string Mode { get; }
    public SourcesFetchHarness(string mode)
    {
        Mode=mode;
        var fixture=(SourceBatchCase)SourceBatchCases.All.First()[0];
        var projection=fixture.Request.Projection;
        Input=new(RehydrationContractCases.Request(),"fetch-generation","sources/acquisition.json");
        if(mode=="explicit-options")Input=Input with{Recipe=Input.Recipe with{ExpectedRelease="fixture-release",Limit=1,Resume=false,Profile=AcquisitionCompatibilityProfile.GenericRehydrate},CachePolicy=SourceBatchCachePolicy.ImportLockedHistorical};
        projection=projection with{Profile=Input.Recipe.Profile};
        Planned=new(new("fixture-release",projection.RecipeSha256,1,1,3,0,[],[],new("plan.json",Array.Empty<byte>(),"application/json"),true,false,false,[],SourceProjection:projection),[]);
        if(mode=="invalid-plan")Planned=new(null,[new("INVALID_RECIPE","fixture.recipe")]);
        if(mode=="missing-projection")Planned=Planned with{Plan=Planned.Plan! with{SourceProjection=null}};
        Expected=new(SourceBatchCases.ExpectedReceipt(fixture) with{GenerationId=Input.GenerationId,Projection=projection,CachePolicy=Input.CachePolicy},new("sources/acquisition.json",new string('1',64),17,"application/json"),false);
        Planner=new(Planned);Batch=new(Expected);
        if(mode=="cancelled")Cancellation.Cancel();
    }
    public async Task AssertContract(ISourcesFetchOperation provider)
    {
        if(Mode=="cancelled")await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>provider.RunAsync(Input,Cancellation.Token));
        else if(Mode is "invalid-plan" or "missing-projection")
        {
            var error=await Assert.ThrowsAsync<SourcesFetchException>(()=>provider.RunAsync(Input,Cancellation.Token));
            Assert.Equal(Mode=="invalid-plan"?"FETCH_PLAN_INVALID":"FETCH_PROJECTION_REQUIRED",error.Code);
            Assert.Equal(Mode=="invalid-plan"?Planned.Issues:Array.Empty<PublicationIssue>(),error.Issues);
        }
        else Assert.Same(Expected,await provider.RunAsync(Input,Cancellation.Token));
        Assert.Equal(Mode=="cancelled"?0:1,Planner.Requests.Count);
        if(Planner.Requests.Count>0)Assert.Same(Input.Recipe,Planner.Requests.Single());
        var successful=Mode is "normal" or "explicit-options";
        Assert.Equal(successful?1:0,Batch.Requests.Count);
        if(successful)
        {
            var actual=Batch.Requests.Single();
            Assert.Equal(Input.GenerationId,actual.Request.GenerationId);
            Assert.Equal(Input.ReceiptPath,actual.Request.ReceiptPath);
            Assert.Same(Planned.Plan!.SourceProjection,actual.Request.Projection);
            Assert.Equal(Input.CachePolicy,actual.Request.CachePolicy);
            Assert.Equal(Cancellation.Token,actual.Token);
        }
    }
}
public sealed class ScriptedSourcesFetchOperation(SourcesFetchHarness test,string mutation="") : ISourcesFetchOperation
{
    public Task<SourceBatchResult> RunAsync(SourcesFetchRequest request,CancellationToken token=default)
    {
        if(test.Mode=="cancelled"&&mutation!="ignore-cancel")token.ThrowIfCancellationRequested();
        var planned=test.Planner.Plan(mutation=="recipe"?request.Recipe with{Limit=17}:request.Recipe);
        if(mutation=="double-plan")_=test.Planner.Plan(request.Recipe);
        if(test.Mode=="invalid-plan"&&mutation!="early-batch")throw new SourcesFetchException("FETCH_PLAN_INVALID",planned.Issues);
        if(test.Mode=="missing-projection"&&mutation!="early-batch")throw new SourcesFetchException("FETCH_PROJECTION_REQUIRED",[]);
        var batch=new SourceBatchRequest(mutation=="generation"?"wrong":request.GenerationId,mutation=="receipt"?"wrong.json":request.ReceiptPath,
            mutation=="projection"?planned.Plan!.SourceProjection! with{Rows=[]}:planned.Plan?.SourceProjection!,mutation=="policy"?(SourceBatchCachePolicy)99:request.CachePolicy);
        var result=test.Batch.RunAsync(batch,mutation=="token"?CancellationToken.None:token);
        return mutation=="result"?Task.FromResult(test.Expected with{Reused=true}):result;
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
public sealed class SourcesFetchOperationMockTests
{
    [Theory]
    [InlineData("normal")][InlineData("explicit-options")][InlineData("invalid-plan")][InlineData("missing-projection")][InlineData("cancelled")]
    public Task CorrectDependencyContractPasses(string mode){var test=new SourcesFetchHarness(mode);return test.AssertContract(new ScriptedSourcesFetchOperation(test));}
    [Theory]
    [InlineData("recipe")][InlineData("double-plan")][InlineData("generation")][InlineData("receipt")][InlineData("projection")][InlineData("policy")][InlineData("token")][InlineData("result")]
    public async Task EachPassedArgumentAndInteractionRejectsMutation(string mutation)
    {var test=new SourcesFetchHarness("normal");Assert.NotNull(await Record.ExceptionAsync(()=>test.AssertContract(new ScriptedSourcesFetchOperation(test,mutation))));}
    [Theory]
    [InlineData("invalid-plan","early-batch")][InlineData("missing-projection","early-batch")][InlineData("cancelled","ignore-cancel")]
    public async Task InvalidPlanOrCancellationCannotReachAcquisition(string mode,string mutation)
    {var test=new SourcesFetchHarness(mode);Assert.NotNull(await Record.ExceptionAsync(()=>test.AssertContract(new ScriptedSourcesFetchOperation(test,mutation))));}
}
