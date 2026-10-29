using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public static class ProcessSessionFixturePaths
{
    public static string NewRoot(string category)
    {
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null&&!File.Exists(Path.Combine(directory.FullName,"AndroidMcp.Build.csproj")))directory=directory.Parent;
        if(directory is null)throw new InvalidDataException("Native test workspace root is absent.");
        var root=Path.Combine(directory.FullName,"artifacts",category,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);return root;
    }
}
[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-003"),Trait("Requirement","TEST-BUILD-005")]
public sealed class ProcessSessionRealTests
{
    [Theory,MemberData(nameof(ProcessSessionCases.Cases),MemberType=typeof(ProcessSessionCases))]
    public async Task IdenticalLifecycleCasesBindToTheSingleNativeProcessEngine(string id)
    {
        var test=ProcessSessionCases.Get(id);var root=ProcessSessionFixturePaths.NewRoot("process-session-tests");var gate=Path.Combine(root,"release");
        var request=ProcessRunnerRealTests.Request(root,17,"wait-file",[gate]);
        await ProcessSessionCases.AssertContract(test,new NativeProcessSessionFactory(new SystemClock()),request,_=>File.WriteAllTextAsync(gate,"released"));
        Assert.True(File.Exists(Path.Combine(request.ReceiptDirectory,"stdout.bin")));Assert.True(File.Exists(Path.Combine(request.ReceiptDirectory,"stderr.bin")));
        if(test.Action=="natural")Assert.Equal("ready",await File.ReadAllTextAsync(Path.Combine(request.ReceiptDirectory,"stdout.bin")));
    }
    [Theory,MemberData(nameof(ProcessAdmissionCases.Cases),MemberType=typeof(ProcessAdmissionCases))]
    public async Task IdenticalNativeAdmissionCasesRejectScriptsAndPreflightFailuresBeforeEffects(string id)
    {
        var test=ProcessAdmissionCases.Get(id);var root=ProcessSessionFixturePaths.NewRoot("process-admission-tests");
        await ProcessAdmissionCases.AssertContract(new NativeProcessSessionFactory(new SystemClock()),ProcessAdmissionCases.Request(test,root));
    }
}
