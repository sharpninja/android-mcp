using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record DesignNarrowRecipeCase(string Id,DesignStageSourceCase Source,string Failure="")
{public override string ToString()=>Id;}
public sealed class DesignNarrowRecipeEnvironment
{
    public DesignNarrowRecipeCase Case {get;}
    public FullArchitectureRecipeEnvironment Original {get;}=new("FULL");
    public ArchitectureSubsetEnvironment Subset {get;}
    public IReadOnlyList<Artifact> Inputs {get;}
    public bool LegacyReceiptsEnabled {get;}
    public DocumentationProjection UiProjection {get;}
    public IReadOnlyList<Artifact> Ui=>Has(DesignStage.UiAssets)?UiProjection.Artifacts:[];
    public IReadOnlyList<Artifact> Code=>Has(DesignStage.CodeExtract)?Original.Architecture.Code:[];
    public IReadOnlyList<Artifact> ExpectedReceipts=>LegacyReceiptsEnabled?StandaloneNarrowReceiptCases.ExpectedReceipts(this):[];
    public IReadOnlyList<Artifact> Expected=>Ui.Concat(Subset.ExpectedPublic).Concat(ExpectedReceipts).ToArray();
    public DesignArchitectureRequest Request {get;}
    public DesignStagePlan Selection=>Case.Source.Selection;
    public List<string> Trace {get;}=[];
    public CancellationTokenSource Cancellation {get;}=new();
    public bool Has(DesignStage stage)=>Selection.Stages.Contains(stage);
    public DesignNarrowRecipeEnvironment(DesignNarrowRecipeCase test,bool legacyReceipts=false)
    {
        Case=test;LegacyReceiptsEnabled=legacyReceipts;Subset=new(new(test.Id,Selection.Aliases.ToArray()));
        UiProjection=Original.Model.Ui;
        if(legacyReceipts&&Has(DesignStage.UiAssets))
        {
            var genuine=LegacyReceiptCases.NativeUiInput();
            UiProjection=UiProjection with{Artifacts=UiProjection.Artifacts.Select(artifact=>artifact.RelativePath==genuine.RelativePath?genuine:artifact).ToArray()};
        }
        Inputs=test.Source.Reject||Has(DesignStage.ModelContracts)?[]:
            DesignStageSourceCases.Expected(test.Source).Select(descriptor=>Original.Inputs.Single(input=>input.RelativePath==descriptor.Source.RelativePath)).ToArray();
        Request=Original.Request with{Renderer=Has(DesignStage.Tools)?Original.Request.Renderer:null!};
        if(test.Failure=="cancel")Cancellation.Cancel();
    }
    public Artifact Input(string path)=>Inputs.Single(input=>input.RelativePath==path);
}
public sealed class RecordingNarrowStageServices(DesignNarrowRecipeEnvironment e):IDocumentationRendererProfileReader,
    IModuleGraphValidator,IDocumentationProjector,IArchitectureAssetPreparation,IArchitectureStageProjectionComposer,IDocumentationToolBootstrap,IDocumentationModelPreflight
{
    public DocumentationRendererProfile Read(Artifact toolchain,Artifact renderer)
    {e.Trace.Add("profile");Assert.True(e.Has(DesignStage.Tools));Assert.Same(e.Input("toolchain-lock.json"),toolchain);Assert.Same(e.Input("renderer-lock.json"),renderer);return e.Original.Architecture.Inner.Composition.Profile;}
    public ModuleValidationResult Validate(ModuleValidationInput input)
    {
        e.Trace.Add("module");Assert.True(e.Has(DesignStage.ModuleGraph));Assert.Equal(Encoding.UTF8.GetString(e.Input("architecture/code-diagrams.md").Bytes.Span),input.CodeMarkdown);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(e.Input("architecture/module-contract.json").Bytes.Span),input.ModuleContract));
        return e.Original.Model.Modules with{Issues=e.Case.Failure=="module"?[new("MODULE_CYCLE","architecture/module-contract.json","fixture")]:[]};
    }
    public DocumentationProjection Project(DocumentationInput input)
    {e.Trace.Add("ui");Assert.True(e.Has(DesignStage.UiAssets));Assert.Same(e.Input(DocumentationAssetPaths.Blueprint),input.BlueprintMarkdown);Assert.Same(e.Input(DocumentationAssetPaths.Design),input.DesignMarkdown);if(e.Case.Failure=="ui")throw new InvalidDataException("narrow UI fixture");return e.UiProjection;}
    public IReadOnlyList<Artifact> ExtractCodeViews(ArchitecturePreparationInput input)
    {e.Trace.Add("code");Assert.True(e.Has(DesignStage.CodeExtract));Assert.Same(e.Input("architecture/workspace.dsl"),input.Workspace);Assert.Same(e.Input("architecture/code-diagrams.md"),input.CodeMarkdown);if(e.Case.Failure=="code")throw new ArchitectureAssetException("CODE_BLOCK","narrow fixture");return e.Code;}
    public ArtifactGenerationPreparation PrepareSubset(DesignStagePlan selection,IReadOnlyList<Artifact> inputs,DocumentationRendererProfile? profile,IReadOnlyList<Artifact> code,ArchitectureRendererContext? context,Func<CancellationToken,Task>? admit)
    {
        e.Trace.Add("subset.prepare");Assert.Same(e.Selection,selection);Assert.Same(e.Inputs,inputs);Assert.Equal(e.Code,code);
        if(e.Has(DesignStage.Tools)){Assert.Same(e.Original.Architecture.Inner.Composition.Profile,profile);Assert.Same(e.Request.Renderer,context);Assert.NotNull(admit);}
        else {Assert.Null(profile);Assert.Null(context);Assert.Null(admit);}
        return new(e.Subset.ExpectedPublic.Select(a=>a.RelativePath).ToArray(),async token=>
        {e.Trace.Add("subset.materialize");Assert.Equal(e.Cancellation.Token,token);if(admit is not null)await admit(token);if(e.Has(DesignStage.CodeRender))e.Trace.Add("render");if(e.Case.Failure=="render")throw new ArchitectureAssetException("RENDERER_PROCESS","narrow fixture");return e.Subset.ExpectedPublic;});
    }
    public Task<IReadOnlyList<ToolArtifactReceipt>> AcquireAsync(DocumentationRendererProfile profile,ToolArtifactAccess access,string? proof,CancellationToken token=default)
    {e.Trace.Add("bootstrap");Assert.True(e.Has(DesignStage.Tools));Assert.Equal(e.Cancellation.Token,token);Assert.Same(e.Original.Architecture.Inner.Composition.Profile,profile);Assert.Equal(e.Request.ToolAccess,access);Assert.Equal(e.Request.OfflineGradleChecksumProof,proof);if(e.Case.Failure=="tools")throw new TransportException("TOOL_HASH","narrow fixture");return Task.FromResult<IReadOnlyList<ToolArtifactReceipt>>(e.Original.Architecture.Inner.Composition.Tools);}
    public DocumentationModelPreflightResult Prepare(DocumentationModelPreflightInput input)=>throw new InvalidOperationException("Narrow stages cannot read the model, contract snapshots or optional catalog");
    public Artifact NormalizeVector(Artifact artifact,string path)=>throw new InvalidOperationException("One existing renderer normalizes vectors");
    public ArchitectureCompositionPlan Plan(IReadOnlyList<Artifact> inputs,DocumentationRendererProfile profile,IReadOnlyList<Artifact> code)=>throw new InvalidOperationException("One Core union feeds subset preparation");
    public Task<IReadOnlyList<Artifact>> RenderAsync(ArchitectureCompositionPlan plan,ArchitectureRendererContext context,IReadOnlyList<ToolArtifactReceipt> tools,CancellationToken token=default)=>throw new InvalidOperationException("One Core union feeds subset preparation");
}
public static class DesignNarrowStageRecipeCases
{
    public static IEnumerable<object[]> Cases
    {
        get
        {
            var source=DesignStageSourceCases.Cases.Select(row=>(DesignStageSourceCase)row[0]).ToArray();
            var result=source.Select(test=>new DesignNarrowRecipeCase(test.Id,test)).ToList();
            foreach(var (alias,failure) in new[]{("validateModuleGraph","module"),("generateUiAssets","ui"),("extractCodeViews","code"),("verifyToolchain","tools"),("renderCodeViews","render"),("extractCodeViews","cancel")})
                result.Add(new(alias+"-"+failure,source.Single(test=>test.Id==alias),failure));
            return result.Select(test=>new object[]{test});
        }
    }
    public static async Task AssertRecipe(DesignNarrowRecipeEnvironment e,Func<DesignArchitectureRequest,DesignStagePlan,ArtifactGenerationRecipe> provider)
    {
        ArtifactGenerationRecipe? recipe=null;var creation=Record.Exception(()=>recipe=provider(e.Request,e.Selection));
        if(e.Case.Source.Reject){Assert.Equal("DESIGN_STAGE_SELECTION",Assert.IsType<ArchitectureAssetException>(creation).Code);Assert.Empty(e.Trace);return;}
        if(e.Has(DesignStage.ModelContracts)){Assert.Equal("MODEL_PREFLIGHT_REQUIRED",Assert.IsType<ArchitectureAssetException>(creation).Code);Assert.Empty(e.Trace);return;}
        Assert.Null(creation);Assert.NotNull(recipe);Assert.Empty(e.Trace);
        Assert.Equal(DesignStageSourceCases.Expected(e.Case.Source).Select(input=>new ArtifactSourceSpec(input.Source.RelativePath,input.Source.MediaType)),recipe.Sources);
        ArtifactGenerationPreparation? prepared=null;var early=Record.Exception(()=>prepared=recipe.Prepare(e.Inputs));
        if(e.Case.Failure is "module" or "ui" or "code")
        {
            if(e.Case.Failure=="ui")Assert.Equal("narrow UI fixture",Assert.IsType<InvalidDataException>(early).Message);
            else Assert.Equal(e.Case.Failure=="module"?"MODULE_CONTRACT":"CODE_BLOCK",Assert.IsType<ArchitectureAssetException>(early).Code);
            Assert.DoesNotContain("bootstrap",e.Trace);Assert.DoesNotContain("subset.materialize",e.Trace);return;
        }
        Assert.Null(early);Assert.NotNull(prepared);Assert.Null(prepared.VerifyBeforeCommitAsync);
        var prepareTrace=new List<string>();if(e.Has(DesignStage.Tools))prepareTrace.Add("profile");if(e.Has(DesignStage.ModuleGraph))prepareTrace.Add("module");if(e.Has(DesignStage.UiAssets))prepareTrace.Add("ui");if(e.Has(DesignStage.CodeExtract))prepareTrace.Add("code");prepareTrace.Add("subset.prepare");
        Assert.Equal(prepareTrace,e.Trace);Assert.Equal(e.Expected.Select(a=>a.RelativePath),prepared.OutputPaths);
        IReadOnlyList<Artifact>? actual=null;var failure=await Record.ExceptionAsync(async()=>actual=await prepared.MaterializeAsync(e.Cancellation.Token));
        if(e.Case.Failure=="cancel"){Assert.IsAssignableFrom<OperationCanceledException>(failure);Assert.Equal(prepareTrace,e.Trace);return;}
        if(e.Case.Failure=="tools"){Assert.Equal("TOOL_HASH",Assert.IsType<TransportException>(failure).Code);return;}
        if(e.Case.Failure=="render"){Assert.Equal("RENDERER_PROCESS",Assert.IsType<ArchitectureAssetException>(failure).Code);return;}
        Assert.Null(failure);Assert.NotNull(actual);DesignDocumentationStageCases.AssertArtifacts(e.Expected,actual);
        var fullTrace=prepareTrace.Concat(new[]{"subset.materialize"}).ToList();if(e.Has(DesignStage.Tools))fullTrace.Add("bootstrap");if(e.Has(DesignStage.CodeRender))fullTrace.Add("render");Assert.Equal(fullTrace,e.Trace);
    }
}
public sealed class ScriptedNarrowStageRecipeFactory(DesignNarrowRecipeEnvironment e,string mutation="")
{
    public ArtifactGenerationRecipe Architecture(DesignArchitectureRequest request,DesignStagePlan selection)
    {
        if(e.Case.Source.Reject)throw new ArchitectureAssetException(mutation=="admission"?"WRONG":"DESIGN_STAGE_SELECTION","fixture");
        if(e.Has(DesignStage.ModelContracts))throw new ArchitectureAssetException(mutation=="admission"?"WRONG":"MODEL_PREFLIGHT_REQUIRED","fixture");
        var descriptors=DesignStageSourceCases.Expected(e.Case.Source);var s=new RecordingNarrowStageServices(e);
        var specs=descriptors.Select(input=>new ArtifactSourceSpec(input.Source.RelativePath,input.Source.MediaType)).ToArray();if(mutation=="sources")specs=specs.Skip(1).ToArray();
        return new(specs,inputs=>
        {
            var profile=e.Has(DesignStage.Tools)||mutation=="profile-overread"?s.Read(e.Input("toolchain-lock.json"),e.Input("renderer-lock.json")):null;
            if(e.Has(DesignStage.ModuleGraph))
            {var result=s.Validate(new(JsonNode.Parse(e.Input("architecture/module-contract.json").Bytes.Span)!.AsObject(),Encoding.UTF8.GetString(e.Input("architecture/code-diagrams.md").Bytes.Span)));if(result.Issues.Count>0&&mutation!="module-failure")throw new ArchitectureAssetException("MODULE_CONTRACT","fixture");}
            var ui=e.Has(DesignStage.UiAssets)&&mutation!="skip-ui"?s.Project(new(e.Input(DocumentationAssetPaths.Blueprint),e.Input(DocumentationAssetPaths.Design))).Artifacts:Array.Empty<Artifact>();
            var code=e.Has(DesignStage.CodeExtract)&&mutation!="skip-code"?s.ExtractCodeViews(new(e.Input("architecture/workspace.dsl"),e.Input("architecture/code-diagrams.md"))):Array.Empty<Artifact>();
            Func<CancellationToken,Task>? tools=e.Has(DesignStage.Tools)?async token=>{await s.AcquireAsync(profile!,request.ToolAccess,request.OfflineGradleChecksumProof,mutation=="token"?CancellationToken.None:token);}:null;
            if(mutation=="early-tools")tools!(e.Cancellation.Token).GetAwaiter().GetResult();
            var subset=s.PrepareSubset(selection,inputs,profile,code,e.Has(DesignStage.Tools)?request.Renderer:null,tools);
            return new(ui.Select(a=>a.RelativePath).Concat(subset.OutputPaths).Concat(e.ExpectedReceipts.Select(a=>a.RelativePath)).ToArray(),async token=>
            {if(mutation!="cancel")token.ThrowIfCancellationRequested();var output=ui.Concat(await subset.MaterializeAsync(token)).Concat(e.ExpectedReceipts).ToArray();return mutation=="output"?output.Skip(1).ToArray():output;});
        });
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DesignNarrowStageRecipeMockTests
{
    [Theory,MemberData(nameof(DesignNarrowStageRecipeCases.Cases),MemberType=typeof(DesignNarrowStageRecipeCases))]
    public Task CorrectNarrowInputsTimingAndFailuresPass(DesignNarrowRecipeCase test)
    {var e=new DesignNarrowRecipeEnvironment(test);return DesignNarrowStageRecipeCases.AssertRecipe(e,new ScriptedNarrowStageRecipeFactory(e).Architecture);}
    [Theory]
    [InlineData("extractCodeViews","sources")][InlineData("extractCodeViews","profile-overread")][InlineData("generateUiAssets","skip-ui")]
    [InlineData("extractCodeViews","skip-code")][InlineData("verifyToolchain","early-tools")][InlineData("verifyToolchain","token")][InlineData("extractCodeViews","output")]
    [InlineData("validateModuleGraph-module","module-failure")][InlineData("extractCodeViews-cancel","cancel")]
    [InlineData("INVALID-ALIAS","admission")][InlineData("validateModelContracts","admission")]
    public async Task EachScopeEffectAndFailureBypassIsRejected(string id,string mutation)
    {var test=DesignNarrowStageRecipeCases.Cases.Select(row=>(DesignNarrowRecipeCase)row[0]).Single(test=>test.Id==id);var e=new DesignNarrowRecipeEnvironment(test);Assert.NotNull(await Record.ExceptionAsync(()=>DesignNarrowStageRecipeCases.AssertRecipe(e,new ScriptedNarrowStageRecipeFactory(e,mutation).Architecture)));}
}
