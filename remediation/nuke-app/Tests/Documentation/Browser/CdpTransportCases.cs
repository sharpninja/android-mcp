using System.Net.WebSockets;
using System.Text;
using AndroidMcp.BuildTool.Documentation.Browser;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Browser;

internal sealed record CdpSocketFrame(WebSocketMessageType Type,byte[] Bytes,bool End);
internal sealed record CdpFramingCase(string Id,IReadOnlyList<CdpSocketFrame> Frames,byte[]? Expected,Type? Error=null);
internal static class CdpTransportCases
{
    internal static readonly IReadOnlyList<CdpFramingCase> All=Create();
    public static IEnumerable<object[]> Cases=>All.Select(x=>new object[]{x.Id});
    internal static CdpFramingCase Get(string id)=>All.Single(x=>x.Id==id);
    private static IReadOnlyList<CdpFramingCase> Create()
    {
        var unicode=Encoding.UTF8.GetBytes("\"雪\"");var large=Enumerable.Range(0,1048583).Select(x=>(byte)(x%256)).ToArray();
        return [new("FRAME-clean-close",[new(WebSocketMessageType.Close,[],true)],null),new("FRAME-empty-text",[new(WebSocketMessageType.Text,[],true)],[]),
            new("FRAME-fragments",[new(WebSocketMessageType.Text,"{"u8.ToArray(),false),new(WebSocketMessageType.Text,"}"u8.ToArray(),true)],"{}"u8.ToArray()),
            new("FRAME-close-mid-fragment",[new(WebSocketMessageType.Text,"{"u8.ToArray(),false),new(WebSocketMessageType.Close,[],true)],null,typeof(CdpProtocolException)),
            new("FRAME-binary",[new(WebSocketMessageType.Binary,[1],true)],null,typeof(CdpProtocolException)),
            new("FRAME-unicode-split",[new(WebSocketMessageType.Text,unicode[..2],false),new(WebSocketMessageType.Text,unicode[2..],true)],unicode),
            new("FRAME-large",[new(WebSocketMessageType.Text,large,true)],large)];
    }
    internal static async Task AssertContract(CdpFramingCase test,ICdpMessageTransport transport)
    {
        if(test.Error is not null){var error=await Record.ExceptionAsync(()=>transport.ReceiveAsync());Assert.NotNull(error);Assert.Equal(test.Error,error!.GetType());return;}
        var result=await transport.ReceiveAsync();if(test.Expected is null)Assert.False(result.HasValue);else{Assert.True(result.HasValue);Assert.Equal(test.Expected,result.Value.ToArray());}
    }
}
internal sealed class FixtureMessageTransport(CdpFramingCase test,string? mutation=null):ICdpMessageTransport
{
    public Task SendAsync(ReadOnlyMemory<byte> message,CancellationToken cancellationToken=default)=>Task.CompletedTask;
    public Task<ReadOnlyMemory<byte>?> ReceiveAsync(CancellationToken cancellationToken=default)
    {
        if(test.Error is not null){if(mutation=="error")throw new IOException("wrong error");throw new CdpProtocolException("fixture invalid framing");}
        ReadOnlyMemory<byte>? data=test.Expected is null?(ReadOnlyMemory<byte>?)null:new ReadOnlyMemory<byte>(test.Expected);
        if(mutation=="flip-null")data=test.Expected is null?new ReadOnlyMemory<byte>([]):(ReadOnlyMemory<byte>?)null;
        if(mutation=="truncate"&&test.Expected is {Length:>0})data=new ReadOnlyMemory<byte>(test.Expected[..^1]);
        return Task.FromResult(data);
    }
    public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
}
internal sealed class FixtureWebSocket(IReadOnlyList<CdpSocketFrame> frames):WebSocket
{
    private int frameIndex,offset;private WebSocketState state=WebSocketState.Open;
    public override WebSocketCloseStatus? CloseStatus=>state==WebSocketState.CloseReceived?WebSocketCloseStatus.NormalClosure:null;public override string? CloseStatusDescription=>null;
    public override string? SubProtocol=>null;public override WebSocketState State=>state;
    public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer,CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();var frame=frames[frameIndex];var count=Math.Min(buffer.Count,frame.Bytes.Length-offset);frame.Bytes.AsSpan(offset,count).CopyTo(buffer.AsSpan());offset+=count;
        var consumed=offset==frame.Bytes.Length;if(consumed){frameIndex++;offset=0;}if(frame.Type==WebSocketMessageType.Close)state=WebSocketState.CloseReceived;
        return Task.FromResult(new WebSocketReceiveResult(count,frame.Type,consumed&&frame.End));
    }
    public override Task SendAsync(ArraySegment<byte> buffer,WebSocketMessageType messageType,bool endOfMessage,CancellationToken cancellationToken)=>Task.CompletedTask;
    public override Task CloseAsync(WebSocketCloseStatus closeStatus,string? statusDescription,CancellationToken cancellationToken){state=WebSocketState.Closed;return Task.CompletedTask;}
    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus,string? statusDescription,CancellationToken cancellationToken){state=WebSocketState.CloseSent;return Task.CompletedTask;}
    public override void Abort()=>state=WebSocketState.Aborted;public override void Dispose()=>state=WebSocketState.Closed;
}
