using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation.Browser;

public interface ICdpMessageTransport : IAsyncDisposable
{
    Task SendAsync(ReadOnlyMemory<byte> message,CancellationToken cancellationToken=default);
    Task<ReadOnlyMemory<byte>?> ReceiveAsync(CancellationToken cancellationToken=default);
}
public sealed record CdpEvent(string Method,JsonObject Parameters,string? SessionId,JsonObject Envelope);
public interface ICdpClient : IAsyncDisposable
{
    Task<JsonObject> CallAsync(string method,JsonObject? parameters=null,string? sessionId=null,CancellationToken cancellationToken=default);
    IAsyncEnumerable<CdpEvent> EventsAsync(CancellationToken cancellationToken=default);
}
public sealed class CdpCommandException(string method,int code,string message,JsonObject envelope):IOException(message)
{
    public string Method {get;}=method;
    public int Code {get;}=code;
    public JsonObject Envelope {get;}=envelope;
}
public sealed class CdpProtocolException(string message,Exception? inner=null):IOException(message,inner);
public sealed class CdpDisconnectedException(string message):IOException(message);
public sealed record BrowserQaRequest(string ProjectRoot,string OutputRoot,ToolIdentity Browser,
    string RendererProfile="native",int ViewportWidth=1440,int ViewportHeight=960,int ExpectedArchitectureDiagrams=21);
public sealed record BrowserQaResult(StoredArtifact Receipt,bool Passed,int Slides,int Images,int SvgFiles,
    IReadOnlyList<StoredArtifact> Captures,IReadOnlyList<string> Failures);
public interface IBrowserQaOperation
{
    Task<BrowserQaResult> RunAsync(BrowserQaRequest request,CancellationToken cancellationToken=default);
}
