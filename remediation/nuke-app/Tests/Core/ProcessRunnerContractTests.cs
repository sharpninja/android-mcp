using AndroidMcp.BuildTool.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;

public sealed record ProcessCase(string Id,string[] Arguments,int ExitCode,bool Cancelled,string? Error);
public static class ProcessRunnerCases
{
    public static IEnumerable<object[]> Cases=>new[]{
        new ProcessCase("PROC-01",["arg with spaces","$literal;not-shell","雪"],17,false,null),
        new ProcessCase("PROC-02",[""],0,false,null),
        new ProcessCase("PROC-03",["cancel"],-1,true,null),
        new ProcessCase("PROC-04",["invalid-tool"],0,false,"InvalidOperationException")
    }.Select(x=>new object[]{x});
    public static async Task AssertContract(ProcessCase test,IProcessRunner runner,ProcessRequest request)
    {
        using var cancellation=new CancellationTokenSource();
        if(test.Cancelled)cancellation.Cancel();
        if(test.Error is not null)
        {
            var error=await Record.ExceptionAsync(()=>runner.RunAsync(request,cancellation.Token));
            Assert.NotNull(error);Assert.Equal(test.Error,error!.GetType().Name);return;
        }
        var result=await runner.RunAsync(request,cancellation.Token);
        Assert.Equal(test.ExitCode,result.ExitCode);Assert.Equal(test.Cancelled,result.Cancelled);
        Assert.Equal(Path.Combine(request.ReceiptDirectory,"stdout.bin"),result.StandardOutputPath);
        Assert.Equal(Path.Combine(request.ReceiptDirectory,"stderr.bin"),result.StandardErrorPath);
        Assert.True(result.EndedAt>=result.StartedAt);
    }
    public static ProcessRequest Request(ProcessCase test)=>new(new("/approved/native/tool","pinned-sha256","fixture"),test.Arguments,"/approved/work",new Dictionary<string,string>(),"/receipt/run");
}
public sealed class ScriptedProcessRunner(ProcessCase test,string? mutation=null):IProcessRunner
{
    public ProcessRequest? Received {get;private set;}
    public Task<ProcessResult> RunAsync(ProcessRequest request,CancellationToken cancellationToken=default)
    {
        Received=request;
        if(test.Error is not null && mutation!="error")throw new InvalidOperationException("Tool identity mismatch.");
        var now=DateTimeOffset.UnixEpoch;
        return Task.FromResult(new ProcessResult(mutation=="exit"?999:test.ExitCode,
            mutation=="stdout"?"wrong":Path.Combine(request.ReceiptDirectory,"stdout.bin"),
            mutation=="stderr"?"wrong":Path.Combine(request.ReceiptDirectory,"stderr.bin"),now,
            mutation=="time"?now.AddSeconds(-1):now,
            mutation=="cancel"?!test.Cancelled:test.Cancelled));
    }
}
[Trait("Gate","Mock")]
public sealed class ProcessRunnerMockTests
{
    [Theory,MemberData(nameof(ProcessRunnerCases.Cases),MemberType=typeof(ProcessRunnerCases))]
    public async Task CorrectMock(ProcessCase test)
    {
        var provider=new ScriptedProcessRunner(test);var request=ProcessRunnerCases.Request(test);
        await ProcessRunnerCases.AssertContract(test,provider,request);Assert.Same(request,provider.Received);
    }
    [Theory,MemberData(nameof(ProcessRunnerCases.Cases),MemberType=typeof(ProcessRunnerCases))]
    public async Task WrongIndividualResponsesRejected(ProcessCase test)
    {
        var mutations=test.Error is null?new[]{"exit","stdout","stderr","time","cancel"}:new[]{"error"};
        foreach(var mutation in mutations)
            Assert.NotNull(await Record.ExceptionAsync(()=>ProcessRunnerCases.AssertContract(test,new ScriptedProcessRunner(test,mutation),ProcessRunnerCases.Request(test))));
    }
}
