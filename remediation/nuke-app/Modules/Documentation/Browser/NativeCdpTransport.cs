using System.Net;
using System.Net.WebSockets;

namespace AndroidMcp.BuildTool.Documentation.Browser;

public sealed class NativeCdpTransport:ICdpMessageTransport
{
    private readonly WebSocket socket;
    private readonly HttpMessageInvoker? http;
    internal NativeCdpTransport(WebSocket socket,HttpMessageInvoker? http=null){this.socket=socket;this.http=http;}
    public static void ValidateLoopback(Uri endpoint,string scheme)
    {
        if(!endpoint.IsAbsoluteUri||endpoint.Scheme!=scheme||!IPAddress.TryParse(endpoint.Host,out var address)||!IPAddress.IsLoopback(address)||endpoint.UserInfo.Length!=0||endpoint.Fragment.Length!=0||endpoint.Port<=0)
            throw new ArgumentException("CDP endpoints must be explicit credential-free loopback addresses.",nameof(endpoint));
    }
    public static async Task<NativeCdpTransport> ConnectAsync(Uri endpoint,CancellationToken cancellationToken=default)
    {
        ValidateLoopback(endpoint,"ws");var socket=new ClientWebSocket();var http=new HttpMessageInvoker(new SocketsHttpHandler{UseProxy=false,AllowAutoRedirect=false});
        try{await socket.ConnectAsync(endpoint,http,cancellationToken);return new(socket,http);}catch{socket.Dispose();http.Dispose();throw;}
    }
    public async Task SendAsync(ReadOnlyMemory<byte> message,CancellationToken cancellationToken=default)=>await socket.SendAsync(message,WebSocketMessageType.Text,true,cancellationToken);
    public async Task<ReadOnlyMemory<byte>?> ReceiveAsync(CancellationToken cancellationToken=default)
    {
        using var assembled=new MemoryStream();var buffer=new byte[81920];var started=false;
        while(true)
        {
            var frame=await socket.ReceiveAsync(buffer.AsMemory(),cancellationToken);
            if(frame.MessageType==WebSocketMessageType.Close)
            {if(started)throw new CdpProtocolException("CDP peer closed within a fragmented text message.");return (ReadOnlyMemory<byte>?)null;}
            if(frame.MessageType!=WebSocketMessageType.Text)throw new CdpProtocolException("CDP transport received an unexpected binary message.");
            started=true;assembled.Write(buffer,0,frame.Count);if(frame.EndOfMessage)return assembled.ToArray();
        }
    }
    public ValueTask DisposeAsync(){socket.Abort();socket.Dispose();http?.Dispose();return ValueTask.CompletedTask;}
}
