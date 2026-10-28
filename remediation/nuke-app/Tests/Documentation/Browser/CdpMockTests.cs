using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Documentation.Browser;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Browser;

internal sealed class ScriptedCdpClient(CdpCase test,ScriptedCdpWire wire,string? mutation=null):ICdpClient
{
    private int callCount;private Task? drain;
    private async Task DrainAsync()
    {for(var i=0;i<test.Frames.Count;i++)await wire.ReceiveAsync();}
    public async Task<JsonObject> CallAsync(string method,JsonObject? parameters=null,string? sessionId=null,CancellationToken cancellationToken=default)
    {
        var index=callCount++;var expected=test.Calls[index];Assert.Equal(expected.Method,method);Assert.Equal(expected.SessionId,sessionId);Assert.True(JsonNode.DeepEquals(expected.Parameters,parameters));
        var outgoing=CdpCases.Obj(("id",mutation=="id"?99:index+1),("method",mutation=="method"?"Wrong.method":method),("params",mutation=="params"?new JsonObject():parameters));
        if(sessionId is not null)outgoing["sessionId"]=mutation=="session"?"other":sessionId;
        await wire.SendAsync(JsonSerializer.SerializeToUtf8Bytes(outgoing),cancellationToken);drain??=DrainAsync();await drain;
        if(test.Error is not null && mutation!="error")
        {
            if(test.Error==typeof(CdpCommandException))throw new CdpCommandException(method,mutation=="code"?999:test.Code!.Value,"fixture command failed",JsonNode.Parse(test.Frames[0]!)!.AsObject());
            if(test.Error==typeof(CdpDisconnectedException))throw new CdpDisconnectedException("fixture closed");
            if(test.Error==typeof(CdpProtocolException))throw new CdpProtocolException("fixture invalid CDP input");
        }
        return mutation is "result" or "error"?new JsonObject():(JsonObject)expected.Result.DeepClone();
    }
    public async IAsyncEnumerable<CdpEvent> EventsAsync([EnumeratorCancellation]CancellationToken cancellationToken=default)
    {
        await Task.CompletedTask;if(test.Id!="CDP-out-of-order"||mutation=="event")yield break;var envelope=JsonNode.Parse(test.Frames[0]!)!.AsObject();
        yield return new("Page.lifecycleEvent",(JsonObject)envelope["params"]!.DeepClone(),"page-session",envelope);
    }
    public ValueTask DisposeAsync()=>wire.DisposeAsync();
}
[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-005")]
public sealed class CdpMockTests
{
    [Theory,MemberData(nameof(CdpCases.Cases),MemberType=typeof(CdpCases))]
    public async Task CorrectProtocolMocksProveCorrelationContextErrorsAndLosslessData(string id)
    {var test=CdpCases.Get(id);await using var wire=new ScriptedCdpWire(test);await using var client=new ScriptedCdpClient(test,wire);await CdpCases.AssertContract(test,client,wire);}
    [Theory,MemberData(nameof(CdpCases.Cases),MemberType=typeof(CdpCases))]
    public async Task IndependentWrongEnvelopesResultsAndErrorsAreRejected(string id)
    {
        var test=CdpCases.Get(id);
        var mutations=test.Error is null?new[]{"id","method","params","session","result"}:test.SendFailure?new[]{"id","method","params","session"}:new[]{"id","method","params","session","error"};
        if(test.Id=="CDP-out-of-order")mutations=mutations.Append("event").ToArray();if(test.Error==typeof(CdpCommandException))mutations=mutations.Append("code").ToArray();
        foreach(var mutation in mutations)
        {
            // The large fixture has no session, and an empty-parameter command
            // cannot distinguish an empty params replacement.
            if(mutation=="session"&&test.Calls.All(x=>x.SessionId is null))continue;
            await using var wire=new ScriptedCdpWire(test);await using var client=new ScriptedCdpClient(test,wire,mutation);
            Assert.NotNull(await Record.ExceptionAsync(()=>CdpCases.AssertContract(test,client,wire)));
        }
    }
}
