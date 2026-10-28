using System.Net.WebSockets;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation.Browser;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Browser;

internal sealed record BrowserShutdownCase(string Id,Exception Error,bool Acknowledged,int ExitCode,bool Cancelled,bool Expected);
internal static class BrowserShutdownCases
{
    internal static readonly IReadOnlyList<BrowserShutdownCase> All=[
        new("CLOSE-ack-normal-eof",new WebSocketException(WebSocketError.ConnectionClosedPrematurely),true,0,false,true),
        new("CLOSE-before-ack",new WebSocketException(WebSocketError.ConnectionClosedPrematurely),false,0,false,false),
        new("CLOSE-process-failed",new WebSocketException(WebSocketError.ConnectionClosedPrematurely),true,1,false,false),
        new("CLOSE-process-cancelled",new WebSocketException(WebSocketError.ConnectionClosedPrematurely),true,0,true,false),
        new("CLOSE-other-websocket",new WebSocketException(WebSocketError.InvalidState),true,0,false,false),
        new("CLOSE-malformed-protocol",new CdpProtocolException("fixture malformed fragment"),true,0,false,false),
        new("CLOSE-unknown-io",new IOException("fixture earlier IO error"),true,0,false,false)];
    public static IEnumerable<object[]> Cases=>All.Select(x=>new object[]{x.Id});
    internal static BrowserShutdownCase Get(string id)=>All.Single(x=>x.Id==id);
    internal static void AssertContract(BrowserShutdownCase test,Func<Exception,bool,ProcessResult,bool> policy)
    {
        var exit=new ProcessResult(test.ExitCode,"fixture/stdout.bin","fixture/stderr.bin",DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch,test.Cancelled);
        Assert.Equal(test.Expected,policy(test.Error,test.Acknowledged,exit));
    }
}
internal sealed class ScriptedBrowserShutdownPolicy(string? mutation=null)
{
    // One independently specified accepted wire/process fixture. Every other
    // tuple is an explicit rejection, including prior faults and failed exits.
    private static readonly HashSet<(Type,WebSocketError?,bool,int,bool)> Accepted=[(typeof(WebSocketException),WebSocketError.ConnectionClosedPrematurely,true,0,false)];
    internal bool Evaluate(Exception error,bool acknowledged,ProcessResult exit)
    {
        var allowed=Accepted.Contains((error.GetType(),(error as WebSocketException)?.WebSocketErrorCode,acknowledged,exit.ExitCode,exit.Cancelled));
        return mutation=="flip"?!allowed:allowed;
    }
}
[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-005")]
public sealed class BrowserShutdownMockTests
{
    public static IEnumerable<object[]> Cases=>BrowserShutdownCases.Cases;
    [Theory,MemberData(nameof(Cases))]
    public void OnlyAcknowledgedCloseAndNormalOwnedExitAdmitExactEof(string id)
    {
        var test=BrowserShutdownCases.Get(id);BrowserShutdownCases.AssertContract(test,new ScriptedBrowserShutdownPolicy().Evaluate);
        Assert.NotNull(Record.Exception(()=>BrowserShutdownCases.AssertContract(test,new ScriptedBrowserShutdownPolicy("flip").Evaluate)));
    }
}
