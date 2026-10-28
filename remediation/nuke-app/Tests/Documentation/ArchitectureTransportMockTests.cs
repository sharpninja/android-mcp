using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record ArchitectureTransportCase(string Id,ArchitectureRendererCommand Command,int ExitCode,bool Cancelled,string Error)
{public override string ToString()=>Id;}
public sealed class ArchitectureTransportEnvironment
{
    public DocumentationRendererProfile Profile {get;}=RendererProfileCases.Original().Expected;
    public ArchitectureRendererContext Context {get;}=new(new("/usr/bin/docker",new string('a',64),"fixture-docker"),"/fixture/root with spaces","/fixture/tools/plantuml.jar","/fixture/receipts","1000:1000");
    public List<ProcessRequest> Requests {get;}=[];
    public ArchitectureTransportCase Test {get;}
    public ArchitectureTransportEnvironment(ArchitectureTransportCase test){Test=test;}
    public ProcessResult Result=>new(Test.ExitCode,"/fixture/stdout.bin","/fixture/stderr.bin",DateTimeOffset.Parse("2026-10-27T00:00:00Z"),DateTimeOffset.Parse("2026-10-27T00:00:01Z"),Test.Cancelled);
    public ProcessRequest ExpectedRequest()
    {
        var args=new List<string>{"run","--rm","--pull=never","--network","none","--cap-drop","ALL","--security-opt","no-new-privileges","--user","1000:1000","--mount","type=bind,source=/fixture/root with spaces,target=/work","--mount","type=bind,source=/fixture/tools/plantuml.jar,target=/tools/plantuml.jar,readonly","--workdir","/work","--entrypoint",Test.Command.Phase==ArchitectureRendererPhase.FontProbe?"fc-match":"java",Profile.Image};
        switch(Test.Command.Phase)
        {
            case ArchitectureRendererPhase.JavaVersion:args.Add("-version");break;
            case ArchitectureRendererPhase.FontProbe:args.Add("DejaVu Sans");break;
            case ArchitectureRendererPhase.RenderCode:
            case ArchitectureRendererPhase.RenderNative:
                args.AddRange(["-Djava.awt.headless=true","-jar","/tools/plantuml.jar","-tsvg","-nometadata","-charset","UTF-8"]);args.AddRange(Test.Command.SourcePaths.Select(p=>"/work/"+p));break;
            case ArchitectureRendererPhase.ValidateWorkspace:
                args.AddRange(["-cp","/usr/local/structurizr-cli:/usr/local/structurizr-cli/lib/*","com.structurizr.cli.StructurizrCliApplication","validate","-workspace","/work/architecture/workspace.dsl"]);break;
            case ArchitectureRendererPhase.ExportWorkspace:
                args.AddRange(["-cp","/usr/local/structurizr-cli:/usr/local/structurizr-cli/lib/*","com.structurizr.cli.StructurizrCliApplication","export","-workspace","/work/architecture/workspace.dsl","-format","plantuml","-output","/work/build/structurizr-export"]);break;
            default:throw new ArgumentOutOfRangeException();
        }
        return new(Context.Docker,args,Context.WorkRoot,new Dictionary<string,string>(),Context.ReceiptRoot+"/"+Test.Command.Phase);
    }
}
public sealed class RecordingArchitectureRunner(ArchitectureTransportEnvironment environment):IProcessRunner
{
    public Task<ProcessResult> RunAsync(ProcessRequest request,CancellationToken cancellationToken=default)
    {cancellationToken.ThrowIfCancellationRequested();environment.Requests.Add(request);return Task.FromResult(environment.Result);}
}
public static class ArchitectureTransportCases
{
    public static IEnumerable<object[]> Data=>Enum.GetValues<ArchitectureRendererPhase>().Select(phase=>new object[]{new ArchitectureTransportCase(phase.ToString(),new(phase,phase switch
    {ArchitectureRendererPhase.RenderCode=>["build/code-views/a.puml","build/code-views/b.puml"],ArchitectureRendererPhase.RenderNative=>["build/structurizr-profiled/structurizr-A.puml"],_=>[]}),0,false,"")})
        .Concat(new object[][]
        {
            [new ArchitectureTransportCase("PROCESS-FAIL",new(ArchitectureRendererPhase.ValidateWorkspace,[]),9,false,"RENDERER_PROCESS")],
            [new ArchitectureTransportCase("PROCESS-CANCELLED",new(ArchitectureRendererPhase.ValidateWorkspace,[]),0,true,"CANCELLED")],
            [new ArchitectureTransportCase("SOURCE-TRAVERSAL",new(ArchitectureRendererPhase.RenderCode,["../escape.puml"]),0,false,"RENDERER_ARGUMENT")],
            [new ArchitectureTransportCase("SOURCE-TYPE",new(ArchitectureRendererPhase.RenderCode,["build/code-views/a.svg"]),0,false,"RENDERER_ARGUMENT")],
            [new ArchitectureTransportCase("SOURCE-MISSING",new(ArchitectureRendererPhase.RenderCode,[]),0,false,"RENDERER_ARGUMENT")],
            [new ArchitectureTransportCase("SOURCE-PHASE",new(ArchitectureRendererPhase.ValidateWorkspace,["build/code-views/a.puml"]),0,false,"RENDERER_ARGUMENT")]
        });
    public static async Task AssertContract(ArchitectureTransportCase test,ArchitectureTransportEnvironment environment,IArchitectureRendererTransport provider)
    {
        ProcessResult? result=null;var error=await Record.ExceptionAsync(async()=>result=await provider.ExecuteAsync(environment.Profile,environment.Context,test.Command));
        if(test.Error=="CANCELLED")Assert.IsAssignableFrom<OperationCanceledException>(error);
        else if(test.Error.Length>0)Assert.Equal(test.Error,Assert.IsType<ArchitectureAssetException>(error).Code);
        else {Assert.Null(error);Assert.Equal(environment.Result,result);}
        if(test.Error=="RENDERER_ARGUMENT"){Assert.Empty(environment.Requests);return;}
        var request=Assert.Single(environment.Requests);var expected=environment.ExpectedRequest();
        Assert.Equal(expected.Tool,request.Tool);Assert.Equal(expected.Arguments,request.Arguments);Assert.Equal(expected.WorkingDirectory,request.WorkingDirectory);
        Assert.Empty(request.PublicEnvironment);Assert.Equal(expected.ReceiptDirectory,request.ReceiptDirectory);Assert.Null(request.StandardInputPath);
    }
}
public sealed class ScriptedArchitectureTransport(ArchitectureTransportEnvironment environment,string mutation=""):IArchitectureRendererTransport
{
    public async Task<ProcessResult> ExecuteAsync(DocumentationRendererProfile profile,ArchitectureRendererContext context,ArchitectureRendererCommand command,CancellationToken cancellationToken=default)
    {
        Assert.Same(environment.Profile,profile);Assert.Same(environment.Context,context);Assert.Same(environment.Test.Command,command);
        var request=environment.ExpectedRequest();var args=request.Arguments.ToList();
        if(environment.Test.Error=="RENDERER_ARGUMENT")
        {if(mutation=="argument-run")await new RecordingArchitectureRunner(environment).RunAsync(request,cancellationToken);throw new ArchitectureAssetException(mutation=="outcome"?"WRONG":"RENDERER_ARGUMENT","Scripted argument rejection");}
        if(mutation=="tool")request=request with{Tool=request.Tool with{Sha256="wrong"}};
        else if(mutation=="cwd")request=request with{WorkingDirectory="wrong"};
        else if(mutation=="environment")request=request with{PublicEnvironment=new Dictionary<string,string>{{"EXTRA","wrong"}}};
        else if(mutation=="receipt")request=request with{ReceiptDirectory="wrong"};
        else if(mutation=="stdin")request=request with{StandardInputPath="wrong"};
        else if(mutation.StartsWith("argv-",StringComparison.Ordinal))
        {var index=int.Parse(mutation[5..],System.Globalization.CultureInfo.InvariantCulture);args[index]="wrong";request=request with{Arguments=args};}
        var result=await new RecordingArchitectureRunner(environment).RunAsync(request,cancellationToken);
        if(mutation.StartsWith("result-",StringComparison.Ordinal))return mutation switch
        {"result-exit"=>result with{ExitCode=4},"result-output"=>result with{StandardOutputPath="wrong"},"result-error"=>result with{StandardErrorPath="wrong"},"result-start"=>result with{StartedAt=result.StartedAt.AddSeconds(-1)},"result-end"=>result with{EndedAt=result.EndedAt.AddSeconds(1)},_=>result with{Cancelled=true}};
        if(mutation=="outcome")throw new ArchitectureAssetException("WRONG","Wrong scripted outcome");
        if(result.Cancelled)throw new OperationCanceledException();if(result.ExitCode!=0)throw new ArchitectureAssetException("RENDERER_PROCESS","Failed scripted renderer");return result;
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class ArchitectureTransportMockTests
{
    [Theory,MemberData(nameof(ArchitectureTransportCases.Data),MemberType=typeof(ArchitectureTransportCases))]
    public Task CorrectProcessTransportMocksPass(ArchitectureTransportCase test)
    {var environment=new ArchitectureTransportEnvironment(test);return ArchitectureTransportCases.AssertContract(test,environment,new ScriptedArchitectureTransport(environment));}
    [Theory,MemberData(nameof(ArchitectureTransportCases.Data),MemberType=typeof(ArchitectureTransportCases))]
    public async Task WrongProcessOutcomeRejected(ArchitectureTransportCase test)
    {var environment=new ArchitectureTransportEnvironment(test);Assert.NotNull(await Record.ExceptionAsync(()=>ArchitectureTransportCases.AssertContract(test,environment,new ScriptedArchitectureTransport(environment,"outcome"))));}
    public static IEnumerable<object[]> ArgumentControls=>ArchitectureTransportCases.Data.Select(row=>(ArchitectureTransportCase)row[0]).Where(test=>test.Error.Length==0).SelectMany(test=>new ArchitectureTransportEnvironment(test).ExpectedRequest().Arguments.Select((_,index)=>new object[]{test,"argv-"+index}));
    [Theory,MemberData(nameof(ArgumentControls))]
    public async Task EachArgvBoundaryRejectsItsOwnWrongResponse(ArchitectureTransportCase test,string mutation)
    {var environment=new ArchitectureTransportEnvironment(test);Assert.NotNull(await Record.ExceptionAsync(()=>ArchitectureTransportCases.AssertContract(test,environment,new ScriptedArchitectureTransport(environment,mutation))));}
    [Theory]
    [InlineData("tool")][InlineData("cwd")][InlineData("environment")][InlineData("receipt")][InlineData("stdin")]
    [InlineData("result-exit")][InlineData("result-output")][InlineData("result-error")][InlineData("result-start")][InlineData("result-end")][InlineData("result-cancelled")]
    public async Task EachRequestAndReceiptFieldRejectsWrongResponse(string mutation)
    {
        var test=(ArchitectureTransportCase)ArchitectureTransportCases.Data.First()[0];var environment=new ArchitectureTransportEnvironment(test);
        Assert.NotNull(await Record.ExceptionAsync(()=>ArchitectureTransportCases.AssertContract(test,environment,new ScriptedArchitectureTransport(environment,mutation))));
    }
    [Theory]
    [InlineData("SOURCE-TRAVERSAL")][InlineData("SOURCE-TYPE")][InlineData("SOURCE-MISSING")][InlineData("SOURCE-PHASE")]
    public async Task PreflightMustRejectBeforeProcess(string id)
    {var test=ArchitectureTransportCases.Data.Select(row=>(ArchitectureTransportCase)row[0]).Single(c=>c.Id==id);var environment=new ArchitectureTransportEnvironment(test);Assert.NotNull(await Record.ExceptionAsync(()=>ArchitectureTransportCases.AssertContract(test,environment,new ScriptedArchitectureTransport(environment,"argument-run"))));}
}
