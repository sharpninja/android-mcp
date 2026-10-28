using System.IO.Compression;
using AndroidMcp.BuildTool.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;
public sealed record ArchiveSafetyCase(string Id,string Path,bool Link,string Error);
public interface IArchiveSafetyProbe
{
    Task ExecuteAsync(ArchiveSafetyCase test);
    int Writes {get;}
}
public static class ArchiveSafetyCases
{
    public static IEnumerable<object[]> All=>new[]{
        new ArchiveSafetyCase("traversal","repo/../../escape",false,"ArgumentException"),
        new ArchiveSafetyCase("absolute","/repo/a",false,"ArgumentException"),
        new ArchiveSafetyCase("backslash","repo/a\\b",false,"ArgumentException"),
        new ArchiveSafetyCase("symlink","repo/a",true,"ARCHIVE_LINK")
    }.Select(x=>new object[]{x});
    public static async Task AssertCase(ArchiveSafetyCase test,IArchiveSafetyProbe provider)
    {
        var error=await Record.ExceptionAsync(()=>provider.ExecuteAsync(test));Assert.NotNull(error);
        if(test.Error=="ArgumentException")Assert.IsType<ArgumentException>(error);else{Assert.IsType<TransportException>(error);Assert.Equal(test.Error,((TransportException)error!).Code);}
        Assert.Equal(0,provider.Writes);
    }
}
public sealed class ScriptedArchiveSafety(string? mutation=null):IArchiveSafetyProbe
{
    public int Writes=>mutation=="write"?1:0;
    public Task ExecuteAsync(ArchiveSafetyCase test)
    {if(mutation=="error")return Task.CompletedTask;if(test.Error=="ArgumentException")throw new ArgumentException();throw new TransportException(test.Error,"Scripted rejection");}
}
[Trait("Gate","Mock")]
public sealed class ArchiveSafetyMockTests
{
    [Theory,MemberData(nameof(ArchiveSafetyCases.All),MemberType=typeof(ArchiveSafetyCases))]
    public Task CorrectRejections(ArchiveSafetyCase test)=>ArchiveSafetyCases.AssertCase(test,new ScriptedArchiveSafety());
    [Theory,MemberData(nameof(ArchiveSafetyCases.All),MemberType=typeof(ArchiveSafetyCases))]
    public async Task MissingErrorAndPrematureWriteRejected(ArchiveSafetyCase test)
    {foreach(var mutation in new[]{"error","write"})Assert.NotNull(await Record.ExceptionAsync(()=>ArchiveSafetyCases.AssertCase(test,new ScriptedArchiveSafety(mutation))));}
}

public interface IRetryContractProbe
{
    Task RunAsync();
    IReadOnlyList<TimeSpan> Delays {get;}
    IReadOnlyList<Uri> Requests {get;}
}
public static class RetryCases
{
    public static readonly Uri Url=new("https://example.test/pinned");
    public static async Task AssertCase(IRetryContractProbe probe)
    {await probe.RunAsync();Assert.Equal(new[]{Url,Url,Url},probe.Requests);Assert.Equal(new[]{1,60,1,120,1}.Select(x=>TimeSpan.FromSeconds(x)),probe.Delays);}
}
public sealed class ScriptedRetryProbe(string? mutation=null):IRetryContractProbe
{
    public Task RunAsync()=>Task.CompletedTask;
    public IReadOnlyList<TimeSpan> Delays=>mutation=="delays"?[]:new[]{1,60,1,120,1}.Select(x=>TimeSpan.FromSeconds(x)).ToArray();
    public IReadOnlyList<Uri> Requests=>mutation=="url"?[RetryCases.Url,new("https://wrong.test/"),RetryCases.Url]:mutation=="attempts"?[RetryCases.Url]:[RetryCases.Url,RetryCases.Url,RetryCases.Url];
}
[Trait("Gate","Mock")]
public sealed class RetryMockTests
{
    [Fact]public Task CorrectPacingRetryAndSameUrl()=>RetryCases.AssertCase(new ScriptedRetryProbe());
    [Theory][InlineData("delays")][InlineData("url")][InlineData("attempts")]
    public async Task InteractionMutationsRejected(string mutation)=>Assert.NotNull(await Record.ExceptionAsync(()=>RetryCases.AssertCase(new ScriptedRetryProbe(mutation))));
}
