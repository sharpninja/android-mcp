using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Documentation.Browser;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Browser;

// A scripted lifecycle model: expected result values come from the three
// fixture calls, not from production client logic or a live browser.
internal sealed class LifecycleCdpMock:ICdpClient,ICdpClientDiagnostics
{
    private readonly ControlledCdpWire wire;private readonly ICdpLifecycleObserver? observer;private readonly string? mutation;private readonly object sync=new();
    private readonly Dictionary<int,TaskCompletionSource<JsonObject>> replies=[];private readonly HashSet<int> cancelled=[];private readonly CancellationTokenSource lifetime=new();private readonly Task pump;private Exception? terminal;private int next;
    internal LifecycleCdpMock(ControlledCdpWire wire,ICdpLifecycleObserver? observer,string? mutation=null){this.wire=wire;this.observer=observer;this.mutation=mutation;pump=PumpAsync();}
    public int PendingCommands{get{lock(sync)return mutation=="count"?999:replies.Count;}}
    public async Task<JsonObject> CallAsync(string method,JsonObject? parameters=null,string? sessionId=null,CancellationToken cancellationToken=default)
    {
        var id=Interlocked.Increment(ref next);if(observer is not null)await observer.BeforeRegistrationAsync(id,cancellationToken);
        lock(sync)if(terminal is not null){if(mutation=="admit-after-end")return new();throw terminal;}
        var reply=new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);lock(sync)replies[id]=reply;
        using var registration=cancellationToken.Register(()=>{lock(sync){replies.Remove(id);cancelled.Add(id);reply.TrySetCanceled(cancellationToken);if(mutation=="cancel-other"){foreach(var pending in replies.Values)pending.TrySetCanceled();replies.Clear();}}});
        await wire.SendAsync(JsonSerializer.SerializeToUtf8Bytes(CdpCases.Obj(("id",id),("method",method),("params",parameters??new JsonObject()),("sessionId",sessionId))));return await reply.Task;
    }
    private async Task PumpAsync()
    {
        try
        {
            while(true)
            {
                var bytes=await wire.ReceiveAsync(lifetime.Token);if(bytes is null)throw new CdpDisconnectedException("fixture closed");var envelope=JsonNode.Parse(bytes.Value.Span)!;var id=envelope["id"]!.GetValue<int>();
                lock(sync)
                {
                    if(cancelled.Remove(id)){if(envelope["sessionId"]!.GetValue<string>()!="page-session"||mutation=="reject-late")throw new CdpProtocolException("fixture late reply context rejected");continue;}
                    if(!replies.Remove(id,out var reply))throw new CdpProtocolException("fixture unknown identifier");
                    reply.TrySetResult(CdpCases.Obj(("value",id==2?222:333)));
                }
            }
        }
        catch(OperationCanceledException)when(lifetime.IsCancellationRequested){}
        catch(Exception error){lock(sync){terminal=error;foreach(var reply in replies.Values)reply.TrySetException(error);replies.Clear();}observer?.Terminal(error);}
    }
    public async IAsyncEnumerable<CdpEvent> EventsAsync([EnumeratorCancellation]CancellationToken cancellationToken=default){await Task.CompletedTask;yield break;}
    public async ValueTask DisposeAsync(){lifetime.Cancel();await wire.DisposeAsync();await pump;lifetime.Dispose();}
}
[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-005")]
public sealed class CdpLifecycleMockTests
{
    [Fact]
    public async Task CorrectTerminalRaceAndWrongPostTerminalAdmissionAreDiscriminated()
    {
        await CdpLifecycleCases.TerminalBetweenAdmissionAndRegistration((wire,observer)=>new LifecycleCdpMock(wire,observer));
        await Assert.ThrowsAnyAsync<Exception>(()=>CdpLifecycleCases.TerminalBetweenAdmissionAndRegistration((wire,observer)=>new LifecycleCdpMock(wire,observer,"admit-after-end")));
    }
    [Theory,InlineData(false),InlineData(true)]
    public async Task CorrectCancellationAndLateContextPoliciesAndIndependentControls(bool wrongSession)
    {
        await CdpLifecycleCases.CancellationAndLateReply((wire,observer)=>new LifecycleCdpMock(wire,observer),wrongSession);
        await Assert.ThrowsAnyAsync<Exception>(()=>CdpLifecycleCases.CancellationAndLateReply((wire,observer)=>new LifecycleCdpMock(wire,observer,"count"),wrongSession));
        await Assert.ThrowsAnyAsync<Exception>(()=>CdpLifecycleCases.CancellationAndLateReply((wire,observer)=>new LifecycleCdpMock(wire,observer,"cancel-other"),wrongSession));
        if(!wrongSession)await Assert.ThrowsAnyAsync<Exception>(()=>CdpLifecycleCases.CancellationAndLateReply((wire,observer)=>new LifecycleCdpMock(wire,observer,"reject-late"),false));
    }
}
