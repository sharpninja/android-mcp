using System.Net;
using System.Text;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Native"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationHttpTransportRealTests
{
    [Theory,MemberData(nameof(DocumentationHttpCases.Cases),MemberType=typeof(DocumentationHttpCases))]
    public Task SameCasesUseTheSoleHttpClientTransport(string id)=>DocumentationHttpCases.AssertContract(id,Probe);

    private static async Task<DocumentationHttpObservation> Probe(string id)
    {
        var caseId=id;id=DocumentationHttpCases.Scenario(caseId);var budget=DocumentationHttpCases.Budget(caseId);
        var time=new ManualTime();using var caller=new CancellationTokenSource();var agents=new List<string>();var sends=0;
        void Effect(string phase,CancellationToken token)
        {
            if(id=="PER-READ")time.Advance(TimeSpan.FromSeconds(phase=="SEND"?budget.TotalSeconds-10:phase=="READ"?budget.TotalSeconds-5:0));
            if(id=="TIMEOUT-"+phase){time.Advance(budget);token.ThrowIfCancellationRequested();throw new InvalidOperationException("Unbounded operation.");}
            if(id=="CANCEL-"+phase){caller.Cancel();token.ThrowIfCancellationRequested();throw new InvalidOperationException("Caller cancellation ignored.");}
        }
        using var handler=new Handler((request,token)=>{
            sends++;agents.Add(request.Headers.UserAgent.ToString());Effect("SEND",token);
            return new HttpResponseMessage(id=="NO-RETRY"?HttpStatusCode.TooManyRequests:HttpStatusCode.OK){RequestMessage=request,Content=new Content(id,Effect)};
        });
        using var client=new HttpClient(handler){Timeout=Timeout.InfiniteTimeSpan};
        if(id.EndsWith("CUSTOM",StringComparison.Ordinal))client.DefaultRequestHeaders.UserAgent.ParseAdd("Custom-Agent");
        string? error=null;var body="";
        try{
            var profile=caseId.StartsWith("FETCH-",StringComparison.Ordinal)?HttpByteTransportProfile.SourceSnapshotFetch:caseId.StartsWith("RESTORE-",StringComparison.Ordinal)?HttpByteTransportProfile.SourceSnapshotRestore:
                id=="UNSUPPORTED"?(HttpByteTransportProfile)999:id.StartsWith("DEFAULT",StringComparison.Ordinal)?HttpByteTransportProfile.Default:HttpByteTransportProfile.DocumentationBootstrap;
            var transport=new HttpByteTransport(client,null,null,profile,time);
            var response=await transport.SendAsync(new(new("https://unit.test/artifact"),"fixture",ContentHash.Sha256("abc"u8),3,3),caller.Token);
            body=Encoding.UTF8.GetString(response.Bytes.Span);
        }
        catch(TransportException exception){error=exception.Code;}
        catch(OperationCanceledException){error="cancelled";}
        catch(ArgumentOutOfRangeException){error="profile";}
        return new(error,caller.IsCancellationRequested,body,agents,sends,time.Budgets,time.Active,time.Elapsed);
    }

    private sealed class Handler(Func<HttpRequestMessage,CancellationToken,HttpResponseMessage> send):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Task.FromResult(send(request,token));}
    private sealed class Content(string id,Action<string,CancellationToken> effect):HttpContent
    {
        protected override bool TryComputeLength(out long length){length=3;return true;}
        protected override Task SerializeToStreamAsync(Stream stream,TransportContext? context)=>throw new NotSupportedException();
        protected override Task<Stream> CreateContentReadStreamAsync()=>CreateContentReadStreamAsync(CancellationToken.None);
        protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken token){effect("STREAM",token);return Task.FromResult<Stream>(new Body(id,effect));}
    }
    private sealed class Body(string id,Action<string,CancellationToken> effect):MemoryStream(Encoding.UTF8.GetBytes(id=="HASH"?"xyz":id=="BOUND"?"abcd":"abc"))
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token=default)
        {effect("READ",token);return base.ReadAsync(id=="PER-READ"?buffer[..1]:buffer,token);}
    }
    private sealed class ManualTime:TimeProvider
    {
        private readonly List<Timer> timers=[];
        public List<TimeSpan> Budgets {get;}=[];
        public TimeSpan Elapsed {get;private set;}
        public int Active=>timers.Count(timer=>!timer.Disposed);
        public override DateTimeOffset GetUtcNow()=>DateTimeOffset.UnixEpoch+Elapsed;
        public override long GetTimestamp()=>Elapsed.Ticks;
        public override long TimestampFrequency=>TimeSpan.TicksPerSecond;
        public override ITimer CreateTimer(TimerCallback callback,object? state,TimeSpan dueTime,TimeSpan period)
        {Budgets.Add(dueTime);var timer=new Timer(this,callback,state,Elapsed+dueTime);timers.Add(timer);return timer;}
        public void Advance(TimeSpan interval)
        {
            Elapsed+=interval;
            foreach(var timer in timers.ToArray())if(!timer.Disposed&&timer.Due<=Elapsed){timer.Due=TimeSpan.MaxValue;timer.Callback(timer.State);}
        }
        private sealed class Timer(ManualTime owner,TimerCallback callback,object? state,TimeSpan due):ITimer
        {
            public TimerCallback Callback {get;}=callback;public object? State {get;}=state;
            public TimeSpan Due {get;set;}=due;public bool Disposed {get;private set;}
            public bool Change(TimeSpan dueTime,TimeSpan newPeriod){if(Disposed)return false;Due=dueTime==Timeout.InfiniteTimeSpan?TimeSpan.MaxValue:owner.Elapsed+dueTime;return true;}
            public void Dispose()=>Disposed=true;
            public ValueTask DisposeAsync(){Dispose();return ValueTask.CompletedTask;}
        }
    }
}
