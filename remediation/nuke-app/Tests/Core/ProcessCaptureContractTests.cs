using System.Text;
using AndroidMcp.BuildTool.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;
public static class ProcessCaptureCases
{
    public static IEnumerable<object[]> Cases => new[]{0,7,1048583}.Select(n=>new object[]{n});
    public static byte[] Payload(int length)=>Enumerable.Range(0,length).Select(n=>(byte)(n%256)).ToArray();
    public static byte[] ErrorBytes=>Encoding.UTF8.GetBytes("stderr:\0雪/no-final-newline");
    public static async Task AssertContract(int length,IProcessCaptureReader reader,ProcessResult result)
    {
        Assert.Equal(Payload(length),(await reader.ReadAsync(result,false)).ToArray());
        Assert.Equal(ErrorBytes,(await reader.ReadAsync(result,true)).ToArray());
    }
}
public sealed class ScriptedCaptureReader(int length,string? mutation=null) : IProcessCaptureReader
{
    public Task<ReadOnlyMemory<byte>> ReadAsync(ProcessResult result,bool standardError,CancellationToken cancellationToken=default)
    {
        var bytes=standardError?ProcessCaptureCases.ErrorBytes:ProcessCaptureCases.Payload(length);
        if(mutation=="truncate" && standardError)bytes=bytes[..^1];
        if(mutation=="wrong" && standardError)bytes[0]^=255;
        if(mutation=="omit")bytes=[];
        return Task.FromResult<ReadOnlyMemory<byte>>(bytes);
    }
}
[Trait("Gate","Mock")]
public sealed class ProcessCaptureMockTests
{
    private static readonly ProcessResult Result=new(17,"out","err",DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch,false);
    [Theory,MemberData(nameof(ProcessCaptureCases.Cases),MemberType=typeof(ProcessCaptureCases))]
    public Task CorrectMock(int length)=>ProcessCaptureCases.AssertContract(length,new ScriptedCaptureReader(length),Result);
    [Theory,MemberData(nameof(ProcessCaptureCases.Cases),MemberType=typeof(ProcessCaptureCases))]
    public async Task CorruptionTruncationAndOmissionRejected(int length)
    {
        foreach(var mutation in new[]{"truncate","wrong","omit"})
            Assert.NotNull(await Record.ExceptionAsync(()=>ProcessCaptureCases.AssertContract(length,new ScriptedCaptureReader(length,mutation),Result)));
    }
}
