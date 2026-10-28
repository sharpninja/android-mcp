using System.Text;
using AndroidMcp.BuildTool.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;
public sealed record ToolTransferCase(string Id,PinnedRequest Request,byte[] Body,string? Error,int Calls,Uri? Redirect=null);
public interface IToolTransferProbe { Task<TransportResponse> SendAsync(ToolTransferCase test);int Calls{get;} }
public static class ToolTransferCases
{
    private const string Sha="ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
    private static PinnedRequest Small=>new(new Uri("https://example.test/tool.jar"),"1.0",Sha,3,3,ToolTransfer:new(Sha,3,3));
    public static IEnumerable<object[]> All=>new[]{
        new ToolTransferCase("small-tool",Small,[97,98,99],null,1),
        new ToolTransferCase("source-cap-retained",Small with{ExpectedLength=137393837,MaxBytes=137393837,ToolTransfer=null},[97,98,99],"REQUEST",0),
        new ToolTransferCase("large-tool-admitted-then-exact-size-checked",Small with{ExpectedLength=137393837,MaxBytes=137393837,ToolTransfer=new(Sha,137393837,137393837)},[97,98,99],"BODY_LENGTH",1),
        new ToolTransferCase("tool-pin-mismatch",Small with{ToolTransfer=new(new string('0',64),3,3)},[97,98,99],"TOOL_PIN",0),
        new ToolTransferCase("tool-pin-required",Small with{ExpectedSha256=null},[97,98,99],"TOOL_PIN",0),
        new ToolTransferCase("tool-range-forbidden",Small with{Range=new(0,3)},[97,98,99],"TOOL_PIN",0),
        new ToolTransferCase("tool-bound-explicit",Small with{MaxBytes=4},[97,98,99],"TOOL_PIN",0),
        new ToolTransferCase("tool-body-oversized",Small,[97,98,99,100],"BODY_LIMIT",1),
        new ToolTransferCase("tool-global-ceiling",Small with{ExpectedLength=513L*1024*1024,MaxBytes=513L*1024*1024,ToolTransfer=new(Sha,513L*1024*1024,513L*1024*1024)},[97,98,99],"TOOL_PIN",0),
        new ToolTransferCase("published-checksum",Small with{Url=new Uri("https://example.test/tool.jar.sha256"),ExpectedSha256=null,ExpectedLength=null,MaxBytes=4096,ToolTransfer=new(Sha,137393837,137393837,ToolTransferKind.PublishedChecksum)},Encoding.ASCII.GetBytes(Sha),null,1),
        new ToolTransferCase("checksum-bound",Small with{ExpectedSha256=null,ExpectedLength=null,MaxBytes=4097,ToolTransfer=new(Sha,3,3,ToolTransferKind.PublishedChecksum)},Encoding.ASCII.GetBytes(Sha),"TOOL_PIN",0),
        new ToolTransferCase("admitted-redirect",Small with{ToolTransfer=new(Sha,3,3,AllowedRedirects:[new("https://cdn.example.test/tool.jar")])},[97,98,99],null,2,new("https://cdn.example.test/tool.jar")),
        new ToolTransferCase("unadmitted-redirect",Small,[97,98,99],"REDIRECT",1,new("https://cdn.example.test/tool.jar")),
        new ToolTransferCase("unsafe-redirect-pin",Small with{ToolTransfer=new(Sha,3,3,AllowedRedirects:[new("http://cdn.example.test/tool.jar")])},[97,98,99],"TOOL_PIN",0),
        new ToolTransferCase("redirect-cycle",Small with{ToolTransfer=new(Sha,3,3,AllowedRedirects:[new("https://cdn.example.test/tool.jar")])},[97,98,99],"REDIRECT",2,new("https://cdn.example.test/tool.jar"))
    }.Select(x=>new object[]{x});
    public static async Task AssertCase(ToolTransferCase test,IToolTransferProbe provider)
    {
        if(test.Error is not null){var error=await Record.ExceptionAsync(()=>provider.SendAsync(test));Assert.IsType<TransportException>(error);Assert.Equal(test.Error,((TransportException)error!).Code);}
        else{var result=await provider.SendAsync(test);Assert.Equal(test.Body,result.Bytes.ToArray());Assert.Equal(test.Redirect??test.Request.Url,result.Url);Assert.Equal(200,result.StatusCode);Assert.Equal(test.Body.Length,result.DeclaredLength);Assert.Equal("identity",result.ContentEncoding);}
        Assert.Equal(test.Calls,provider.Calls);
    }
}
public sealed class ScriptedToolTransfer(string? mutation=null):IToolTransferProbe
{
    private ToolTransferCase? fixture;public int Calls=>(fixture?.Calls??0)+(mutation=="calls"?1:0);
    public Task<TransportResponse> SendAsync(ToolTransferCase test)
    {
        fixture=test;if(test.Error is not null&&mutation!="error")throw new TransportException(mutation=="code"?"wrong":test.Error,"Scripted typed tool transfer rejection");
        return Task.FromResult(new TransportResponse(mutation=="url"?new("https://wrong.test/"):test.Redirect??test.Request.Url,mutation=="status"?500:200,mutation=="bytes"?new byte[]{0}:test.Body,ContentEncoding:mutation=="encoding"?"gzip":"identity",DeclaredLength:mutation=="length"?-1:test.Body.Length));
    }
}
[Trait("Gate","Mock")]
public sealed class ToolTransferMockTests
{
    [Theory,MemberData(nameof(ToolTransferCases.All),MemberType=typeof(ToolTransferCases))]
    public Task CorrectToolTransferContract(ToolTransferCase test)=>ToolTransferCases.AssertCase(test,new ScriptedToolTransfer());
    [Theory,MemberData(nameof(ToolTransferCases.All),MemberType=typeof(ToolTransferCases))]
    public async Task EveryResultAndCallMutationRejected(ToolTransferCase test)
    {foreach(var mutation in test.Error is null?new[]{"url","status","bytes","encoding","length","calls"}:new[]{"error","code","calls"})Assert.NotNull(await Record.ExceptionAsync(()=>ToolTransferCases.AssertCase(test,new ScriptedToolTransfer(mutation))));}
}
