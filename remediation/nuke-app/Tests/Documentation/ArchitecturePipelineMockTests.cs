using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed class ArchitecturePipelineEnvironment
{
    public string Scenario {get;}
    public static readonly string[] NativeKeys=["ArcComponents","ArcSession","CallbackFlow","Containers","Context","CredentialRevocation","Deployment","ExecutionComponents","GroupFlow","InvocationFlow","OwnerTransition","ServerComponents","ServerCoreComponents","Shutdown","Startup"];
    public CompositionEnvironment Composition {get;}=new("ARCH-ONLINE");
    public List<string> Trace {get;}=[];
    public Dictionary<string,Artifact> Work {get;}=new(StringComparer.Ordinal);
    public List<Artifact> StoreWrites {get;}=[];
    public List<ArchitectureRendererCommand> Commands {get;}=[];
    public List<(Artifact Raw,string OutputPath)> Normalizations {get;}=[];
    public CancellationTokenSource Cancellation {get;}=new();
    public const string WorkGeneration="fixture-architecture-work";
    public Dictionary<string,Artifact> ExpectedWork {get;}
    public IReadOnlyList<ArtifactSourceSpec> ExpectedSources {get;}
    public IArtifactReceiptVerifier Receipts {get;}=new ArtifactReceiptVerifier(new NativeContentHash());
    public ArchitecturePipelineEnvironment(string scenario="SUCCESS")
    {
        Scenario=scenario;
        var inputs=Composition.Inputs;var code=Composition.CodeViews;var final=Composition.Output.Where(a=>a.MediaType=="image/svg+xml").ToArray();
        ExpectedSources=inputs.Select(a=>new ArtifactSourceSpec(a.RelativePath,a.MediaType,Receipts.Describe(a))).ToArray();
        var work=inputs.Concat(code).Concat(final).ToList();
        work.Add(new(".docker-cli/config.json",JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string,string>()),"application/json"));
        foreach(var source in code)work.Add(Text(source.RelativePath[..^5]+".svg","raw-svg:"+source.RelativePath,"image/svg+xml"));
        foreach(var key in NativeKeys)
        {
            var raw="@startuml\nclass "+key+"\n@enduml\n";
            work.Add(Text("build/structurizr-export/structurizr-"+key+".puml",raw,"text/vnd.plantuml"));
            work.Add(Text("build/structurizr-profiled/structurizr-"+key+".puml",raw.Replace("@startuml","@startuml\n!include ../../architecture/render-skin.puml",StringComparison.Ordinal),"text/vnd.plantuml"));
            work.Add(Text("build/structurizr-profiled/structurizr-"+key+".svg","raw-svg:"+key,"image/svg+xml"));
        }
        ExpectedWork=work.ToDictionary(a=>a.RelativePath,StringComparer.Ordinal);Assert.Equal(85,ExpectedWork.Count);Assert.Equal(21,final.Length);
    }
    public static Artifact Text(string path,string text,string media)=>new(path,Encoding.UTF8.GetBytes(text),media);
}
public sealed class RecordingPipelineStore(ArchitecturePipelineEnvironment e):IArtifactStore
{
    public Task<ReadOnlyMemory<byte>> ReadAsync(string path,CancellationToken cancellationToken=default)
    {cancellationToken.ThrowIfCancellationRequested();e.Trace.Add("read:"+path);return e.Work.TryGetValue(path,out var artifact)?Task.FromResult(artifact.Bytes):Task.FromException<ReadOnlyMemory<byte>>(new FileNotFoundException(path));}
    public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();e.Trace.Add("write:"+artifact.RelativePath);
        if(e.Scenario=="SOURCE-WRITE-FAIL"&&e.StoreWrites.Count==0)throw new IOException("Scripted source write failure");
        Assert.True(e.Trace.IndexOf("work-preflight")>=0,"No source/config write before common-runtime preflight");
        if(e.Work.TryGetValue(artifact.RelativePath,out var previous))Assert.Equal(previous.Bytes.ToArray(),artifact.Bytes.ToArray());
        else e.Work.Add(artifact.RelativePath,artifact);e.StoreWrites.Add(artifact);return Task.FromResult(e.Receipts.Describe(artifact));
    }
}
public sealed class RecordingPipelineAdmission(ArchitecturePipelineEnvironment e):IRendererImageAdmission
{
    public Task<RendererImageAdmission> AdmitAsync(DocumentationRendererProfile profile,ArchitectureRendererContext context,CancellationToken cancellationToken=default)
    {e.Trace.Add("admit");Assert.Same(e.Composition.Profile,profile);Assert.Same(e.Composition.ArchitectureRequest.Renderer,context);Assert.Equal(e.Cancellation.Token,cancellationToken);if(e.Scenario=="ADMISSION-FAIL")throw new ArchitectureAssetException("RENDERER_PIN","Scripted image admission failure");return Task.FromResult(new RendererImageAdmission(profile.Image,profile.JavaVersion,"DejaVu Sans",profile.RendererFiles,[]));}
}
public sealed class RecordingPipelinePreparation(ArchitecturePipelineEnvironment e):IArchitectureAssetPreparation
{
    public IReadOnlyList<Artifact> ExtractCodeViews(ArchitecturePreparationInput input)=>throw new NotSupportedException("Already prepared once by recipe");
    public Artifact NormalizeVector(Artifact raw,string outputPath)
    {
        e.Trace.Add("normalize:"+outputPath);e.Normalizations.Add((raw,outputPath));Assert.Equal(e.ExpectedWork[raw.RelativePath].Bytes.ToArray(),raw.Bytes.ToArray());
        return e.Composition.Output.Single(a=>a.RelativePath==outputPath);
    }
}
public sealed class RecordingPipelineTransport(ArchitecturePipelineEnvironment e):IArchitectureRendererTransport
{
    public Task<ProcessResult> ExecuteAsync(DocumentationRendererProfile profile,ArchitectureRendererContext context,ArchitectureRendererCommand command,CancellationToken cancellationToken=default)
    {
        e.Trace.Add("tool:"+command.Phase);e.Commands.Add(command);Assert.Same(e.Composition.Profile,profile);Assert.Same(e.Composition.ArchitectureRequest.Renderer,context);Assert.Equal(e.Cancellation.Token,cancellationToken);
        if(e.Scenario=="FAIL-"+command.Phase)throw new ArchitectureAssetException("RENDERER_PROCESS","Scripted renderer process failure");
        var paths=command.Phase switch
        {
            ArchitectureRendererPhase.RenderCode=>e.Composition.CodeViews.Select(a=>a.RelativePath[..^5]+".svg"),
            ArchitectureRendererPhase.ExportWorkspace=>ArchitecturePipelineEnvironment.NativeKeys.Select(k=>"build/structurizr-export/structurizr-"+k+".puml"),
            ArchitectureRendererPhase.RenderNative=>ArchitecturePipelineEnvironment.NativeKeys.Select(k=>"build/structurizr-profiled/structurizr-"+k+".svg"),
            _=>[]
        };
        var emitted=paths.ToArray();
        if((e.Scenario=="MISSING-CODE-SVG"&&command.Phase==ArchitectureRendererPhase.RenderCode)||(e.Scenario=="MISSING-EXPORT"&&command.Phase==ArchitectureRendererPhase.ExportWorkspace)||(e.Scenario=="MISSING-NATIVE-SVG"&&command.Phase==ArchitectureRendererPhase.RenderNative))emitted=emitted.Skip(1).ToArray();
        foreach(var path in emitted)e.Work[path]=e.ExpectedWork[path];
        return Task.FromResult(new ProcessResult(0,"stdout","stderr",DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch,false));
    }
}
public sealed class RecordingPipelineRuntime(ArchitecturePipelineEnvironment e):IArtifactGenerationRuntime
{
    public int Calls {get;private set;}
    public async Task<ArtifactGenerationResult> ExecuteAsync(string generationId,ArtifactGenerationRecipe recipe,CancellationToken cancellationToken=default)
    {
        Calls++;Assert.Equal(ArchitecturePipelineEnvironment.WorkGeneration,generationId);Assert.Equal(e.Cancellation.Token,cancellationToken);Assert.Equal(e.ExpectedSources,recipe.Sources);
        var preparation=recipe.Prepare(e.Composition.Inputs);Assert.Equal(e.ExpectedWork.Keys.Order(StringComparer.Ordinal),preparation.OutputPaths.Order(StringComparer.Ordinal));Assert.Empty(e.StoreWrites);
        if(e.Scenario=="WORK-COLLISION")throw new ArtifactGenerationException("OUTPUT_EXISTS","Scripted occupied work generation");
        e.Trace.Add("work-preflight");IReadOnlyList<Artifact> artifacts;
        try{artifacts=await preparation.MaterializeAsync(cancellationToken);}catch(OperationCanceledException){throw;}catch(Exception error){throw new ArtifactGenerationException("PROJECTION_FAILED","Scripted materialization failure",error);}
        Assert.Equal(e.ExpectedWork.Keys.Order(StringComparer.Ordinal),artifacts.Select(a=>a.RelativePath).Order(StringComparer.Ordinal));
        if(e.Scenario=="WORK-COMMIT-FAIL")throw new ArtifactGenerationException("WRITE_FAILED","Scripted commit failure");
        var store=new RecordingPipelineStore(e);foreach(var artifact in artifacts)await store.WriteAsync(artifact,cancellationToken);
        var outputs=artifacts.Select(e.Receipts.Describe).ToArray();e.Trace.Add("work-commit");
        return new(generationId,e.Composition.Inputs.Select(e.Receipts.Describe).ToArray(),outputs,new("generation-manifest.json",new string('a',64),1,"application/json"));
    }
}
public sealed class ScriptedArchitecturePipeline(ArchitecturePipelineEnvironment e,string mutation=""):IArchitectureProjectionComposer
{
    public readonly RecordingPipelineRuntime Runtime=new(e);
    public readonly RecordingPipelineTransport Transport=new(e);
    public ArchitectureCompositionPlan Plan(IReadOnlyList<Artifact> inputs,DocumentationRendererProfile profile,IReadOnlyList<Artifact> codeViews)=>new(inputs,profile,codeViews,e.Composition.Output.Select(a=>a.RelativePath).ToArray());
    public async Task<IReadOnlyList<Artifact>> RenderAsync(ArchitectureCompositionPlan plan,ArchitectureRendererContext context,IReadOnlyList<AndroidMcp.BuildTool.Modules.Sources.Acquisition.ToolArtifactReceipt> tools,CancellationToken cancellationToken=default)
    {
        Assert.Same(e.Composition.Tools,tools);var store=new RecordingPipelineStore(e);var normalize=new RecordingPipelinePreparation(e);var admission=new RecordingPipelineAdmission(e);
        if(mutation=="early-effects")await admission.AdmitAsync(plan.Profile,context,cancellationToken);
        var recipe=new ArtifactGenerationRecipe(e.ExpectedSources,inputs=>new(e.ExpectedWork.Keys.ToArray(),async token=>
        {
            foreach(var source in plan.Inputs.Concat(plan.CodeViews))await store.WriteAsync(source,token);
            await store.WriteAsync(e.ExpectedWork[".docker-cli/config.json"],token);
            if(mutation!="skip-admission")await admission.AdmitAsync(plan.Profile,context,token);
            await Transport.ExecuteAsync(plan.Profile,context,new(ArchitectureRendererPhase.RenderCode,mutation=="code-source"?new[]{"wrong"}:plan.CodeViews.Select(a=>a.RelativePath).ToArray()),token);
            foreach(var code in plan.CodeViews){var raw=e.ExpectedWork[code.RelativePath[..^5]+".svg"];await store.WriteAsync(normalize.NormalizeVector(new(raw.RelativePath,await store.ReadAsync(raw.RelativePath,token),raw.MediaType),"docs/diagrams/"+Path.GetFileNameWithoutExtension(code.RelativePath)+".svg"),token);}
            await Transport.ExecuteAsync(plan.Profile,context,new(ArchitectureRendererPhase.ValidateWorkspace,[]),token);
            await Transport.ExecuteAsync(plan.Profile,context,new(ArchitectureRendererPhase.ExportWorkspace,[]),token);
            foreach(var key in ArchitecturePipelineEnvironment.NativeKeys){var raw="build/structurizr-export/structurizr-"+key+".puml";Assert.Equal(e.ExpectedWork[raw].Bytes.ToArray(),(await store.ReadAsync(raw,token)).ToArray());var path="build/structurizr-profiled/structurizr-"+key+".puml";await store.WriteAsync(e.ExpectedWork[path],token);}
            await Transport.ExecuteAsync(plan.Profile,context,new(ArchitectureRendererPhase.RenderNative,mutation=="native-source"?new[]{"wrong"}:ArchitecturePipelineEnvironment.NativeKeys.Select(k=>"build/structurizr-profiled/structurizr-"+k+".puml").ToArray()),token);
            foreach(var key in ArchitecturePipelineEnvironment.NativeKeys){var path="build/structurizr-profiled/structurizr-"+key+".svg";var raw=new Artifact(path,await store.ReadAsync(path,token),"image/svg+xml");await store.WriteAsync(normalize.NormalizeVector(raw,"docs/diagrams/structurizr-"+key+".svg"),token);}
            return e.Work.Values.ToArray();
        }));
        try {await Runtime.ExecuteAsync(ArchitecturePipelineEnvironment.WorkGeneration,recipe,cancellationToken);}
        catch(ArtifactGenerationException error) when(mutation=="error-code") {throw new ArtifactGenerationException("WRONG",error.Message,error);}
        catch(ArtifactGenerationException) when(mutation=="swallow-error") {return e.Composition.Output;}
        var result=e.Composition.Output.ToArray();if(mutation=="missing")return result.Skip(1).ToArray();
        if(mutation=="bytes")result[0]=result[0] with{Bytes=new byte[]{0}};if(mutation=="media")result[0]=result[0] with{MediaType="wrong"};if(mutation=="path")result[0]=result[0] with{RelativePath="wrong"};return result;
    }
}
public static class ArchitecturePipelineAssertions
{
    public static async Task AssertCorrect(ArchitecturePipelineEnvironment e,IArchitectureProjectionComposer provider)
    {
        var before=e.Composition.Inputs.Select(a=>a.Bytes.ToArray()).ToArray();
        var plan=provider.Plan(e.Composition.Inputs,e.Composition.Profile,e.Composition.CodeViews);Assert.Same(e.Composition.Inputs,plan.Inputs);Assert.Same(e.Composition.Profile,plan.Profile);Assert.Same(e.Composition.CodeViews,plan.CodeViews);
        Assert.Equal(e.Composition.Output.Select(a=>a.RelativePath).Order(StringComparer.Ordinal),plan.OutputPaths.Order(StringComparer.Ordinal));
        IReadOnlyList<Artifact>? result=null;var error=await Record.ExceptionAsync(async()=>result=await provider.RenderAsync(plan,e.Composition.ArchitectureRequest.Renderer,e.Composition.Tools,e.Cancellation.Token));
        for(var index=0;index<before.Length;index++)Assert.Equal(before[index],e.Composition.Inputs[index].Bytes.ToArray());
        if(e.Scenario!="SUCCESS")
        {
            var typed=Assert.IsType<ArtifactGenerationException>(error);Assert.Equal(e.Scenario=="WORK-COLLISION"?"OUTPUT_EXISTS":e.Scenario=="WORK-COMMIT-FAIL"?"WRITE_FAILED":"PROJECTION_FAILED",typed.Code);
            Assert.Null(result);Assert.DoesNotContain("work-commit",e.Trace);
            var phase=e.Scenario switch{"SOURCE-WRITE-FAIL" or "ADMISSION-FAIL" or "WORK-COLLISION"=>Array.Empty<string>(),"FAIL-RenderCode" or "MISSING-CODE-SVG"=>new[]{"tool:RenderCode"},"FAIL-ValidateWorkspace"=>new[]{"tool:RenderCode","tool:ValidateWorkspace"},"FAIL-ExportWorkspace" or "MISSING-EXPORT"=>new[]{"tool:RenderCode","tool:ValidateWorkspace","tool:ExportWorkspace"},_=>new[]{"tool:RenderCode","tool:ValidateWorkspace","tool:ExportWorkspace","tool:RenderNative"}};
            Assert.Equal(phase,e.Trace.Where(t=>t.StartsWith("tool:",StringComparison.Ordinal)));
            if(e.Scenario=="WORK-COLLISION"){Assert.Empty(e.StoreWrites);Assert.DoesNotContain("admit",e.Trace);}else if(e.Scenario!="SOURCE-WRITE-FAIL")Assert.NotEmpty(e.Work);
            return;
        }
        Assert.Null(error);
        Assert.Equal(e.Composition.Output.OrderBy(a=>a.RelativePath,StringComparer.Ordinal),result!.OrderBy(a=>a.RelativePath,StringComparer.Ordinal));
        Assert.Equal(1,e.Trace.Count(t=>t=="admit"));Assert.True(e.Trace.IndexOf("admit")>e.Trace.IndexOf("work-preflight"));
        Assert.Equal(new[]{"tool:RenderCode","tool:ValidateWorkspace","tool:ExportWorkspace","tool:RenderNative"},e.Trace.Where(t=>t.StartsWith("tool:",StringComparison.Ordinal)));
        Assert.Equal(e.Composition.CodeViews.Select(a=>a.RelativePath),e.Commands[0].SourcePaths);Assert.Empty(e.Commands[1].SourcePaths);Assert.Empty(e.Commands[2].SourcePaths);
        Assert.Equal(ArchitecturePipelineEnvironment.NativeKeys.Select(k=>"build/structurizr-profiled/structurizr-"+k+".puml"),e.Commands[3].SourcePaths);
        Assert.Equal(21,e.Trace.Count(t=>t.StartsWith("normalize:",StringComparison.Ordinal)));Assert.Contains("work-commit",e.Trace);
        var configIndex=e.Trace.IndexOf("write:.docker-cli/config.json");Assert.True(configIndex>e.Trace.IndexOf("work-preflight"));Assert.True(configIndex<e.Trace.IndexOf("admit"));
        foreach(var source in e.Composition.Inputs.Concat(e.Composition.CodeViews))Assert.True(e.Trace.IndexOf("write:"+source.RelativePath)<e.Trace.IndexOf("admit"));
        foreach(var key in ArchitecturePipelineEnvironment.NativeKeys)
        {
            var raw="build/structurizr-export/structurizr-"+key+".puml";
            Assert.True(e.Trace.IndexOf("read:"+raw)>e.Trace.IndexOf("tool:ExportWorkspace"));
            Assert.True(e.Trace.IndexOf("read:"+raw)<e.Trace.IndexOf("tool:RenderNative"));
        }
        foreach(var expected in e.ExpectedWork){Assert.True(e.Work.ContainsKey(expected.Key));Assert.Equal(expected.Value.Bytes.ToArray(),e.Work[expected.Key].Bytes.ToArray());}
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class ArchitecturePipelineMockTests
{
    [Theory]
    [InlineData("SUCCESS")][InlineData("SOURCE-WRITE-FAIL")][InlineData("ADMISSION-FAIL")][InlineData("FAIL-RenderCode")][InlineData("MISSING-CODE-SVG")][InlineData("FAIL-ValidateWorkspace")][InlineData("FAIL-ExportWorkspace")][InlineData("MISSING-EXPORT")][InlineData("FAIL-RenderNative")][InlineData("MISSING-NATIVE-SVG")][InlineData("WORK-COLLISION")][InlineData("WORK-COMMIT-FAIL")]
    public Task CorrectWholeDenominatorPipelineMocksPass(string scenario){var e=new ArchitecturePipelineEnvironment(scenario);return ArchitecturePipelineAssertions.AssertCorrect(e,new ScriptedArchitecturePipeline(e));}
    [Theory]
    [InlineData("skip-admission")][InlineData("missing")][InlineData("bytes")][InlineData("media")][InlineData("path")][InlineData("early-effects")][InlineData("code-source")][InlineData("native-source")]
    public async Task PipelineAssertionRejectsIndividualWrongResponse(string mutation){var e=new ArchitecturePipelineEnvironment();Assert.NotNull(await Record.ExceptionAsync(()=>ArchitecturePipelineAssertions.AssertCorrect(e,new ScriptedArchitecturePipeline(e,mutation))));}
    [Theory]
    [InlineData("ADMISSION-FAIL","error-code")][InlineData("FAIL-RenderCode","error-code")][InlineData("MISSING-EXPORT","error-code")][InlineData("WORK-COLLISION","error-code")][InlineData("WORK-COMMIT-FAIL","error-code")]
    [InlineData("ADMISSION-FAIL","swallow-error")][InlineData("FAIL-RenderNative","swallow-error")]
    public async Task PipelineFailureAssertionsRejectIndividualWrongOutcome(string scenario,string mutation){var e=new ArchitecturePipelineEnvironment(scenario);Assert.NotNull(await Record.ExceptionAsync(()=>ArchitecturePipelineAssertions.AssertCorrect(e,new ScriptedArchitecturePipeline(e,mutation))));}
}
