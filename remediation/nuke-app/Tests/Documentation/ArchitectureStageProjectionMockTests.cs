using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record ArchitectureSubsetCase(string Id,string[] Aliases,string Failure="")
{public override string ToString()=>Id;}
public sealed class ArchitectureSubsetEnvironment
{
    public ArchitecturePipelineEnvironment Inner {get;}=new();
    public ArchitectureSubsetCase Case {get;}
    public DesignStagePlan Selection {get;}
    public IDesignStagePlanner Planner {get;}=new DesignStagePlanner();
    public IReadOnlyList<Artifact> Inputs {get;}
    public IReadOnlyList<Artifact> Code {get;}
    public DocumentationRendererProfile? Profile {get;}
    public ArchitectureRendererContext? Context {get;}
    public Dictionary<string,Artifact> ExpectedWork {get;}=new(StringComparer.Ordinal);
    public IReadOnlyList<Artifact> ExpectedPublic {get;}
    public ArchitectureRendererPhase[] ExpectedPhases {get;}
    public int BootstrapCalls {get;set;}
    public List<CancellationToken> BootstrapTokens {get;}=[];
    public RecordingSubsetRuntime Runtime {get;}
    public bool Has(DesignStage stage)=>Selection.Stages.Contains(stage);
    public bool ExplicitExport {get;}
    public ArchitectureSubsetEnvironment(ArchitectureSubsetCase test)
    {
        Case=test;var metadata=Planner.Describe();Selection=Planner.Select(new(test.Aliases,test.Failure=="registry"?"wrong":metadata.Sha256));
        Code=Has(DesignStage.CodeExtract)?Inner.Composition.CodeViews:[];
        Inputs=Has(DesignStage.CodeExtract)?Inner.Composition.Inputs:Inner.Composition.Inputs.Where(a=>a.RelativePath is "toolchain-lock.json" or "renderer-lock.json").ToArray();
        Profile=Has(DesignStage.Tools)&&test.Failure!="profile"?Inner.Composition.Profile:null;
        Context=Has(DesignStage.Tools)&&test.Failure!="context"?Inner.Composition.ArchitectureRequest.Renderer:null;
        ExplicitExport=Selection.Aliases.Any(alias=>metadata.Metadata.Aliases.Single(definition=>definition.Name==alias).Selects.Contains(DesignStage.ArchitectureExport));
        var phases=new List<ArchitectureRendererPhase>();
        if(Has(DesignStage.CodeRender))phases.Add(ArchitectureRendererPhase.RenderCode);
        if(Has(DesignStage.ArchitectureValidate))phases.Add(ArchitectureRendererPhase.ValidateWorkspace);
        if(Has(DesignStage.ArchitectureExport))phases.Add(ArchitectureRendererPhase.ExportWorkspace);
        if(Has(DesignStage.ArchitectureRender))phases.Add(ArchitectureRendererPhase.RenderNative);
        ExpectedPhases=phases.ToArray();var output=new List<Artifact>();
        if(Has(DesignStage.CodeExtract))output.AddRange(Code);
        if(Has(DesignStage.CodeRender))output.AddRange(Inner.Composition.Output.Where(a=>a.MediaType=="image/svg+xml"&&!a.RelativePath.Contains("structurizr-",StringComparison.Ordinal)));
        if(Has(DesignStage.ArchitectureRender))output.AddRange(Inner.Composition.Output.Where(a=>a.RelativePath.Contains("structurizr-",StringComparison.Ordinal)));
        if(ExplicitExport)
        {
            foreach(var key in ArchitecturePipelineEnvironment.NativeKeys)output.Add(Inner.ExpectedWork["build/structurizr-profiled/structurizr-"+key+".puml"] with{RelativePath="build/structurizr-export/structurizr-"+key+".puml"});
            output.Add(Inputs.Single(a=>a.RelativePath=="architecture/render-skin.puml"));
        }
        ExpectedPublic=output.OrderBy(a=>a.RelativePath,StringComparer.Ordinal).ToArray();
        if(Has(DesignStage.Tools))
        {
            foreach(var artifact in Inputs.Concat(Code))ExpectedWork.Add(artifact.RelativePath,artifact);
            Add(".docker-cli/config.json");
            if(Has(DesignStage.CodeRender))foreach(var code in Code){Add(code.RelativePath[..^5]+".svg");Add("docs/diagrams/"+Path.GetFileNameWithoutExtension(code.RelativePath)+".svg");}
            if(Has(DesignStage.ArchitectureExport))foreach(var key in ArchitecturePipelineEnvironment.NativeKeys){Add("build/structurizr-export/structurizr-"+key+".puml");Add("build/structurizr-profiled/structurizr-"+key+".puml");}
            if(Has(DesignStage.ArchitectureRender))foreach(var key in ArchitecturePipelineEnvironment.NativeKeys){Add("build/structurizr-profiled/structurizr-"+key+".svg");Add("docs/diagrams/structurizr-"+key+".svg");}
        }
        Runtime=new(this);
    }
    private void Add(string path)=>ExpectedWork.Add(path,Inner.ExpectedWork[path]);
    public Func<CancellationToken,Task>? Admission=>Case.Failure=="callback"?null:AdmitAsync;
    private Task AdmitAsync(CancellationToken token)
    {Inner.Trace.Add("bootstrap");BootstrapCalls++;BootstrapTokens.Add(token);if(Case.Failure=="bootstrap")throw new IOException("Scripted bootstrap failure");return Task.CompletedTask;}
}
public sealed class RecordingSubsetRuntime(ArchitectureSubsetEnvironment e):IArtifactGenerationRuntime
{
    public int Calls {get;private set;}
    public async Task<ArtifactGenerationResult> ExecuteAsync(string id,ArtifactGenerationRecipe recipe,CancellationToken cancellationToken=default)
    {
        Calls++;Assert.Equal(ArchitecturePipelineEnvironment.WorkGeneration,id);Assert.Equal(e.Inner.Cancellation.Token,cancellationToken);
        Assert.Equal(e.Inputs.Select(a=>new ArtifactSourceSpec(a.RelativePath,a.MediaType,e.Inner.Receipts.Describe(a))),recipe.Sources);
        var prepared=recipe.Prepare(e.Inputs);Assert.Equal(e.ExpectedWork.Keys.Order(StringComparer.Ordinal),prepared.OutputPaths.Order(StringComparer.Ordinal));Assert.Empty(e.Inner.StoreWrites);Assert.Equal(0,e.BootstrapCalls);
        if(e.Case.Failure=="collision")throw new ArtifactGenerationException("OUTPUT_EXISTS","Occupied subset work generation");
        e.Inner.Trace.Add("work-preflight");IReadOnlyList<Artifact> output;
        try{output=await prepared.MaterializeAsync(cancellationToken);}catch(OperationCanceledException){throw;}catch(Exception error){throw new ArtifactGenerationException("PROJECTION_FAILED","Subset materialization failed",error);}
        Assert.Equal(e.ExpectedWork.Keys.Order(StringComparer.Ordinal),output.Select(a=>a.RelativePath).Order(StringComparer.Ordinal));
        e.Inner.Trace.Add("work-commit");return new(id,e.Inputs.Select(e.Inner.Receipts.Describe).ToArray(),output.Select(e.Inner.Receipts.Describe).ToArray(),new("generation-manifest.json",new string('a',64),1,"application/json"));
    }
}
public static class ArchitectureStageProjectionCases
{
    public static IEnumerable<object[]> Cases
    {
        get
        {
            foreach(var alias in new DesignStagePlanner().Describe().Metadata.Aliases)yield return [new ArchitectureSubsetCase(alias.Name,[alias.Name])];
            yield return [new ArchitectureSubsetCase("union-extract-render",["extractCodeViews","renderCodeViews"])];
            yield return [new ArchitectureSubsetCase("union-export-render",["exportArchitecture","generateArchitectureDiagrams"])];
            yield return [new ArchitectureSubsetCase("union-ui-render",["generateUiAssets","renderStoryboards","generateArchitectureDiagrams"])];
            yield return [new ArchitectureSubsetCase("union-all",new DesignStagePlanner().Describe().Metadata.Aliases.Select(alias=>alias.Name).ToArray())];
            yield return [new ArchitectureSubsetCase("duplicate-export",["exportArchitecture","exportArchitecture"])];
            foreach(var failure in new[]{"profile","context","callback","collision","bootstrap"})yield return [new ArchitectureSubsetCase("FAIL-"+failure,["generateArchitectureDiagrams"],failure)];
            yield return [new ArchitectureSubsetCase("FAIL-unknown",["unknown"],"selection")];
            yield return [new ArchitectureSubsetCase("FAIL-registry",["extractCodeViews"],"registry")];
        }
    }
    public static async Task AssertSubset(ArchitectureSubsetEnvironment e,IArchitectureStageProjectionComposer provider)
    {
        var snapshots=e.Inputs.Concat(e.Code).Select(a=>a.Bytes.ToArray()).ToArray();ArtifactGenerationPreparation? prepared=null;
        var error=Record.Exception(()=>prepared=provider.PrepareSubset(e.Selection,e.Inputs,e.Profile,e.Code,e.Context,e.Admission));
        if(e.Case.Failure is "selection" or "registry" or "profile" or "context" or "callback")
        {
            var expected=e.Case.Failure is "selection" or "registry"?"DESIGN_STAGE_SELECTION":e.Case.Failure=="callback"?"TOOL_ADMISSION":"RENDERER_CONTEXT";
            Assert.Equal(expected,Assert.IsType<ArchitectureAssetException>(error).Code);Assert.Null(prepared);Assert.Empty(e.Inner.Trace);Assert.Equal(0,e.Runtime.Calls);return;
        }
        Assert.Null(error);Assert.NotNull(prepared);Assert.Equal(e.ExpectedPublic.Select(a=>a.RelativePath),prepared.OutputPaths.Order(StringComparer.Ordinal));Assert.Null(prepared.VerifyBeforeCommitAsync);
        Assert.Empty(e.Inner.Trace);Assert.Equal(0,e.BootstrapCalls);Assert.Equal(0,e.Runtime.Calls);
        IReadOnlyList<Artifact>? actual=null;var runError=await Record.ExceptionAsync(async()=>actual=await prepared.MaterializeAsync(e.Inner.Cancellation.Token));
        if(e.Case.Failure is "collision" or "bootstrap")
        {
            Assert.Equal(e.Case.Failure=="collision"?"OUTPUT_EXISTS":"PROJECTION_FAILED",Assert.IsType<ArtifactGenerationException>(runError).Code);Assert.Null(actual);Assert.DoesNotContain("work-commit",e.Inner.Trace);
            if(e.Case.Failure=="collision"){Assert.Empty(e.Inner.StoreWrites);Assert.Equal(0,e.BootstrapCalls);}else Assert.Equal(1,e.BootstrapCalls);
            Assert.Empty(e.Inner.Commands);return;
        }
        Assert.Null(runError);Assert.NotNull(actual);Assert.Equal(e.ExpectedPublic.Select(a=>a.RelativePath),actual.OrderBy(a=>a.RelativePath,StringComparer.Ordinal).Select(a=>a.RelativePath));
        foreach(var expected in e.ExpectedPublic){var artifact=actual.Single(a=>a.RelativePath==expected.RelativePath);Assert.Equal(expected.MediaType,artifact.MediaType);Assert.Equal(expected.Bytes.ToArray(),artifact.Bytes.ToArray());}
        Assert.Equal(e.ExpectedPhases,e.Inner.Commands.Select(command=>command.Phase));Assert.Equal(e.Has(DesignStage.Tools)?1:0,e.Runtime.Calls);Assert.Equal(e.Has(DesignStage.Tools)?1:0,e.BootstrapCalls);
        Assert.All(e.BootstrapTokens,token=>Assert.Equal(e.Inner.Cancellation.Token,token));
        Assert.Equal(e.Has(DesignStage.Tools)?1:0,e.Inner.Trace.Count(value=>value=="admit"));
        if(e.Has(DesignStage.Tools)){Assert.True(e.Inner.Trace.IndexOf("bootstrap")>e.Inner.Trace.IndexOf("work-preflight"));Assert.True(e.Inner.Trace.IndexOf("admit")>e.Inner.Trace.IndexOf("bootstrap"));Assert.Contains("work-commit",e.Inner.Trace);}
        else{Assert.Empty(e.Inner.Trace);Assert.Empty(e.Inner.StoreWrites);}
        if(e.Has(DesignStage.CodeRender))Assert.Equal(e.Code.Select(a=>a.RelativePath),e.Inner.Commands.Single(command=>command.Phase==ArchitectureRendererPhase.RenderCode).SourcePaths);
        if(e.Has(DesignStage.ArchitectureRender))Assert.Equal(ArchitecturePipelineEnvironment.NativeKeys.Select(key=>"build/structurizr-profiled/structurizr-"+key+".puml"),e.Inner.Commands.Single(command=>command.Phase==ArchitectureRendererPhase.RenderNative).SourcePaths);
        foreach(var command in e.Inner.Commands.Where(command=>command.Phase is ArchitectureRendererPhase.ValidateWorkspace or ArchitectureRendererPhase.ExportWorkspace))Assert.Empty(command.SourcePaths);
        Assert.Equal((e.Has(DesignStage.CodeRender)?6:0)+(e.Has(DesignStage.ArchitectureRender)?15:0),e.Inner.Normalizations.Count);
        for(var index=0;index<snapshots.Length;index++)Assert.Equal(snapshots[index],e.Inputs.Concat(e.Code).ElementAt(index).Bytes.ToArray());
        if(e.ExplicitExport)
        {
            var skin=actual.Single(a=>a.RelativePath=="architecture/render-skin.puml");Assert.Equal(e.Inputs.Single(a=>a.RelativePath==skin.RelativePath).Bytes.ToArray(),skin.Bytes.ToArray());
            foreach(var export in actual.Where(a=>a.RelativePath.StartsWith("build/structurizr-export/",StringComparison.Ordinal)))
            {
                var text=System.Text.Encoding.UTF8.GetString(export.Bytes.Span);Assert.Contains("!include ../../architecture/render-skin.puml",text,StringComparison.Ordinal);
                foreach(System.Text.RegularExpressions.Match include in System.Text.RegularExpressions.Regex.Matches(text,@"(?m)^!include\s+([^\r\n]+)"))
                {
                    var target=new Uri(new Uri("https://fixture.invalid/generation/"+export.RelativePath),include.Groups[1].Value.Trim());
                    Assert.Equal("fixture.invalid",target.Host);Assert.StartsWith("/generation/",target.AbsolutePath,StringComparison.Ordinal);
                    Assert.Contains(actual,artifact=>artifact.RelativePath==Uri.UnescapeDataString(target.AbsolutePath["/generation/".Length..]));
                }
            }
        }
    }
}
public sealed class ScriptedArchitectureStageComposer(ArchitectureSubsetEnvironment e,string mutation=""):IArchitectureStageProjectionComposer
{
    public ArchitectureCompositionPlan Plan(IReadOnlyList<Artifact> inputs,DocumentationRendererProfile profile,IReadOnlyList<Artifact> code)=>throw new InvalidOperationException("Legacy boundary has its existing independent assertions");
    public Task<IReadOnlyList<Artifact>> RenderAsync(ArchitectureCompositionPlan plan,ArchitectureRendererContext context,IReadOnlyList<AndroidMcp.BuildTool.Modules.Sources.Acquisition.ToolArtifactReceipt> tools,CancellationToken cancellationToken=default)=>throw new InvalidOperationException("Legacy boundary has its existing independent assertions");
    public ArtifactGenerationPreparation PrepareSubset(DesignStagePlan selection,IReadOnlyList<Artifact> inputs,DocumentationRendererProfile? profile,IReadOnlyList<Artifact> code,ArchitectureRendererContext? context,Func<CancellationToken,Task>? admission)
    {
        Assert.Same(e.Selection,selection);Assert.Same(e.Inputs,inputs);Assert.Same(e.Code,code);Assert.Same(e.Profile,profile);Assert.Same(e.Context,context);
        if(selection.Errors.Count>0)throw new ArchitectureAssetException(mutation=="outcome"?"WRONG":"DESIGN_STAGE_SELECTION","Rejected stage selection");
        if(e.Has(DesignStage.Tools)&&(profile is null||context is null))throw new ArchitectureAssetException(mutation=="outcome"?"WRONG":"RENDERER_CONTEXT","Missing native prerequisite");
        if(e.Has(DesignStage.Tools)&&admission is null)throw new ArchitectureAssetException(mutation=="outcome"?"WRONG":"TOOL_ADMISSION","Missing deferred tool admission");
        return new(e.ExpectedPublic.Select(a=>a.RelativePath).ToArray(),async token=>
        {
            if(mutation=="outcome")throw new InvalidOperationException("Wrong subset outcome");
            if(!e.Has(DesignStage.Tools))return e.ExpectedPublic;
            var store=new RecordingPipelineStore(e.Inner);var transport=new RecordingPipelineTransport(e.Inner);var normalize=new RecordingPipelinePreparation(e.Inner);var image=new RecordingPipelineAdmission(e.Inner);
            var recipe=new ArtifactGenerationRecipe(inputs.Select(a=>new ArtifactSourceSpec(a.RelativePath,a.MediaType,e.Inner.Receipts.Describe(a))).ToArray(),_=>new(e.ExpectedWork.Keys.ToArray(),async innerToken=>
            {
                foreach(var source in inputs.Concat(code))await store.WriteAsync(source,innerToken);await store.WriteAsync(e.ExpectedWork[".docker-cli/config.json"],innerToken);
                await admission!(mutation=="token"?CancellationToken.None:innerToken);if(mutation=="bootstrap-twice")await admission(innerToken);
                await image.AdmitAsync(profile!,context!,innerToken);
                foreach(var phase in e.ExpectedPhases)
                {
                    var paths=phase==ArchitectureRendererPhase.RenderCode?code.Select(a=>a.RelativePath).ToArray():phase==ArchitectureRendererPhase.RenderNative?ArchitecturePipelineEnvironment.NativeKeys.Select(key=>"build/structurizr-profiled/structurizr-"+key+".puml").ToArray():Array.Empty<string>();
                    await transport.ExecuteAsync(profile!,context!,new(phase,paths),innerToken);
                    if(mutation=="phase-twice")await transport.ExecuteAsync(profile!,context!,new(phase,paths),innerToken);
                    if(phase==ArchitectureRendererPhase.RenderCode)foreach(var source in code){var path=source.RelativePath[..^5]+".svg";await store.WriteAsync(normalize.NormalizeVector(new(path,await store.ReadAsync(path,innerToken),"image/svg+xml"),"docs/diagrams/"+Path.GetFileNameWithoutExtension(source.RelativePath)+".svg"),innerToken);}
                    if(phase==ArchitectureRendererPhase.ExportWorkspace)foreach(var key in ArchitecturePipelineEnvironment.NativeKeys)await store.WriteAsync(e.ExpectedWork["build/structurizr-profiled/structurizr-"+key+".puml"],innerToken);
                    if(phase==ArchitectureRendererPhase.RenderNative)foreach(var key in ArchitecturePipelineEnvironment.NativeKeys){var path="build/structurizr-profiled/structurizr-"+key+".svg";await store.WriteAsync(normalize.NormalizeVector(new(path,await store.ReadAsync(path,innerToken),"image/svg+xml"),"docs/diagrams/structurizr-"+key+".svg"),innerToken);}
                }
                return e.ExpectedWork.Keys.Select(path=>e.Inner.Work[path]).ToArray();
            }));
            await e.Runtime.ExecuteAsync(ArchitecturePipelineEnvironment.WorkGeneration,recipe,token);
            var result=e.ExpectedPublic.ToArray();if(mutation=="missing-output")return result.Skip(1).ToArray();if(mutation=="output-bytes"&&result.Length>0)result[0]=result[0] with{Bytes=new byte[]{0}};
            return result;
        });
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class ArchitectureStageProjectionMockTests
{
    [Theory,MemberData(nameof(ArchitectureStageProjectionCases.Cases),MemberType=typeof(ArchitectureStageProjectionCases))]
    public Task CorrectAliasSubsetAndUnionMocksPass(ArchitectureSubsetCase test){var e=new ArchitectureSubsetEnvironment(test);return ArchitectureStageProjectionCases.AssertSubset(e,new ScriptedArchitectureStageComposer(e));}
    [Theory,MemberData(nameof(ArchitectureStageProjectionCases.Cases),MemberType=typeof(ArchitectureStageProjectionCases))]
    public async Task EveryWrongSubsetOutcomeIsRejected(ArchitectureSubsetCase test){var e=new ArchitectureSubsetEnvironment(test);Assert.NotNull(await Record.ExceptionAsync(()=>ArchitectureStageProjectionCases.AssertSubset(e,new ScriptedArchitectureStageComposer(e,"outcome"))));}
    [Theory]
    [InlineData("token")][InlineData("bootstrap-twice")][InlineData("phase-twice")][InlineData("missing-output")][InlineData("output-bytes")]
    public async Task EachTokenDuplicateAndOutputBypassIsRejected(string mutation){var e=new ArchitectureSubsetEnvironment(new("NEGATIVE",["exportArchitecture","generateArchitectureDiagrams"]));Assert.NotNull(await Record.ExceptionAsync(()=>ArchitectureStageProjectionCases.AssertSubset(e,new ScriptedArchitectureStageComposer(e,mutation))));}
}
