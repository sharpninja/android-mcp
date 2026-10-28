using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed record ProcessSessionCase(string Id,string Action,int ExitCode,bool Cancelled);
public static class ProcessSessionCases
{
    public static readonly IReadOnlyList<ProcessSessionCase> All=[new("SESSION-natural","natural",17,false),new("SESSION-stop","stop",-1,true),new("SESSION-cancel","cancel",-1,true),new("SESSION-dispose","dispose",-1,true),new("SESSION-before","before",-1,true)];
    public static IEnumerable<object[]> Cases=>All.Select(x=>new object[]{x.Id});
    public static ProcessSessionCase Get(string id)=>All.Single(x=>x.Id==id);
    public static async Task AssertContract(ProcessSessionCase test,IProcessSessionFactory factory,ProcessRequest request,Func<IProcessSession,Task> release)
    {
        var arguments=request.Arguments.ToArray();var environment=request.PublicEnvironment.ToArray();using var cancel=new CancellationTokenSource();
        if(test.Action=="before")cancel.Cancel();
        var session=await factory.StartAsync(request,cancel.Token);
        try
        {
            Assert.Equal(Path.Combine(request.ReceiptDirectory,"stdout.bin"),session.StandardOutputPath);Assert.Equal(Path.Combine(request.ReceiptDirectory,"stderr.bin"),session.StandardErrorPath);
            var completion=session.Completion;Assert.Same(completion,session.Completion);
            if(test.Action=="before"){Assert.Equal(0,session.ProcessId);Assert.True(completion.IsCompleted);}
            else {Assert.True(session.ProcessId>0);Assert.False(completion.IsCompleted);}
            switch(test.Action)
            {
                case "natural":await release(session);break;
                case "stop":await session.StopAsync();await session.StopAsync();Assert.True(completion.IsCompleted);break;
                case "cancel":cancel.Cancel();break;
                case "dispose":await session.DisposeAsync();await session.DisposeAsync();Assert.True(completion.IsCompleted);break;
            }
            var result=await completion;Assert.Equal(test.ExitCode,result.ExitCode);Assert.Equal(test.Cancelled,result.Cancelled);
            Assert.Equal(session.StandardOutputPath,result.StandardOutputPath);Assert.Equal(session.StandardErrorPath,result.StandardErrorPath);Assert.True(result.EndedAt>=result.StartedAt);
            await session.StopAsync();Assert.Same(result,await session.Completion);
            Assert.Equal(arguments,request.Arguments);Assert.Equal(environment,request.PublicEnvironment);
        }
        finally {await session.DisposeAsync();}
    }
}

internal sealed class ScriptedProcessSession(ProcessRequest request,ProcessSessionCase test,CancellationToken token,string? mutation=null):IProcessSession
{
    private readonly TaskCompletionSource<ProcessResult> completion=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenRegistration registration;private bool initialized;
    public int ProcessId=>mutation=="pid"?-1:test.Action=="before"?0:1234;
    public string StandardOutputPath=>mutation=="stdout"?"wrong":Path.Combine(request.ReceiptDirectory,"stdout.bin");
    public string StandardErrorPath=>mutation=="stderr"?"wrong":Path.Combine(request.ReceiptDirectory,"stderr.bin");
    public Task<ProcessResult> Completion
    {
        get
        {
            if(!initialized){initialized=true;registration=token.Register(()=>Complete(true));if(mutation=="early")Complete(false);}
            return completion.Task;
        }
    }
    public void Complete(bool cancelled)
    {
        if(completion.Task.IsCompleted)return;
        var start=DateTimeOffset.UnixEpoch;
        completion.TrySetResult(new(mutation=="exit"?888:cancelled?-1:17,mutation=="result-stdout"?"wrong":StandardOutputPath,mutation=="result-stderr"?"wrong":StandardErrorPath,
            start,mutation=="time"?start.AddSeconds(-1):start,mutation=="cancel"?!cancelled:cancelled));
    }
    public Task StopAsync(CancellationToken cancellationToken=default){Complete(true);return Task.CompletedTask;}
    public ValueTask DisposeAsync(){Complete(true);registration.Dispose();return ValueTask.CompletedTask;}
}
internal sealed class ScriptedProcessSessionFactory(ProcessSessionCase test,string? mutation=null):IProcessSessionFactory
{
    public ProcessRequest? Received {get;private set;}
    public Task<IProcessSession> StartAsync(ProcessRequest request,CancellationToken cancellationToken=default)
    {
        Received=request;return Task.FromResult<IProcessSession>(new ScriptedProcessSession(request,test,cancellationToken,mutation));
    }
}

public sealed record ProcessAdmissionCase(string Id,byte[]? Bytes,string Name,string? BadOption=null);
public static class ProcessAdmissionCases
{
    public static readonly IReadOnlyList<ProcessAdmissionCase> All=[
        new("NATIVE-shebang","#!/forbidden/interpreter\nfixture-data"u8.ToArray(),"native-fixture"),new("NATIVE-text","fixture-data"u8.ToArray(),"native-fixture"),new("NATIVE-empty",[],"native-fixture"),
        new("NATIVE-truncated-elf",[0x7f,69,76,70],"native-fixture"),new("NATIVE-truncated-pe",[77,90],"native-fixture"),
        new("NATIVE-interpreter-name",null,"python"),new("NATIVE-script-extension",null,"fixture.js"),new("NATIVE-hash",null,"native-fixture","hash"),
        new("NATIVE-credential",null,"native-fixture","credential"),new("NATIVE-workdir",null,"native-fixture","workdir"),new("NATIVE-receipt",null,"native-fixture","receipt")];
    public static IEnumerable<object[]> Cases=>All.Select(x=>new object[]{x.Id});
    public static ProcessAdmissionCase Get(string id)=>All.Single(x=>x.Id==id);
    public static ProcessRequest Request(ProcessAdmissionCase test,string root)
    {
        Directory.CreateDirectory(root);var bytes=test.Bytes??File.ReadAllBytes(Environment.ProcessPath!);var executable=Path.Combine(root,test.Name);File.WriteAllBytes(executable,bytes);
        var receipt=Path.Combine(root,"receipt");if(test.BadOption=="receipt"){Directory.CreateDirectory(receipt);File.WriteAllText(Path.Combine(receipt,"retained.txt"),"retained");}
        IReadOnlyDictionary<string,string> environment=test.BadOption=="credential"?new Dictionary<string,string>{{"API_KEY","fixture-only-not-a-secret"}}:new Dictionary<string,string>();
        return new(new(executable,test.BadOption=="hash"?"invalid-pin":ContentHash.Sha256(bytes),"fixture"),[],test.BadOption=="workdir"?Path.Combine(root,"missing"):root,environment,receipt);
    }
    public static async Task AssertContract(IProcessSessionFactory factory,ProcessRequest request)
    {
        var original=File.Exists(request.Tool.ExecutablePath)?await File.ReadAllBytesAsync(request.Tool.ExecutablePath):null;
        var error=await Record.ExceptionAsync(async()=>{await using var session=await factory.StartAsync(request);await session.StopAsync();});
        Assert.IsType<InvalidOperationException>(error);
        if(original is not null)Assert.Equal(original,await File.ReadAllBytesAsync(request.Tool.ExecutablePath));
        if(!Directory.Exists(request.ReceiptDirectory))return;
        Assert.Equal(new[]{"retained.txt"},Directory.GetFiles(request.ReceiptDirectory).Select(Path.GetFileName));
        Assert.Equal("retained",await File.ReadAllTextAsync(Path.Combine(request.ReceiptDirectory,"retained.txt")));
    }
}
