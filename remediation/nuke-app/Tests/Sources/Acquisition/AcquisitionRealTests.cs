using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed class AcquisitionRealProbe:IAcquisitionContractProbe
{
    private readonly MemoryAcquisitionStore store=new();
    public int NetworkCalls {get;private set;}
    public int Writes=>store.Writes;
    public async Task<(ReadOnlyMemory<byte> Bytes,string Sha256,long Size,string Status)> AcquireAsync(AcquisitionCase test)
    {
        var pin=new SourcePin("source/a",new Uri("https://example.test/"+AndroidMcp.BuildTool.Tests.Core.TransportCases.Revision+"/a"),AndroidMcp.BuildTool.Tests.Core.TransportCases.Revision,test.Sha256,test.Size,test.GitBlob,Enum.Parse<SourceWireEncoding>(test.Encoding));
        var acquired=await new SourceAcquirer(new FixtureTransport(this,test),store).AcquireAsync(pin);
        return(await store.ReadAsync(pin.LocalPath),acquired.Artifact.Sha256,acquired.Artifact.Length,acquired.Status);
    }
    private sealed class FixtureTransport(AcquisitionRealProbe owner,AcquisitionCase fixture):IByteTransport
    {
        public Task<TransportResponse> SendAsync(PinnedRequest request,CancellationToken cancellationToken=default)
        {owner.NetworkCalls++;Assert.Equal(SourceWireEncoding.GitilesText.ToString()==fixture.Encoding,request.Url.Query=="?format=TEXT");return Task.FromResult(new TransportResponse(request.Url,200,fixture.Wire));}
    }
}
[Trait("Gate","Real")]
public sealed class AcquisitionRealTests
{
    [Theory,MemberData(nameof(AcquisitionCases.All),MemberType=typeof(AcquisitionCases))]
    public Task SameCasesAndAssertionsAgainstSourceAcquirer(AcquisitionCase test)=>AcquisitionCases.AssertCase(test,new AcquisitionRealProbe());
}
