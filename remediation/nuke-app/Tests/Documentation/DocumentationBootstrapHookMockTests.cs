using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed class BootstrapHookEnvironment
{
    public string Scenario{get;}
    public DocumentationBootstrapEnvironment Original{get;}=new(new("BOOTSTRAP-ONLINE",ToolArtifactAccess.Online,null,""));
    public List<string> Trace{get;}=[];
    public List<CancellationToken> Tokens{get;}=[];
    public List<ToolArtifactRequest> Requests{get;}=[];
    public CancellationTokenSource Cancellation{get;}=new();
    public BootstrapHookEnvironment(string scenario){Scenario=scenario;if(scenario=="CANCELLED")Cancellation.Cancel();}
    public IToolArtifactAcquirer Tools=>new RecordingTools(this);
    public IDocumentationBootstrapFileEffects Effects=>new RecordingEffects(this);
    private sealed class RecordingTools(BootstrapHookEnvironment e):IToolArtifactAcquirer
    {
        public Task<ToolArtifactReceipt> AcquireAsync(ToolArtifactRequest request,CancellationToken token=default)
        {
            token.ThrowIfCancellationRequested();var index=e.Requests.Count;Assert.InRange(index,0,4);
            e.Requests.Add(request);e.Tokens.Add(token);e.Trace.Add("acquire:"+index);Assert.Equal(e.Original.Requests[index],request);
            if(e.Scenario=="ACQUIRE-"+index)throw new TransportException("TOOL_HASH","original acquisition failure fixture");
            return Task.FromResult(e.Original.Receipts[index]);
        }
    }
    private sealed class RecordingEffects(BootstrapHookEnvironment e):IDocumentationBootstrapFileEffects
    {
        public Task GradleExtractedAsync(DocumentationRendererProfile profile,ToolArtifactReceipt gradle,CancellationToken token=default)
        {
            token.ThrowIfCancellationRequested();e.Tokens.Add(token);Assert.Same(e.Original.Profile,profile);Assert.Same(e.Original.Receipts[0],gradle);e.Trace.Add("gradle-effects");
            if(e.Scenario=="GRADLE-EFFECT")throw new IOException("gradle permission fixture");
            if(e.Scenario=="AFTER-GRADLE-CANCEL")e.Cancellation.Cancel();return Task.CompletedTask;
        }
        public Task WrappersAcquiredAsync(DocumentationRendererProfile profile,CancellationToken token=default)
        {
            token.ThrowIfCancellationRequested();e.Tokens.Add(token);Assert.Same(e.Original.Profile,profile);e.Trace.Add("wrapper-effects");
            if(e.Scenario=="WRAPPER-EFFECT")throw new IOException("wrapper permission fixture");
            if(e.Scenario=="PROPERTY-REJECTION")throw new ArchitectureAssetException("WRAPPER_PROPERTIES","Committed wrapper properties differ from pinned expected configuration");
            return Task.CompletedTask;
        }
    }
}
public static class BootstrapHookCases
{
    public static IEnumerable<object[]> Cases=>new[]{"FULL","ACQUIRE-0","ACQUIRE-1","ACQUIRE-2","ACQUIRE-3","ACQUIRE-4","GRADLE-EFFECT","WRAPPER-EFFECT","PROPERTY-REJECTION","CANCELLED","AFTER-GRADLE-CANCEL"}.Select(id=>new object[]{id});
    private static string[] ExpectedTrace(string scenario)
    {
        string[] all=["acquire:0","gradle-effects","acquire:1","acquire:2","acquire:3","wrapper-effects","acquire:4"];
        var last=scenario switch{"FULL"=>6,"CANCELLED"=>-1,"GRADLE-EFFECT" or "AFTER-GRADLE-CANCEL"=>1,"WRAPPER-EFFECT" or "PROPERTY-REJECTION"=>5,
            "ACQUIRE-0"=>0,"ACQUIRE-1"=>2,"ACQUIRE-2"=>3,"ACQUIRE-3"=>4,"ACQUIRE-4"=>6,_=>throw new ArgumentException("Unknown scenario")};
        return all.Take(last+1).ToArray();
    }
    public static async Task AssertContract(BootstrapHookEnvironment e,IDocumentationToolBootstrap provider)
    {
        IReadOnlyList<ToolArtifactReceipt>? actual=null;var error=await Record.ExceptionAsync(async()=>actual=await provider.AcquireAsync(e.Original.Profile,ToolArtifactAccess.Online,null,e.Cancellation.Token));
        if(e.Scenario=="FULL"){Assert.Null(error);Assert.Equal(e.Original.Receipts,actual);}
        else if(e.Scenario is "CANCELLED" or "AFTER-GRADLE-CANCEL")Assert.IsAssignableFrom<OperationCanceledException>(error);
        else if(e.Scenario.StartsWith("ACQUIRE-",StringComparison.Ordinal))Assert.Equal("TOOL_HASH",Assert.IsType<TransportException>(error).Code);
        else if(e.Scenario=="PROPERTY-REJECTION")Assert.Equal("WRAPPER_PROPERTIES",Assert.IsType<ArchitectureAssetException>(error).Code);
        else Assert.IsType<IOException>(error);
        if(e.Scenario!="FULL")Assert.Null(actual);
        Assert.Equal(ExpectedTrace(e.Scenario),e.Trace);foreach(var token in e.Tokens)Assert.Equal(e.Cancellation.Token,token);
    }
}
public sealed class ScriptedBootstrapHooks(BootstrapHookEnvironment e,string mutation=""):IDocumentationToolBootstrap
{
    public async Task<IReadOnlyList<ToolArtifactReceipt>> AcquireAsync(DocumentationRendererProfile profile,ToolArtifactAccess access,string? proof,CancellationToken token=default)
    {
        Assert.Same(e.Original.Profile,profile);Assert.Equal(ToolArtifactAccess.Online,access);Assert.Null(proof);token.ThrowIfCancellationRequested();
        var result=new List<ToolArtifactReceipt>();
        for(var index=0;index<5;index++){
            var receipt=await e.Tools.AcquireAsync(e.Original.Requests[index],token);result.Add(receipt);
            if(index==0&&mutation!="skip-gradle")await e.Effects.GradleExtractedAsync(profile,mutation=="gradle-receipt"?e.Original.Receipts[1]:receipt,mutation=="token"?CancellationToken.None:token);
            if(index==0&&mutation=="duplicate-gradle")await e.Effects.GradleExtractedAsync(profile,receipt,token);
            if(index==3&&mutation is not ("skip-wrappers" or "late-wrappers"))await e.Effects.WrappersAcquiredAsync(profile,token);
        }
        if(mutation=="late-wrappers")await e.Effects.WrappersAcquiredAsync(profile,token);
        if(mutation=="result-order")result.Reverse();return mutation=="result-omission"?result.Skip(1).ToArray():result;
    }
}
[Trait("Gate","Mock"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationBootstrapHookMockTests
{
    [Theory,MemberData(nameof(BootstrapHookCases.Cases),MemberType=typeof(BootstrapHookCases))]
    public Task CorrectMocksPreserveExactOriginalAcquisitionAndEffectSequence(string scenario)
    {var e=new BootstrapHookEnvironment(scenario);return BootstrapHookCases.AssertContract(e,new ScriptedBootstrapHooks(e));}
    [Theory]
    [InlineData("skip-gradle")][InlineData("skip-wrappers")][InlineData("gradle-receipt")][InlineData("duplicate-gradle")][InlineData("late-wrappers")][InlineData("result-order")][InlineData("result-omission")][InlineData("token")]
    public async Task EachOriginalOrderEffectReceiptAndTokenAssertionRejectsWrongMocks(string mutation)
    {var e=new BootstrapHookEnvironment("FULL");Assert.NotNull(await Record.ExceptionAsync(()=>BootstrapHookCases.AssertContract(e,new ScriptedBootstrapHooks(e,mutation))));}
    [Theory,InlineData("GRADLE-EFFECT","skip-gradle"),InlineData("WRAPPER-EFFECT","skip-wrappers"),InlineData("PROPERTY-REJECTION","skip-wrappers")]
    public async Task FailureShortCircuitCannotBeBypassed(string scenario,string mutation)
    {var e=new BootstrapHookEnvironment(scenario);Assert.NotNull(await Record.ExceptionAsync(()=>BootstrapHookCases.AssertContract(e,new ScriptedBootstrapHooks(e,mutation))));}
}
