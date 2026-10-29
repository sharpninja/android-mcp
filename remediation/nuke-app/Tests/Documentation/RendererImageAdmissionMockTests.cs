using System.Text;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed class RendererAdmissionEnvironment
{
    public string Scenario {get;}
    public DocumentationRendererProfile Profile {get;}
    public ArchitectureRendererContext Context {get;}=new(new("/fixture/docker",new string('a',64),"fixture"),"/fixture/owned work","/fixture/tools/plantuml.jar","/fixture/owned receipts","1000:1000");
    public Dictionary<string,ReadOnlyMemory<byte>> Files {get;}=new(StringComparer.Ordinal);
    public List<ProcessRequest> Requests {get;}=[];
    public List<ArchitectureRendererPhase> Phases {get;}=[];
    public List<string> Captures {get;}=[];
    public List<ProcessResult> Results {get;}=[];
    public CancellationTokenSource Cancellation {get;}=new();
    public RendererAdmissionEnvironment(string scenario)
    {
        Scenario=scenario;var original=RendererProfileCases.Original().Expected;
        foreach(var pin in original.RendererFiles)
        {
            var bytes=pin.RelativePath=="structurizr.sh"?File.ReadAllBytes(Path.Combine(DocumentationFixture.Current.Workspace,"remediation/nuke-planning/docs/fixtures/structurizr-cli-2025.11.09.wrapper.txt")):Encoding.UTF8.GetBytes("independently-authored-image-file-fixture:"+pin.RelativePath);
            if(pin.RelativePath=="structurizr.sh")Assert.Equal(pin.Sha256,DocumentationFixture.Hash(bytes));
            Files.Add(pin.RelativePath,bytes);
        }
        // Synthetic JAR bytes are deliberately distinct from locked real binaries. The wrapper is retained text.
        Profile=original with{RendererFiles=Files.Select(p=>new RendererFilePin(p.Key,DocumentationFixture.Hash(p.Value.Span))).ToArray()};
        if(scenario=="CANCEL-PREFLIGHT")Cancellation.Cancel();
    }
    public ProcessRequest ExpectedFileRequest(int index)
    {
        var pin=Profile.RendererFiles[index];
        return new(Context.Docker,new[]{"--host","unix:///var/run/docker.sock","--config",Context.WorkRoot+"/.docker-cli","run","--rm","--pull=never","--network","none","--cap-drop","ALL","--security-opt","no-new-privileges","--user",Context.UserIdentity,"--mount","type=bind,source="+Context.WorkRoot+",target=/work","--mount","type=bind,source="+Context.PlantUmlJar+",target=/tools/plantuml.jar,readonly","--workdir","/work","--entrypoint","cat",Profile.Image,"/usr/local/structurizr-cli/"+pin.RelativePath},Context.WorkRoot,new Dictionary<string,string>(),Context.ReceiptRoot+"/IdentityFile"+index,ClearEnvironment:true);
    }
    public ProcessResult Result(string name,int exitCode=0,bool cancelled=false)=>new(exitCode,"/fixture/capture/"+name+".stdout","/fixture/capture/"+name+".stderr",DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch,cancelled);
}
public sealed class RecordingAdmissionRunner(RendererAdmissionEnvironment e):IProcessRunner
{
    public Task<ProcessResult> RunAsync(ProcessRequest request,CancellationToken cancellationToken=default)
    {
        Assert.Equal(e.Cancellation.Token,cancellationToken);cancellationToken.ThrowIfCancellationRequested();e.Requests.Add(request);
        var result=e.Result("file"+(e.Requests.Count-1),e.Scenario=="PROCESS-FAIL"?17:0,e.Scenario=="PROCESS-CANCELLED");e.Results.Add(result);return Task.FromResult(result);
    }
}
public sealed class RecordingAdmissionTransport(RendererAdmissionEnvironment e):IArchitectureRendererTransport
{
    public Task<ProcessResult> ExecuteAsync(DocumentationRendererProfile profile,ArchitectureRendererContext context,ArchitectureRendererCommand command,CancellationToken cancellationToken=default)
    {
        Assert.Same(e.Profile,profile);Assert.Same(e.Context,context);Assert.Equal(e.Cancellation.Token,cancellationToken);Assert.Empty(command.SourcePaths);e.Phases.Add(command.Phase);
        var result=e.Result(command.Phase.ToString());e.Results.Add(result);return Task.FromResult(result);
    }
}
public sealed class RecordingAdmissionCapture(RendererAdmissionEnvironment e):IProcessCaptureReader
{
    public Task<ReadOnlyMemory<byte>> ReadAsync(ProcessResult result,bool standardError,CancellationToken cancellationToken=default)
    {
        Assert.Equal(e.Cancellation.Token,cancellationToken);var path=standardError?result.StandardErrorPath:result.StandardOutputPath;e.Captures.Add(path);
        if(path.Contains("JavaVersion",StringComparison.Ordinal))return Task.FromResult<ReadOnlyMemory<byte>>(Encoding.UTF8.GetBytes(e.Scenario=="JAVA-MISMATCH"?"openjdk version \"17.0.1\"":"openjdk version \"21.0.8\" 2025-07-15\nOpenJDK Runtime Environment"));
        if(path.Contains("FontProbe",StringComparison.Ordinal))return Task.FromResult<ReadOnlyMemory<byte>>(Encoding.UTF8.GetBytes(e.Scenario=="FONT-MISSING"?"LiberationSans-Regular.ttf: \"Liberation Sans\" \"Regular\"":"DejaVuSans.ttf: \"DejaVu Sans\" \"Book\""));
        var index=int.Parse(Path.GetFileName(path)[4..^7],System.Globalization.CultureInfo.InvariantCulture);
        if(e.Scenario=="CAPTURE-MISSING")throw new FileNotFoundException("Scripted missing binary capture");
        return Task.FromResult(e.Scenario=="FILE-HASH-"+index?(ReadOnlyMemory<byte>)new byte[]{0}:e.Files[e.Profile.RendererFiles[index].RelativePath]);
    }
}
public sealed class ScriptedRendererAdmission(RendererAdmissionEnvironment e,string mutation=""):IRendererImageAdmission
{
    public async Task<RendererImageAdmission> AdmitAsync(DocumentationRendererProfile profile,ArchitectureRendererContext context,CancellationToken cancellationToken=default)
    {
        Assert.Same(e.Profile,profile);Assert.Same(e.Context,context);Assert.Equal(e.Cancellation.Token,cancellationToken);cancellationToken.ThrowIfCancellationRequested();
        var runner=new RecordingAdmissionRunner(e);var capture=new RecordingAdmissionCapture(e);var transport=new RecordingAdmissionTransport(e);
        for(var index=0;index<profile.RendererFiles.Count;index++)
        {
            var request=e.ExpectedFileRequest(index);
            if(mutation=="inherited-environment")request=request with{ClearEnvironment=false};
            if(mutation=="remote-daemon"){var argv=request.Arguments.ToArray();argv[1]="tcp://remote.invalid:2376";request=request with{Arguments=argv};}
            if(mutation=="wrong-file"){var argv=request.Arguments.ToArray();argv[^1]="/other";request=request with{Arguments=argv};}
            var result=await runner.RunAsync(request,cancellationToken);
            if(result.Cancelled)throw new OperationCanceledException(cancellationToken);
            if(result.ExitCode!=0)throw new ArchitectureAssetException(mutation=="error-code"?"WRONG":"RENDERER_PROCESS","Image file capture failed");
            ReadOnlyMemory<byte> bytes;
            try{bytes=await capture.ReadAsync(result,mutation=="file-channel",cancellationToken);}catch(IOException error){throw new ArchitectureAssetException(mutation=="error-code"?"WRONG":"RENDERER_PIN",error.Message);}
            if(e.Scenario=="FILE-HASH-"+index&&mutation!="skip-hash")throw new ArchitectureAssetException(mutation=="error-code"?"WRONG":"RENDERER_PIN","Wrong image file bytes");
        }
        var java=await transport.ExecuteAsync(profile,context,new(ArchitectureRendererPhase.JavaVersion,[]),cancellationToken);await capture.ReadAsync(java,mutation!="java-channel",cancellationToken);
        if(e.Scenario=="JAVA-MISMATCH"&&mutation!="skip-java-check")throw new ArchitectureAssetException(mutation=="error-code"?"WRONG":"RENDERER_JAVA","Java prerequisite mismatch");
        var font=await transport.ExecuteAsync(profile,context,new(ArchitectureRendererPhase.FontProbe,[]),cancellationToken);await capture.ReadAsync(font,mutation=="font-channel",cancellationToken);
        if(e.Scenario=="FONT-MISSING"&&mutation!="skip-font-check")throw new ArchitectureAssetException(mutation=="error-code"?"WRONG":"RENDERER_FONT","DejaVu Sans prerequisite missing");
        return new(mutation=="image"?"wrong":profile.Image,mutation=="java"?"wrong":profile.JavaVersion,mutation=="font"?"wrong":"DejaVu Sans",mutation=="files"?[]:profile.RendererFiles,mutation=="processes"?[]:e.Results.ToArray());
    }
}
public static class RendererAdmissionAssertions
{
    public static async Task AssertCorrect(RendererAdmissionEnvironment e,IRendererImageAdmission provider)
    {
        RendererImageAdmission? value=null;var error=await Record.ExceptionAsync(async()=>value=await provider.AdmitAsync(e.Profile,e.Context,e.Cancellation.Token));
        if(e.Scenario is "CANCEL-PREFLIGHT" or "PROCESS-CANCELLED")Assert.IsAssignableFrom<OperationCanceledException>(error);
        else if(e.Scenario!="SUCCESS")Assert.Equal(e.Scenario=="PROCESS-FAIL"?"RENDERER_PROCESS":e.Scenario=="JAVA-MISMATCH"?"RENDERER_JAVA":e.Scenario=="FONT-MISSING"?"RENDERER_FONT":"RENDERER_PIN",Assert.IsType<ArchitectureAssetException>(error).Code);
        else {Assert.Null(error);Assert.NotNull(value);Assert.Equal(e.Profile.Image,value.Image);Assert.Equal(e.Profile.JavaVersion,value.JavaVersion);Assert.Equal("DejaVu Sans",value.FontFamily);Assert.Equal(e.Profile.RendererFiles,value.VerifiedFiles);Assert.Equal(e.Results,value.Processes);Assert.Equal(7,value.Processes.Count);}
        if(e.Scenario!="SUCCESS")Assert.Null(value);
        var expectedFiles=e.Scenario=="CANCEL-PREFLIGHT"?0:e.Scenario.StartsWith("FILE-HASH-",StringComparison.Ordinal)?int.Parse(e.Scenario[10..],System.Globalization.CultureInfo.InvariantCulture)+1:e.Scenario is "PROCESS-FAIL" or "PROCESS-CANCELLED" or "CAPTURE-MISSING"?1:5;
        Assert.Equal(expectedFiles,e.Requests.Count);
        for(var index=0;index<expectedFiles;index++)
        {
            var expected=e.ExpectedFileRequest(index);var actual=e.Requests[index];Assert.Equal(expected.Tool,actual.Tool);Assert.Equal(expected.Arguments,actual.Arguments);Assert.Equal(expected.WorkingDirectory,actual.WorkingDirectory);Assert.Equal(expected.ReceiptDirectory,actual.ReceiptDirectory);Assert.True(actual.ClearEnvironment);Assert.Empty(actual.PublicEnvironment);Assert.Null(actual.StandardInputPath);
        }
        Assert.Equal(e.Scenario=="JAVA-MISMATCH"?new[]{ArchitectureRendererPhase.JavaVersion}:e.Scenario is "SUCCESS" or "FONT-MISSING"?new[]{ArchitectureRendererPhase.JavaVersion,ArchitectureRendererPhase.FontProbe}:Array.Empty<ArchitectureRendererPhase>(),e.Phases);
        var expectedCaptures=e.Scenario=="CANCEL-PREFLIGHT"||e.Scenario is "PROCESS-FAIL" or "PROCESS-CANCELLED"?0:expectedFiles+e.Phases.Count;Assert.Equal(expectedCaptures,e.Captures.Count);
        var expectedPaths=new List<string>();
        if(expectedCaptures>0){expectedPaths.AddRange(Enumerable.Range(0,expectedFiles).Select(i=>e.Result("file"+i).StandardOutputPath));if(e.Phases.Contains(ArchitectureRendererPhase.JavaVersion))expectedPaths.Add(e.Result("JavaVersion").StandardErrorPath);if(e.Phases.Contains(ArchitectureRendererPhase.FontProbe))expectedPaths.Add(e.Result("FontProbe").StandardOutputPath);}
        Assert.Equal(expectedPaths,e.Captures);
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class RendererImageAdmissionMockTests
{
    [Theory]
    [InlineData("SUCCESS")][InlineData("FILE-HASH-0")][InlineData("FILE-HASH-1")][InlineData("FILE-HASH-2")][InlineData("FILE-HASH-3")][InlineData("FILE-HASH-4")][InlineData("PROCESS-FAIL")][InlineData("PROCESS-CANCELLED")][InlineData("CAPTURE-MISSING")][InlineData("JAVA-MISMATCH")][InlineData("FONT-MISSING")][InlineData("CANCEL-PREFLIGHT")]
    public Task CorrectImageAdmissionMocksPass(string scenario){var e=new RendererAdmissionEnvironment(scenario);return RendererAdmissionAssertions.AssertCorrect(e,new ScriptedRendererAdmission(e));}
    [Theory]
    [InlineData("SUCCESS","image")][InlineData("SUCCESS","java")][InlineData("SUCCESS","font")][InlineData("SUCCESS","files")][InlineData("SUCCESS","processes")][InlineData("SUCCESS","remote-daemon")][InlineData("SUCCESS","inherited-environment")][InlineData("SUCCESS","wrong-file")]
    [InlineData("FILE-HASH-0","skip-hash")][InlineData("FILE-HASH-1","skip-hash")][InlineData("FILE-HASH-2","skip-hash")][InlineData("FILE-HASH-3","skip-hash")][InlineData("FILE-HASH-4","skip-hash")][InlineData("FILE-HASH-0","error-code")][InlineData("PROCESS-FAIL","error-code")]
    [InlineData("SUCCESS","file-channel")][InlineData("SUCCESS","java-channel")][InlineData("SUCCESS","font-channel")][InlineData("JAVA-MISMATCH","skip-java-check")][InlineData("FONT-MISSING","skip-font-check")]
    [InlineData("CAPTURE-MISSING","error-code")][InlineData("JAVA-MISMATCH","error-code")][InlineData("FONT-MISSING","error-code")]
    public async Task IndividualAdmissionDriftIsRejected(string scenario,string mutation){var e=new RendererAdmissionEnvironment(scenario);Assert.NotNull(await Record.ExceptionAsync(()=>RendererAdmissionAssertions.AssertCorrect(e,new ScriptedRendererAdmission(e,mutation))));}
}
