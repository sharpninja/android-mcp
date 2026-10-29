using System.Net;
using System.Text;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed record TransportCase(string Id, PinnedRequest Request, TransportResponse Expected);
public static class TransportCases
{
    public const string Revision = "6e0618704ad758ba2ea5822faa80bcd36fbeba3d";
    public const string AbcSha = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
    public static IEnumerable<object[]> All => new[]
    {
        new TransportCase("whole",new(new Uri("https://example.test/"+Revision+"/file"),Revision,AbcSha,3),
            new(new Uri("https://example.test/"+Revision+"/file"),200,Encoding.ASCII.GetBytes("abc"),"\"v1\"",DeclaredLength:3)),
        new TransportCase("range",new(new Uri("https://example.test/archive.zip"),Revision,AbcSha,3,8*1024*1024,new(4,3),"\"v1\"","\"v1\"",ExpectedTotalLength:100),
            new(new Uri("https://example.test/archive.zip"),206,Encoding.ASCII.GetBytes("abc"),"\"v1\"","bytes 4-6/100",DeclaredLength:3))
    }.Select(x=>new object[]{x});
    public static async Task AssertCase(TransportCase test,IByteTransport transport)
    {
        var actual=await transport.SendAsync(test.Request);
        Assert.Equal(test.Expected.Url,actual.Url); Assert.Equal(test.Expected.StatusCode,actual.StatusCode);
        Assert.Equal(test.Expected.Bytes.ToArray(),actual.Bytes.ToArray()); Assert.Equal(test.Expected.ETag,actual.ETag);
        Assert.Equal(test.Expected.ContentRange,actual.ContentRange); Assert.Equal(test.Expected.ContentEncoding,actual.ContentEncoding);
        Assert.Equal(test.Expected.DeclaredLength,actual.DeclaredLength);
    }
}
public sealed class ScriptedByteTransport(TransportCase test,string? mutation=null):IByteTransport
{
    public List<PinnedRequest> Requests { get; }=[];
    public Task<TransportResponse> SendAsync(PinnedRequest request,CancellationToken cancellationToken=default)
    {
        Requests.Add(request); Assert.Equal(test.Request,request);
        var r=test.Expected;
        return Task.FromResult(mutation switch {
            "url"=>r with {Url=new Uri("https://wrong.test/")}, "status"=>r with {StatusCode=500},
            "bytes"=>r with {Bytes=new byte[]{0}}, "etag"=>r with {ETag="wrong"},
            "range"=>r with {ContentRange="wrong"}, "encoding"=>r with {ContentEncoding="gzip"},
            "length"=>r with {DeclaredLength=99}, _=>r});
    }
}
[Trait("Gate","Mock")]
public sealed class TransportMockTests
{
    [Theory,MemberData(nameof(TransportCases.All),MemberType=typeof(TransportCases))]
    public async Task CorrectResponsesAndRequestInteractions(TransportCase test)
    {var fake=new ScriptedByteTransport(test);await TransportCases.AssertCase(test,fake);Assert.Equal(new[]{test.Request},fake.Requests);}
    [Theory,MemberData(nameof(TransportCases.All),MemberType=typeof(TransportCases))]
    public async Task EveryIndividualResponseMutationIsRejected(TransportCase test)
    {foreach(var property in new[]{"url","status","bytes","etag","range","encoding","length"}) Assert.NotNull(await Record.ExceptionAsync(()=>TransportCases.AssertCase(test,new ScriptedByteTransport(test,property))));}
}

