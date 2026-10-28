using System.Net;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed class ArchiveAcquisitionRealProbe:IArchiveAcquisitionProbe
{
    private readonly MemoryAcquisitionStore store=new();
    public int NetworkCalls{get;private set;}public int Writes=>store.Writes;
    public Task<byte[]> ReadAsync()=>Task.FromResult(store.Files["sources/api.xml"].Bytes.ToArray());
    public Task<AcquiredSource> AcquireAsync()
    {
        var zip=ArchiveCases.ZipFixture();var sha=ContentHash.Sha256(zip);
        var pin=new SourcePin("sources/api.xml",new Uri("https://example.test/archive.zip"),"pinned",AcquisitionCases.AbcSha,3,
            Archive:new(new Uri("https://example.test/archive.zip"),"data/api-versions.xml",sha,ContentHash.AidlSha1Legacy(zip),zip.Length));
        return new SourceAcquirer(new BytesTransport(this,zip),store).AcquireAsync(pin);
    }
    private sealed class BytesTransport(ArchiveAcquisitionRealProbe owner,byte[] bytes):IByteTransport
    {public Task<TransportResponse> SendAsync(PinnedRequest request,CancellationToken cancellationToken=default){owner.NetworkCalls++;return Task.FromResult(new TransportResponse(request.Url,200,bytes));}}
}
public sealed class ArchiveSafetyRealProbe:IArchiveSafetyProbe
{
    private readonly MemoryAcquisitionStore store=new();public int Writes=>store.Writes;
    public Task ExecuteAsync(ArchiveSafetyCase test)=>new NativeArchiveExtractor().ExtractTarGZipAsync(NativeArchiveCases.Fixture(test.Path,test.Link),store,"native");
}
public sealed class RetryRealProbe:IRetryContractProbe
{
    private readonly RecordingTransportDelay delay=new();
    private readonly RecordingHttpHandler handler=new((_,attempt)=>new HttpResponseMessage(attempt<3?HttpStatusCode.TooManyRequests:HttpStatusCode.OK){Content=new ByteArrayContent([97,98,99])});
    public IReadOnlyList<TimeSpan> Delays=>delay.Delays;public IReadOnlyList<Uri> Requests=>handler.Requests.Select(x=>x.Url!).ToArray();
    public async Task RunAsync(){using var client=new HttpClient(handler);await new HttpByteTransport(client,delay:delay).SendAsync(new(RetryCases.Url,TransportCases.Revision,TransportCases.AbcSha,3,Retry:TransportRetryPolicy.CanonicalRestore));}
}
public sealed class RangeAdapterMetadataReader:IMetadataArchiveReader
{
    public MetadataArchiveEvidence Read(IRangeSource source)
    {
        var handler=new RecordingHttpHandler((message,_)=>{
            Assert.Equal("\"v1\"",message.Headers.IfMatch.ToString());Assert.Equal("identity",message.Headers.AcceptEncoding.ToString());
            var range=Assert.Single(message.Headers.Range!.Ranges);var start=range.From!.Value;var end=range.To!.Value;
            var response=new HttpResponseMessage(HttpStatusCode.PartialContent){Content=new ByteArrayContent(source.Read(start,checked((int)(end-start+1))))};
            response.Content.Headers.ContentRange=new System.Net.Http.Headers.ContentRangeHeaderValue(start,end,source.Length);response.Headers.ETag=new("\"v1\"");return response;
        });
        using var client=new HttpClient(handler);var transport=new HttpByteTransport(client,delay:new RecordingTransportDelay());
        var ranges=HttpRangeSource.CreateAsync(transport,new(new Uri("https://example.test/archive.zip"),"source.properties",SizeBytes:source.Length,ETag:"\"v1\""),"37.2",AcquisitionCases.AbcSha).GetAwaiter().GetResult();
        return new MetadataArchiveReader().Read(ranges);
    }
}
[Trait("Gate","Real")]
public sealed class ExtendedAcquisitionRealTests
{
    [Fact]public Task FullPinnedZipUsesSameAcquisitionAssertions()=>ArchiveAcquisitionCases.AssertCase(new ArchiveAcquisitionRealProbe());
    [Theory,MemberData(nameof(ArchiveSafetyCases.All),MemberType=typeof(ArchiveSafetyCases))]
    public Task SameArchiveSafetyAssertions(ArchiveSafetyCase test)=>ArchiveSafetyCases.AssertCase(test,new ArchiveSafetyRealProbe());
    [Fact]public Task SameCanonicalRetryInteractions()=>RetryCases.AssertCase(new RetryRealProbe());
    [Fact]public Task SameCacheIdentityAndStrictPolicyAssertions()=>SourceCacheCases.AssertCase(new SourceCache(new MemoryAcquisitionStore(),new ScriptedLeaseProvider(),new CanonicalJson()));
    [Fact]public Task SameMetadataAssertionsThroughExactHttpRanges()=>ArchiveCases.AssertCase(new RangeAdapterMetadataReader(),new(ArchiveCases.ZipFixture()));
    [Theory,MemberData(nameof(TransportPolicyCases.All),MemberType=typeof(TransportPolicyCases))]
    public async Task SameTransportPolicyAssertions(TransportPolicyCase test)
    {
        var handler=new RecordingHttpHandler((_,_)=>{
            var response=new HttpResponseMessage((HttpStatusCode)test.Status){Content=new ByteArrayContent(test.Body)};
            if(test.Range is not null)response.Content.Headers.TryAddWithoutValidation("Content-Range",test.Range);
            if(test.ETag is not null)response.Headers.TryAddWithoutValidation("ETag",test.ETag);
            if(test.Encoding is not null)response.Content.Headers.ContentEncoding.Add(test.Encoding);return response;});
        using var client=new HttpClient(handler);await TransportPolicyCases.AssertCase(test,new HttpByteTransport(client));
        Assert.InRange(handler.Requests.Count,0,1);
    }
}
