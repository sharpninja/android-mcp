using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation.Browser;

public sealed class CdpClient:ICdpClient,ICdpClientDiagnostics
{
    private sealed record Pending(string Method,string? SessionId,TaskCompletionSource<JsonObject> Reply);
    private readonly ICdpMessageTransport wire;private readonly IArtifactStore artifacts;private readonly IClock clock;
    private readonly object state=new();private readonly Dictionary<int,Pending> pending=[];private readonly Dictionary<int,Pending> cancelled=[];private readonly ICdpLifecycleObserver? observer;
    private readonly Channel<CdpEvent> events=Channel.CreateUnbounded<CdpEvent>();
    private readonly SemaphoreSlim sends=new(1,1);private readonly CancellationTokenSource lifetime=new();private readonly Task reader;
    private readonly ConcurrentQueue<StoredArtifact> transcript=new();private int nextId;private long sequence;private Exception? terminal;private int disposed;
    public IReadOnlyList<StoredArtifact> Transcript=>transcript.ToArray();
    int ICdpClientDiagnostics.PendingCommands{get{lock(state)return pending.Count;}}
    public CdpClient(ICdpMessageTransport wire,IArtifactStore artifacts,IClock clock)
        :this(wire,artifacts,clock,null){}
    internal CdpClient(ICdpMessageTransport wire,IArtifactStore artifacts,IClock clock,ICdpLifecycleObserver? observer)
    {this.wire=wire;this.artifacts=artifacts;this.clock=clock;this.observer=observer;reader=ReadLoopAsync();}
    public async Task<JsonObject> CallAsync(string method,JsonObject? parameters=null,string? sessionId=null,CancellationToken cancellationToken=default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed)!=0,this);
        if(string.IsNullOrWhiteSpace(method))throw new ArgumentException("CDP method is required.",nameof(method));
        cancellationToken.ThrowIfCancellationRequested();
        lock(state)if(terminal is { } stopped)throw stopped;
        var id=Interlocked.Increment(ref nextId);if(id<=0)throw new InvalidOperationException("CDP integer request identifiers exhausted.");
        if(observer is not null)await observer.BeforeRegistrationAsync(id,cancellationToken);
        var reply=new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);var item=new Pending(method,sessionId,reply);
        lock(state){if(terminal is { } ended)throw ended;ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed)!=0,this);pending.Add(id,item);}
        using var registration=cancellationToken.Register(()=>
        {lock(state)if(pending.Remove(id,out var active)){cancelled.Add(id,active);active.Reply.TrySetCanceled(cancellationToken);}});
        var envelope=new JsonObject{["id"]=id,["method"]=method,["params"]=parameters?.DeepClone()??new JsonObject()};if(sessionId is not null)envelope["sessionId"]=sessionId;
        try
        {
            await sends.WaitAsync(cancellationToken);
            try
            {
                bool admitted;lock(state)admitted=pending.ContainsKey(id)&&terminal is null;
                if(admitted){var bytes=JsonSerializer.SerializeToUtf8Bytes(envelope);await RecordAsync("sent",bytes,cancellationToken);await wire.SendAsync(bytes,cancellationToken);}
            }
            finally{sends.Release();}
        }
        catch(Exception error){lock(state){pending.Remove(id);cancelled.Remove(id);reply.TrySetException(error);}}
        return await reply.Task;
    }
    private async Task RecordAsync(string direction,ReadOnlyMemory<byte> bytes,CancellationToken token)
    {
        var index=Interlocked.Increment(ref sequence);
        var record=new JsonObject{["sequence"]=index,["direction"]=direction,["at"]=clock.Now.ToString("o"),["bytes"]=bytes.Length,["sha256"]=ContentHash.Sha256(bytes.Span),["body_base64"]=Convert.ToBase64String(bytes.Span)};
        var artifact=await artifacts.WriteAsync(new("protocol/"+index.ToString("D12",System.Globalization.CultureInfo.InvariantCulture)+"-"+direction+".json",new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(record)),"application/json"),token);
        transcript.Enqueue(artifact);
    }
    private async Task ReadLoopAsync()
    {
        try
        {
            while(true)
            {
                var bytes=await wire.ReceiveAsync(lifetime.Token);
                if(bytes is null){await RecordAsync("closed",ReadOnlyMemory<byte>.Empty,lifetime.Token);throw new CdpDisconnectedException("CDP transport closed before further replies.");}
                await RecordAsync("received",bytes.Value,lifetime.Token);JsonObject envelope;
                try
                {
                    _=new CanonicalJson().Serialize(bytes.Value);envelope=JsonNode.Parse(bytes.Value.Span,documentOptions:new JsonDocumentOptions{MaxDepth=int.MaxValue})?.AsObject()??throw new JsonException("CDP envelope is absent.");
                }
                catch(Exception error)when(error is JsonException or CanonicalJsonException or InvalidOperationException)
                {throw new CdpProtocolException("Invalid or ambiguous CDP JSON envelope.",error);}
                if(envelope.ContainsKey("id"))
                {
                    if(envelope["id"] is not JsonValue value||!value.TryGetValue<int>(out var id)||id<=0)throw new CdpProtocolException("CDP response has an invalid request identifier.");
                    lock(state)
                    {
                        var late=cancelled.Remove(id,out var request);
                        if(!late&&!pending.Remove(id,out request))throw new CdpProtocolException("CDP response has an unknown request identifier.");
                        try
                        {
                            if(envelope["sessionId"]?.GetValue<string>()!=request!.SessionId)throw new CdpProtocolException("CDP response crossed its requested target session.");
                            if(envelope["error"] is JsonObject commandError)
                            {
                                var failure=new CdpCommandException(request.Method,commandError["code"]!.GetValue<int>(),commandError["message"]!.GetValue<string>(),(JsonObject)envelope.DeepClone());
                                if(!late)request.Reply.TrySetException(failure);
                            }
                            else if(envelope["result"] is JsonObject result){if(!late)request.Reply.TrySetResult((JsonObject)result.DeepClone());}
                            else throw new CdpProtocolException("CDP reply has neither an object result nor a command error.");
                        }
                        catch(Exception error)
                        {
                            var failure=error as CdpProtocolException??new CdpProtocolException("Malformed CDP response fields.",error);request!.Reply.TrySetException(failure);throw failure;
                        }
                    }
                }
                else
                {
                    var method=envelope["method"]?.GetValue<string>();if(string.IsNullOrWhiteSpace(method))throw new CdpProtocolException("CDP event method is absent.");
                    if(envelope["params"] is not null and not JsonObject)throw new CdpProtocolException("CDP event parameters must be an object.");
                    events.Writer.TryWrite(new(method,(JsonObject?)envelope["params"]?.DeepClone()??new(),envelope["sessionId"]?.GetValue<string>(),(JsonObject)envelope.DeepClone()));
                }
            }
        }
        catch(OperationCanceledException)when(lifetime.IsCancellationRequested){events.Writer.TryComplete();}
        catch(Exception error)
        {
            CompleteTerminal(error);
        }
    }
    public async IAsyncEnumerable<CdpEvent> EventsAsync([EnumeratorCancellation]CancellationToken cancellationToken=default)
    {await foreach(var item in events.Reader.ReadAllAsync(cancellationToken))yield return item;}
    private void CompleteTerminal(Exception error)
    {
        lock(state){if(terminal is not null)return;terminal=error;foreach(var request in pending.Values)request.Reply.TrySetException(error);pending.Clear();cancelled.Clear();events.Writer.TryComplete(error);}
        observer?.Terminal(error);
    }
    public async ValueTask DisposeAsync()
    {
        if(Interlocked.Exchange(ref disposed,1)!=0)return;CompleteTerminal(new ObjectDisposedException(nameof(CdpClient)));lifetime.Cancel();await wire.DisposeAsync();
        try{await reader;}catch(OperationCanceledException){}
        lifetime.Dispose();sends.Dispose();
    }
}
