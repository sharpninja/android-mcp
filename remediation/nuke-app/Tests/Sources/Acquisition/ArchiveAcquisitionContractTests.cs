using System.Text;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;
public interface IArchiveAcquisitionProbe
{
    Task<AcquiredSource> AcquireAsync();
    Task<byte[]> ReadAsync();
    int NetworkCalls {get;}
    int Writes {get;}
}
public static class ArchiveAcquisitionCases
{
    public static async Task AssertCase(IArchiveAcquisitionProbe provider)
    {
        var result=await provider.AcquireAsync();Assert.Equal("downloaded",result.Status);Assert.Equal("sources/api.xml",result.Artifact.RelativePath);
        Assert.Equal(AcquisitionCases.AbcSha,result.Artifact.Sha256);Assert.Equal(3,result.Artifact.Length);Assert.Equal(Encoding.ASCII.GetBytes("abc"),await provider.ReadAsync());
        Assert.Equal(1,provider.NetworkCalls);Assert.Equal(2,provider.Writes);
        var reused=await provider.AcquireAsync();Assert.Equal("reused",reused.Status);Assert.Equal(result.Artifact,reused.Artifact);Assert.Equal(1,provider.NetworkCalls);Assert.Equal(2,provider.Writes);
    }
}
public sealed class ScriptedArchiveAcquisitionProbe(string? mutation=null):IArchiveAcquisitionProbe
{
    public int NetworkCalls {get;private set;}public int Writes {get;private set;}
    public Task<AcquiredSource> AcquireAsync()
    {
        var first=Writes==0;if(first||mutation=="network")NetworkCalls++;if(first||mutation=="write")Writes+=2;
        return Task.FromResult(new AcquiredSource(new(mutation=="path"?"wrong":"sources/api.xml",mutation=="hash"?"wrong":AcquisitionCases.AbcSha,mutation=="size"?4:3,"application/octet-stream"),mutation=="status"?"wrong":first?"downloaded":"reused",new Uri("https://example.test/archive.zip"),"pinned",null));
    }
    public Task<byte[]> ReadAsync()=>Task.FromResult(mutation=="bytes"?new byte[]{0}:Encoding.ASCII.GetBytes("abc"));
}
[Trait("Gate","Mock")]
public sealed class ArchiveAcquisitionMockTests
{
    [Fact]public Task CorrectMock()=>ArchiveAcquisitionCases.AssertCase(new ScriptedArchiveAcquisitionProbe());
    [Theory][InlineData("path")][InlineData("hash")][InlineData("size")][InlineData("status")][InlineData("bytes")][InlineData("network")][InlineData("write")]
    public async Task IndividualPropertiesAndInteractionsRejected(string mutation)=>Assert.NotNull(await Record.ExceptionAsync(()=>ArchiveAcquisitionCases.AssertCase(new ScriptedArchiveAcquisitionProbe(mutation))));
}
