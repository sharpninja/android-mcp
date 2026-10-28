using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using AndroidMcp.BuildTool.Documentation.Browser;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Browser;

public sealed record CdpCallCase(string Method,JsonObject Parameters,string? SessionId,JsonObject Result);
public sealed record CdpCase(string Id,IReadOnlyList<CdpCallCase> Calls,IReadOnlyList<byte[]?> Frames,Type? Error=null,int? Code=null,bool SendFailure=false);
public static class CdpCases
{
    public static JsonObject Obj(params (string Key,object? Value)[] values)
    {var result=new JsonObject();foreach(var pair in values)result[pair.Key]=pair.Value is JsonNode node?node.DeepClone():JsonSerializer.SerializeToNode(pair.Value);return result;}
    private static byte[] Bytes(JsonObject value)=>JsonSerializer.SerializeToUtf8Bytes(value);
    public static readonly IReadOnlyList<CdpCase> All=Create();
    public static IEnumerable<object[]> Cases=>All.Select(x=>new object[]{x.Id});
    public static CdpCase Get(string id)=>All.Single(x=>x.Id==id);
    private static IReadOnlyList<CdpCase> Create()
    {
        var first=new CdpCallCase("Runtime.evaluate",Obj(("expression","literal 雪"),("returnByValue",true)),"page-session",Obj(("value","first"),("unknown",Obj(("retained",true)))));
        var second=new CdpCallCase("Page.getLayoutMetrics",new(),"page-session",Obj(("value","second")));
        byte[] Reply(int id,JsonObject body,string? session="page-session")=>Bytes(Obj(("id",id),("result",body),("sessionId",session)));
        var eventFrame=Bytes(Obj(("method","Page.lifecycleEvent"),("params",Obj(("name","load"),("loaderId","fixture-loader"))),("sessionId","page-session"),("unknown",17)));
        var error=Bytes(Obj(("id",1),("sessionId","page-session"),("error",Obj(("code",-32000),("message","fixture command failed"),("data",Obj(("retained","雪")))))));
        var huge=new CdpCallCase("Runtime.evaluate",Obj(("expression","large fixture")),null,Obj(("value",new string('x',1048583)+"雪\0")));
        return [new("CDP-out-of-order",[first,second],[eventFrame,Reply(2,second.Result),Reply(1,first.Result)]),
            new("CDP-error",[first],[error],typeof(CdpCommandException),-32000),new("CDP-malformed",[first],["{invalid fixture"u8.ToArray()],typeof(CdpProtocolException)),
            new("CDP-wrong-session",[first],[Reply(1,first.Result,"other-session")],typeof(CdpProtocolException)),new("CDP-unknown-id",[first],[Reply(99,first.Result)],typeof(CdpProtocolException)),
            new("CDP-duplicate-id",[first],["{\"id\":1,\"id\":2,\"result\":{}}"u8.ToArray()],typeof(CdpProtocolException)),new("CDP-disconnect",[first],[null],typeof(CdpDisconnectedException)),
            new("CDP-send-failure",[first],[],typeof(IOException),SendFailure:true),new("CDP-large",[huge],[Bytes(Obj(("id",1),("result",huge.Result)))])];
    }
    public static async Task AssertContract(CdpCase test,ICdpClient client,ScriptedCdpWire wire)
    {
        var calls=test.Calls.Select(call=>client.CallAsync(call.Method,(JsonObject)call.Parameters.DeepClone(),call.SessionId)).ToArray();
        if(test.Error is not null)
        {
            foreach(var call in calls)
            {
                var error=await Record.ExceptionAsync(()=>call);Assert.NotNull(error);Assert.Equal(test.Error,error!.GetType());
                if(error is CdpCommandException command)
                {Assert.Equal(test.Calls[0].Method,command.Method);Assert.Equal(test.Code,command.Code);Assert.Equal("fixture command failed",command.Message);Assert.True(JsonNode.DeepEquals(JsonNode.Parse(test.Frames[0]!),command.Envelope));}
            }
        }
        else
        {
            var results=await Task.WhenAll(calls);for(var i=0;i<results.Length;i++)Assert.True(JsonNode.DeepEquals(test.Calls[i].Result,results[i]));
            if(test.Id=="CDP-out-of-order")
            {
                await using var events=client.EventsAsync().GetAsyncEnumerator();Assert.True(await events.MoveNextAsync());var item=events.Current;
                Assert.Equal("Page.lifecycleEvent",item.Method);Assert.Equal("page-session",item.SessionId);Assert.True(JsonNode.DeepEquals(JsonNode.Parse(test.Frames[0]!),item.Envelope));
                Assert.True(JsonNode.DeepEquals(item.Envelope["params"],item.Parameters));
            }
        }
        Assert.Equal(test.Calls.Count,wire.Sent.Count);
        for(var i=0;i<test.Calls.Count;i++)
        {
            var call=test.Calls[i];var expected=Obj(("id",i+1),("method",call.Method),("params",call.Parameters));if(call.SessionId is not null)expected["sessionId"]=call.SessionId;
            Assert.True(JsonNode.DeepEquals(expected,JsonNode.Parse(wire.Sent[i])));
        }
        if(!test.SendFailure)Assert.Equal(test.Frames.Count,wire.Received.Count);
    }
}

public sealed class ScriptedCdpWire(CdpCase test):ICdpMessageTransport
{
    private readonly Channel<byte[]?> incoming=Channel.CreateUnbounded<byte[]?>();
    public List<byte[]> Sent {get;}=[];public List<byte[]?> Received {get;}=[];
    public Task SendAsync(ReadOnlyMemory<byte> message,CancellationToken cancellationToken=default)
    {
        lock(Sent){Sent.Add(message.ToArray());if(test.SendFailure)throw new IOException("fixture send failed");if(Sent.Count==test.Calls.Count)foreach(var frame in test.Frames)incoming.Writer.TryWrite(frame);}
        return Task.CompletedTask;
    }
    public async Task<ReadOnlyMemory<byte>?> ReceiveAsync(CancellationToken cancellationToken=default)
    {var bytes=await incoming.Reader.ReadAsync(cancellationToken);lock(Received)Received.Add(bytes);return bytes is null?null:new ReadOnlyMemory<byte>(bytes);}
    public ValueTask DisposeAsync(){incoming.Writer.TryComplete();return ValueTask.CompletedTask;}
}
