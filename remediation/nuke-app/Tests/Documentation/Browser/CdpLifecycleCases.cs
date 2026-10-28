using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using AndroidMcp.BuildTool.Documentation.Browser;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Browser;

internal sealed class CdpRegistrationFence:ICdpLifecycleObserver
{
    internal readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly TaskCompletionSource<Exception> Ended=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async ValueTask BeforeRegistrationAsync(int id,CancellationToken cancellationToken){if(id==1){Entered.TrySetResult();await Release.Task.WaitAsync(cancellationToken);}}
    public void Terminal(Exception error)=>Ended.TrySetResult(error);
}
internal sealed class ControlledCdpWire:ICdpMessageTransport
{
    private readonly Channel<ReadOnlyMemory<byte>?> incoming=Channel.CreateUnbounded<ReadOnlyMemory<byte>?>();private readonly Channel<bool> changed=Channel.CreateUnbounded<bool>();
    internal List<JsonObject> Sent {get;}=[];
    public Task SendAsync(ReadOnlyMemory<byte> message,CancellationToken cancellationToken=default){lock(Sent)Sent.Add(JsonNode.Parse(message.Span)!.AsObject());changed.Writer.TryWrite(true);return Task.CompletedTask;}
    public Task<ReadOnlyMemory<byte>?> ReceiveAsync(CancellationToken cancellationToken=default)=>incoming.Reader.ReadAsync(cancellationToken).AsTask();
    internal void Close()=>incoming.Writer.TryWrite((ReadOnlyMemory<byte>?)null);
    internal void Reply(int id,int value,string session="page-session")=>incoming.Writer.TryWrite(JsonSerializer.SerializeToUtf8Bytes(CdpCases.Obj(("id",id),("sessionId",session),("result",CdpCases.Obj(("value",value))))));
    internal async Task WaitSentAsync(int count){while(true){lock(Sent)if(Sent.Count>=count)return;await changed.Reader.ReadAsync();}}
    public ValueTask DisposeAsync(){incoming.Writer.TryComplete();changed.Writer.TryComplete();return ValueTask.CompletedTask;}
}
internal static class CdpLifecycleCases
{
    internal static async Task TerminalBetweenAdmissionAndRegistration(Func<ControlledCdpWire,ICdpLifecycleObserver?,ICdpClient> factory)
    {
        await using var wire=new ControlledCdpWire();var fence=new CdpRegistrationFence();await using var client=factory(wire,fence);
        var pending=client.CallAsync("Fixture.first",sessionId:"page-session");await fence.Entered.Task;wire.Close();
        Assert.IsType<CdpDisconnectedException>(await fence.Ended.Task);fence.Release.TrySetResult();
        await Assert.ThrowsAsync<CdpDisconnectedException>(()=>pending);Assert.Empty(wire.Sent);Assert.Equal(0,Assert.IsAssignableFrom<ICdpClientDiagnostics>(client).PendingCommands);
    }
    internal static async Task CancellationAndLateReply(Func<ControlledCdpWire,ICdpLifecycleObserver?,ICdpClient> factory,bool wrongSession)
    {
        await using var wire=new ControlledCdpWire();await using var client=factory(wire,null);using var cancellation=new CancellationTokenSource();
        var first=client.CallAsync("Fixture.first",sessionId:"page-session",cancellationToken:cancellation.Token);await wire.WaitSentAsync(1);
        var second=client.CallAsync("Fixture.second",sessionId:"page-session");await wire.WaitSentAsync(2);var diagnostics=Assert.IsAssignableFrom<ICdpClientDiagnostics>(client);Assert.Equal(2,diagnostics.PendingCommands);
        cancellation.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>first);Assert.Equal(1,diagnostics.PendingCommands);
        wire.Reply(1,111,wrongSession?"other-session":"page-session");
        if(wrongSession){await Assert.ThrowsAsync<CdpProtocolException>(()=>second);Assert.Equal(0,diagnostics.PendingCommands);return;}
        wire.Reply(2,222);Assert.Equal(222,(await second)["value"]!.GetValue<int>());Assert.Equal(0,diagnostics.PendingCommands);
        var third=client.CallAsync("Fixture.third",sessionId:"page-session");await wire.WaitSentAsync(3);wire.Reply(3,333);Assert.Equal(333,(await third)["value"]!.GetValue<int>());Assert.Equal(0,diagnostics.PendingCommands);
    }
}
