using System.Net;
using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed class NativeToolArtifactProbe:IToolArtifactProbe
{
    private readonly MemoryAcquisitionStore store=new();private readonly List<string> requests=[];
    private readonly Dictionary<string,byte[]> originals=new(StringComparer.Ordinal);private IToolArtifactAcquirer? service;
    public IReadOnlyList<string> Requests=>requests;public int Writes=>store.Writes;
    public bool OriginalRetained=>originals.All(x=>store.Files.TryGetValue(x.Key,out var value)&&value.Bytes.Span.SequenceEqual(x.Value));
    public async Task<byte[]> ReadAsync(string path)=>(await store.ReadAsync(path)).ToArray();
    public Task<ToolArtifactReceipt> AcquireAsync(ToolCase test)
    {
        if(service is null)
        {
            if(test.Cache)store.Files[test.Request.CachePath]=new(test.Request.CachePath,test.CorruptCache?new byte[]{0}:test.Bytes,"application/octet-stream");
            if(test.Proof)
            {
                var pin=test.Request.Pin;var raw=Encoding.ASCII.GetBytes(pin.Sha256+"\n");var proof=new ToolChecksumProof(1,pin.Url.AbsoluteUri,test.CorruptProof?new string('0',64):pin.Sha256,pin.SizeBytes,pin.PublishedChecksumUrl!.AbsoluteUri,ContentHash.Sha256(raw),Convert.ToBase64String(raw));
                var path=test.Request.OfflineChecksumProofPath!;store.Files[path]=new(path,new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(proof)),"application/json");
            }
            if(test.Conflict){var path=test.Files.Keys.First();store.Files[path]=new(path,Encoding.ASCII.GetBytes("old"),"application/octet-stream");}
            foreach(var file in store.Files)originals[file.Key]=file.Value.Bytes.ToArray();
            service=new ToolArtifactAcquirer(new BoundaryTransport(test,requests),store,new ScriptedLeaseProvider(),new CanonicalJson());
        }
        return service.AcquireAsync(test.Request);
    }
    private sealed class BoundaryTransport(ToolCase test,List<string> requests):IByteTransport
    {
        public Task<TransportResponse> SendAsync(PinnedRequest request,CancellationToken cancellationToken=default)
        {
            requests.Add(request.Url.AbsoluteUri);var authorization=Assert.IsType<ToolTransferPin>(request.ToolTransfer);Assert.Equal(test.Request.Pin.Sha256,authorization.ArtifactSha256);Assert.Equal(test.Request.Pin.SizeBytes,authorization.ExactArtifactBytes);Assert.Equal(test.Request.Pin.Bounds.MaxDownloadBytes,authorization.MaxDownloadBytes);
            if(request.Url==test.Request.Pin.PublishedChecksumUrl)
            {
                Assert.Equal(ToolTransferKind.PublishedChecksum,authorization.Kind);Assert.Equal(4096,request.MaxBytes);Assert.Null(request.ExpectedSha256);Assert.Null(request.ExpectedLength);
                if(test.Id=="checksum-denied")throw new TransportException("AUTH_PAUSE","Fixture checksum access denied.");
                var checksum=Encoding.ASCII.GetBytes(test.Checksum??test.Request.Pin.Sha256+"\n");return Task.FromResult(new TransportResponse(request.Url,200,checksum,DeclaredLength:checksum.Length));
            }
            Assert.Equal(test.Request.Pin.Url,request.Url);Assert.Equal(ToolTransferKind.Artifact,authorization.Kind);Assert.Equal(test.Request.Pin.Sha256,request.ExpectedSha256);Assert.Equal(test.Request.Pin.SizeBytes,request.ExpectedLength);Assert.Equal(test.Request.Pin.SizeBytes,request.MaxBytes);
            return Task.FromResult(new TransportResponse(request.Url,200,test.Bytes,DeclaredLength:test.Bytes.Length));
        }
    }
}
public sealed class NativeToolProofProbe:IToolProofProbe
{
    public async Task<ToolProofObservation> ReadAsync(bool offline)
    {
        var test=ToolProofCases.Fixture;
        if(offline)test=test with{Request=test.Request with{Access=ToolArtifactAccess.Offline,OfflineChecksumProofPath="proof/retained.json"},Cache=true,Proof=true};
        var provider=new NativeToolArtifactProbe();var result=await provider.AcquireAsync(test);var proof=Assert.IsType<StoredArtifact>(result.ChecksumProof);return new(proof,await provider.ReadAsync(proof.RelativePath));
    }
}
public sealed class NativeToolTransferProbe:IToolTransferProbe
{
    private RecordingHttpHandler? handler;public int Calls=>handler?.Requests.Count??0;
    public async Task<TransportResponse> SendAsync(ToolTransferCase test)
    {
        handler=new((_,attempt)=>{
            if(test.Redirect is not null&&(attempt==1||test.Id=="redirect-cycle")){var redirected=new HttpResponseMessage(HttpStatusCode.TemporaryRedirect);redirected.Headers.Location=test.Redirect;return redirected;}
            return new(HttpStatusCode.OK){Content=new ByteArrayContent(test.Body)};
        });
        using var client=new HttpClient(handler);return await new HttpByteTransport(client).SendAsync(test.Request);
    }
}
[Trait("Gate","Real")]
public sealed class ToolArtifactRealTests
{
    [Theory,MemberData(nameof(ToolArtifactCases.All),MemberType=typeof(ToolArtifactCases))]
    public Task SamePinnedArtifactAndInteractionCases(ToolCase test)=>ToolArtifactCases.AssertCase(test,new NativeToolArtifactProbe());
    [Theory,MemberData(nameof(ToolTransferCases.All),MemberType=typeof(ToolTransferCases))]
    public Task SameTypedTransferCases(ToolTransferCase test)=>ToolTransferCases.AssertCase(test,new NativeToolTransferProbe());
    [Theory][InlineData(false)][InlineData(true)]public Task SameEveryFieldChecksumProofAssertions(bool offline)=>ToolProofCases.AssertCase(offline,new NativeToolProofProbe());
}
