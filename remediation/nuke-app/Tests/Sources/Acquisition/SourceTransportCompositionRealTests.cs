using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed class NativeSourceTransportComposition:ISourceTransportCompositionProbe
{
    public async Task<SourceTransportCompositionObservation> RunAsync(SourceTransportCompositionCase c)
    {
        using var cancel=new CancellationTokenSource();if(c.Outcome=="cancelled")cancel.Cancel();
        var request=new PinnedRequest(new("https://fixture.invalid/exact?p=1"),"revision","hash",3,5,new(7,3),"tag","tag",Retry:TransportRetryPolicy.CanonicalRestore,ExpectedTotalLength:100,PartialMemberSha256:"member");
        var response=new TransportResponse(request.Url,206,new byte[]{1,2,3},"tag","bytes 7-9/100",DeclaredLength:3);
        Exception? failure=c.Outcome=="cancelled"?new OperationCanceledException(cancel.Token):c.Outcome=="failure"?new IOException("original transport error"):null;
        var inner=new RecordingTransport(response,failure);var profile=HttpByteTransportProfile.DocumentationBootstrap;var calls=0;
        var transport=SnapshotFetchTransport.CreateForCaller(c.Mode,p=>{profile=p;calls++;return inner;});
        TransportResponse? actual=null;Exception? error=null;
        try{actual=await transport.SendAsync(request,cancel.Token);}catch(Exception e){error=e;}
        var sent=inner.Request;var expectedRetry=c.Mode==SnapshotCallerMode.Fetch?SnapshotFetchTransport.Retry:request.Retry;
        return new(profile,calls,inner.Sends,sent is not null&&sent with{Retry=request.Retry}==request,
            inner.Token==cancel.Token,failure is not null?actual is null:ReferenceEquals(response,actual),
            failure is null?error is null:ReferenceEquals(failure,error),sent?.Retry==expectedRetry);
    }
    private sealed class RecordingTransport(TransportResponse response,Exception? error):IByteTransport
    {
        public PinnedRequest? Request{get;private set;}public CancellationToken Token{get;private set;}public int Sends{get;private set;}
        public Task<TransportResponse> SendAsync(PinnedRequest request,CancellationToken cancellationToken=default)
        {Request=request;Token=cancellationToken;Sends++;return error is null?Task.FromResult(response):Task.FromException<TransportResponse>(error);}
    }
}
[Trait("Gate","Real")]
public sealed class SourceTransportCompositionRealTests
{
    [Theory,MemberData(nameof(SourceTransportCompositionCases.All),MemberType=typeof(SourceTransportCompositionCases))]
    public Task SameCallerCasesUseSharedTransportAdapter(SourceTransportCompositionCase c)=>SourceTransportCompositionCases.AssertCase(c,new NativeSourceTransportComposition());
}
