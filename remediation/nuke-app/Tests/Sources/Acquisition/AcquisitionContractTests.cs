using System.Text;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

// FR-BUILD-004 ac-2/ac-3, TEST-BUILD-004: data pins are independently fixed.
public sealed record AcquisitionCase(string Id,string Encoding,byte[] Wire,byte[] Expected,string Sha256,long Size,string? GitBlob,string? Error);
public interface IAcquisitionContractProbe
{
    Task<(ReadOnlyMemory<byte> Bytes,string Sha256,long Size,string Status)> AcquireAsync(AcquisitionCase test);
    int NetworkCalls {get;}
    int Writes {get;}
}
public static class AcquisitionCases
{
    public const string AbcSha="ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
    public static IEnumerable<object[]> All=>new[]{
        new AcquisitionCase("raw","Raw",Encoding.ASCII.GetBytes("abc"),Encoding.ASCII.GetBytes("abc"),AbcSha,3,"f2ba8f84ab5c1bce84a7b441cb1959cfc7093b7f",null),
        new AcquisitionCase("gitiles-whitespace","GitilesText",Encoding.ASCII.GetBytes(" Y W J j\n"),Encoding.ASCII.GetBytes("abc"),AbcSha,3,null,null),
        new AcquisitionCase("gitiles-invalid","GitilesText",Encoding.ASCII.GetBytes("YWJj!"),[],AbcSha,3,null,"BASE64"),
        new AcquisitionCase("hash-mismatch","Raw",Encoding.ASCII.GetBytes("abd"),[],AbcSha,3,null,"SOURCE_HASH"),
        new AcquisitionCase("length-mismatch","Raw",Encoding.ASCII.GetBytes("abc"),[],AbcSha,4,null,"SOURCE_SIZE"),
        new AcquisitionCase("git-mismatch","Raw",Encoding.ASCII.GetBytes("abc"),[],AbcSha,3,new string('0',40),"SOURCE_GIT_BLOB")
    }.Select(x=>new object[]{x});
    public static async Task AssertCase(AcquisitionCase test,IAcquisitionContractProbe provider)
    {
        if(test.Error is not null){var e=await Record.ExceptionAsync(()=>provider.AcquireAsync(test));Assert.IsType<TransportException>(e);Assert.Equal(test.Error,((TransportException)e!).Code);Assert.Equal(1,provider.NetworkCalls);Assert.Equal(0,provider.Writes);return;}
        var r=await provider.AcquireAsync(test);Assert.Equal(test.Expected,r.Bytes.ToArray());Assert.Equal(test.Sha256,r.Sha256);Assert.Equal(test.Size,r.Size);Assert.Equal("downloaded",r.Status);Assert.Equal(1,provider.NetworkCalls);Assert.Equal(1,provider.Writes);
        var reused=await provider.AcquireAsync(test);Assert.Equal("reused",reused.Status);Assert.Equal(test.Expected,reused.Bytes.ToArray());Assert.Equal(1,provider.NetworkCalls);Assert.Equal(1,provider.Writes);
    }
}
public sealed class ScriptedAcquisitionProbe(string? mutation=null):IAcquisitionContractProbe
{
    public int NetworkCalls {get;private set;}
    public int Writes {get;private set;}
    private bool acquired;
    public Task<(ReadOnlyMemory<byte> Bytes,string Sha256,long Size,string Status)> AcquireAsync(AcquisitionCase test)
    {
        if(!acquired || mutation=="network")NetworkCalls++;
        if(test.Error is not null && mutation!="error")throw new TransportException(mutation=="code"?"wrong":test.Error,"Scripted contract rejection");
        var status=acquired?"reused":"downloaded";if(!acquired || mutation=="write")Writes++;acquired=true;
        return Task.FromResult<(ReadOnlyMemory<byte>,string,long,string)>((mutation=="bytes"?new byte[]{0}:test.Expected,mutation=="hash"?"wrong":test.Sha256,mutation=="size"?-1:test.Size,mutation=="status"?"wrong":status));
    }
}
[Trait("Gate","Mock")]
public sealed class AcquisitionMockTests
{
    [Theory,MemberData(nameof(AcquisitionCases.All),MemberType=typeof(AcquisitionCases))]
    public Task CorrectMocks(AcquisitionCase test)=>AcquisitionCases.AssertCase(test,new ScriptedAcquisitionProbe());
    [Theory,MemberData(nameof(AcquisitionCases.All),MemberType=typeof(AcquisitionCases))]
    public async Task IndividualPropertyAndInteractionNegativeControls(AcquisitionCase test)
    {foreach(var mutation in test.Error is null?new[]{"bytes","hash","size","status","network","write"}:new[]{"error","code"})Assert.NotNull(await Record.ExceptionAsync(()=>AcquisitionCases.AssertCase(test,new ScriptedAcquisitionProbe(mutation))));}
}
