using AndroidMcp.BuildTool.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;
[Trait("Gate","Real")]
public sealed class ProcessRunnerRealTests
{
    private static string Host=>Environment.ProcessPath!;
    internal static ProcessRequest Request(string root,int exit,string mode,IEnumerable<string> arguments,string? input=null)
    {
        var directory=AppContext.BaseDirectory;
        var argv=new List<string>{"exec","--runtimeconfig",Path.Combine(directory,"AndroidMcp.Build.Tests.runtimeconfig.json"),
            "--depsfile",Path.Combine(directory,"AndroidMcp.Build.Tests.deps.json"),typeof(Build).Assembly.Location,
            "--internal-process-probe",exit.ToString(System.Globalization.CultureInfo.InvariantCulture),mode};
        argv.AddRange(arguments);
        return new(new(Host,ContentHash.Sha256(File.ReadAllBytes(Host)),Environment.Version.ToString()),argv,directory,new Dictionary<string,string>(),Path.Combine(root,"receipt"),input);
    }
    [Theory,MemberData(nameof(ProcessRunnerCases.Cases),MemberType=typeof(ProcessRunnerCases))]
    public async Task SameContractAgainstNativeProcess(ProcessCase test)
    {
        var root=Path.Combine(Path.GetTempPath(),"android-mcp-nuke-process",Guid.NewGuid().ToString("N"));
        var request=Request(root,test.ExitCode,"arguments",test.Arguments);
        if(test.Error is not null)request=request with{Tool=request.Tool with{Sha256="invalid-pin"}};
        await ProcessRunnerCases.AssertContract(test,new NativeProcessRunner(new SystemClock()),request);
        if(test.Error is null && !test.Cancelled)
            Assert.Equal(test.Arguments,System.Text.Json.JsonSerializer.Deserialize<string[]>(await File.ReadAllBytesAsync(Path.Combine(root,"receipt/stdout.bin"))));
    }
    [Theory,MemberData(nameof(ProcessCaptureCases.Cases),MemberType=typeof(ProcessCaptureCases))]
    public async Task SameBinaryCaptureContractAcrossRealPipes(int length)
    {
        var root=Path.Combine(Path.GetTempPath(),"android-mcp-nuke-capture",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var input=Path.Combine(root,"stdin.bin");await File.WriteAllBytesAsync(input,ProcessCaptureCases.Payload(length));
        var result=await new NativeProcessRunner(new SystemClock()).RunAsync(Request(root,17,"copy",[],input));
        Assert.Equal(17,result.ExitCode);
        await ProcessCaptureCases.AssertContract(length,new ProcessCaptureReader(root),result);
    }
}