public sealed record TransportPolicyCase(string Id,PinnedRequest Request,int Status,string? Range,string? ETag,string? Encoding,byte[] Body,string Error);
public static class TransportPolicyCases
{
    private static PinnedRequest Basic=>new(new Uri("https://example.test/"+TransportCases.Revision+"/file"),TransportCases.Revision,TransportCases.AbcSha,3);
    public static IEnumerable<object[]> All=>new[]{
        new TransportPolicyCase("auth",Basic,403,null,null,null,[97,98,99],"AUTH_PAUSE"),
        new TransportPolicyCase("status",Basic,500,null,null,null,[97,98,99],"HTTP_STATUS"),
        new TransportPolicyCase("encoding",Basic,200,null,null,"gzip",[97,98,99],"ENCODING"),
        new TransportPolicyCase("hash",Basic,200,null,null,null,[97,98,100],"BODY_HASH"),
        new TransportPolicyCase("size",Basic with{ExpectedLength=4},200,null,null,null,[97,98,99],"BODY_LENGTH"),
        new TransportPolicyCase("oversize",Basic with{MaxBytes=2},200,null,null,null,[97,98,99],"BODY_LIMIT"),
        new TransportPolicyCase("range-status",Basic with{Range=new(0,3)},200,"bytes 0-2/10",null,null,[97,98,99],"HTTP_STATUS"),
        new TransportPolicyCase("range-header",Basic with{Range=new(0,3)},206,"bytes 1-3/10",null,null,[97,98,99],"CONTENT_RANGE"),
        new TransportPolicyCase("etag",Basic with{ExpectedETag="\"v1\""},200,null,"\"v2\"",null,[97,98,99],"ETAG"),
        new TransportPolicyCase("total-size",Basic with{Range=new(0,3),ExpectedTotalLength=20},206,"bytes 0-2/10",null,null,[97,98,99],"CONTENT_RANGE"),
        new TransportPolicyCase("http",Basic with{Url=new Uri("http://example.test/file")},200,null,null,null,[97,98,99],"URL"),
        new TransportPolicyCase("mutable",Basic with{Url=new Uri("https://example.test/main/file"),ExpectedSha256=null},200,null,null,null,[97,98,99],"PIN"),
        new TransportPolicyCase("range-bound",Basic with{Range=new(0,8*1024*1024+1)},200,null,null,null,[97,98,99],"RANGE_LIMIT")
    }.Select(x=>new object[]{x});
    public static async Task AssertCase(TransportPolicyCase test,IByteTransport transport)
    {var error=await Record.ExceptionAsync(()=>transport.SendAsync(test.Request));Assert.IsType<TransportException>(error);Assert.Equal(test.Error,((TransportException)error!).Code);}
}
public sealed class ScriptedTransportPolicy(TransportPolicyCase test,string? mutation=null):IByteTransport
{
    public Task<TransportResponse> SendAsync(PinnedRequest request,CancellationToken cancellationToken=default)
    {Assert.Equal(test.Request,request);if(mutation!="error")throw new TransportException(mutation=="code"?"wrong":test.Error,"Scripted rejection");return Task.FromResult(new TransportResponse(request.Url,200,ReadOnlyMemory<byte>.Empty));}
}
[Trait("Gate","Mock")]
public sealed class TransportPolicyMockTests
{
    [Theory,MemberData(nameof(TransportPolicyCases.All),MemberType=typeof(TransportPolicyCases))]
    public Task CorrectRejections(TransportPolicyCase test)=>TransportPolicyCases.AssertCase(test,new ScriptedTransportPolicy(test));
    [Theory,MemberData(nameof(TransportPolicyCases.All),MemberType=typeof(TransportPolicyCases))]
    public async Task MissingAndWrongCodeRejected(TransportPolicyCase test)
    {foreach(var mutation in new[]{"error","code"})Assert.NotNull(await Record.ExceptionAsync(()=>TransportPolicyCases.AssertCase(test,new ScriptedTransportPolicy(test,mutation))));}
}

public sealed class RecordingHttpHandler(Func<HttpRequestMessage,int,HttpResponseMessage> respond):HttpMessageHandler
{
    public List<(string Method,Uri? Url,string? Range,string? IfMatch,string Encoding)> Requests {get;}=[];
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
    {
        Requests.Add((request.Method.Method,request.RequestUri,request.Headers.Range?.ToString(),request.Headers.IfMatch.ToString(),request.Headers.AcceptEncoding.ToString()));
        return Task.FromResult(respond(request,Requests.Count));
    }
}
public sealed class RecordingTransportDelay:ITransportDelay
{
    public List<TimeSpan> Delays {get;}=[];
    public Task DelayAsync(TimeSpan delay,CancellationToken cancellationToken=default){cancellationToken.ThrowIfCancellationRequested();Delays.Add(delay);return Task.CompletedTask;}
}

[Trait("Gate","Real")]
public sealed class TransportRealTests
{
    [Theory,MemberData(nameof(TransportCases.All),MemberType=typeof(TransportCases))]
    public async Task HttpAdapterUsesSameCasesAndExactRequestInteractions(TransportCase test)
    {
        var handler=new RecordingHttpHandler((_,_)=>{
            var response=new HttpResponseMessage((HttpStatusCode)test.Expected.StatusCode){Content=new ByteArrayContent(test.Expected.Bytes.ToArray())};
            response.Headers.TryAddWithoutValidation("ETag",test.Expected.ETag);
            if(test.Expected.ContentRange is not null)response.Content.Headers.TryAddWithoutValidation("Content-Range",test.Expected.ContentRange);
            return response;
        });
        using var client=new HttpClient(handler);
        await TransportCases.AssertCase(test,new HttpByteTransport(client));
        var request=Assert.Single(handler.Requests);Assert.Equal(test.Request.Method,request.Method);Assert.Equal(test.Request.Url,request.Url);
        Assert.Equal(test.Request.Range is null?null:$"bytes={test.Request.Range.Offset}-{test.Request.Range.Offset+test.Request.Range.Length-1}",request.Range);
        Assert.Equal(test.Request.IfMatch??"",request.IfMatch);Assert.Equal("identity",request.Encoding);
    }
}
