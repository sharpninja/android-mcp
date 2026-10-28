using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed class FullArchitectureRecipeEnvironment
{
    public string Scenario {get;}
    public ModelPreflightEnvironment Model {get;}
    public ArchitectureSubsetEnvironment Architecture {get;}
    public List<string> Trace {get;}=[];
    public List<CancellationToken> Tokens {get;}=[];
    public IReadOnlyList<Artifact> Inputs=>Model.Input.RequiredArtifacts;
    public DocumentationModelSourceSnapshot Snapshot=>Model.Input.Sources;
    public DesignStagePlan Selection=>Architecture.Selection;
    public DesignArchitectureRequest Request=>Architecture.Inner.Composition.ArchitectureRequest;
    public IReadOnlyList<Artifact> Expected=>Model.Ui.Artifacts.Concat(Architecture.ExpectedPublic).Append(Snapshot.Receipt).OrderBy(artifact=>artifact.RelativePath,StringComparer.Ordinal).ToArray();
    public FullArchitectureRecipeEnvironment(string scenario)
    {
        Scenario=scenario;Model=new(scenario=="ABSENT"?"OPTIONAL-CANONICAL-ABSENT":"FULL");
        Architecture=new(new("OUTER",scenario=="MODEL-ONLY"?["validateModelContracts"]:scenario=="EXPORT"?["exportArchitecture"]:scenario=="PENDING"?["verifyDesignDocs"]:["generateArchitectureDiagrams"]));
    }
}
public sealed class RecordingFullArchitectureServices(FullArchitectureRecipeEnvironment e):IDocumentationRendererProfileReader,IArchitectureAssetPreparation,IArchitectureStageProjectionComposer,IDocumentationToolBootstrap,IDocumentationModelPreflight,IDocumentationSourceCollection
{
    public DocumentationRendererProfile Read(Artifact tools,Artifact renderer)
    {e.Trace.Add("profile");Assert.Same(e.Inputs.Single(a=>a.RelativePath=="toolchain-lock.json"),tools);Assert.Same(e.Inputs.Single(a=>a.RelativePath=="renderer-lock.json"),renderer);return e.Architecture.Inner.Composition.Profile;}
    public DocumentationModelPreflightResult Prepare(DocumentationModelPreflightInput input)
    {e.Trace.Add("model");Assert.Same(e.Inputs,input.RequiredArtifacts);Assert.Same(e.Snapshot,input.Sources);Assert.Equal(e.Selection.Stages.Contains(DesignStage.ModuleGraph),input.EnforceModuleGraph);if(e.Scenario=="MODEL-FAIL")throw new ModelContractException(["outer model fixture"]);return new(e.Model.Model,e.Model.Ui,e.Model.Modules,e.Model.Validated);}
    public IReadOnlyList<Artifact> ExtractCodeViews(ArchitecturePreparationInput input)
    {e.Trace.Add("code");Assert.Same(e.Inputs.Single(a=>a.RelativePath=="architecture/workspace.dsl"),input.Workspace);Assert.Same(e.Inputs.Single(a=>a.RelativePath=="architecture/code-diagrams.md"),input.CodeMarkdown);return e.Architecture.Code;}
    public Artifact NormalizeVector(Artifact rawSvg,string outputPath)=>throw new InvalidOperationException("The shared renderer owns normalization");
    public ArtifactGenerationPreparation PrepareSubset(DesignStagePlan selection,IReadOnlyList<Artifact> inputs,DocumentationRendererProfile? profile,IReadOnlyList<Artifact> codeViews,ArchitectureRendererContext? context,Func<CancellationToken,Task>? admitTools)
    {
        e.Trace.Add("subset.prepare");Assert.Same(e.Selection,selection);Assert.Same(e.Inputs,inputs);Assert.Same(e.Architecture.Inner.Composition.Profile,profile);Assert.Equal(e.Architecture.Code,codeViews);Assert.Same(e.Request.Renderer,context);Assert.NotNull(admitTools);
        return new(e.Architecture.ExpectedPublic.Select(a=>a.RelativePath).ToArray(),async token=>
        {e.Trace.Add("subset.materialize");await admitTools(token);e.Trace.Add("render");if(e.Scenario=="RENDER-FAIL")throw new ArchitectureAssetException("RENDERER_PROCESS","outer render fixture");return e.Architecture.ExpectedPublic;});
    }
    public Task<IReadOnlyList<ToolArtifactReceipt>> AcquireAsync(DocumentationRendererProfile profile,ToolArtifactAccess access,string? proof,CancellationToken cancellationToken=default)
    {e.Trace.Add("bootstrap");e.Tokens.Add(cancellationToken);Assert.Same(e.Architecture.Inner.Composition.Profile,profile);Assert.Equal(e.Request.ToolAccess,access);Assert.Equal(e.Request.OfflineGradleChecksumProof,proof);if(e.Scenario=="TOOL-FAIL")throw new TransportException("TOOL_HASH","outer tool fixture");return Task.FromResult<IReadOnlyList<ToolArtifactReceipt>>(e.Architecture.Inner.Composition.Tools);}
    public Task VerifyUnchangedAsync(DocumentationModelSourceSnapshot snapshot,CancellationToken cancellationToken=default)
    {e.Trace.Add("sources.verify");Assert.Same(e.Snapshot,snapshot);e.Tokens.Add(cancellationToken);if(e.Scenario=="SOURCE-CHANGED")throw new DocumentationSourceCollectionException("MODEL_SOURCE_CHANGED",["canonical:OpenApi"]);return Task.CompletedTask;}
    public Task<DocumentationModelSourceSnapshot> CaptureAsync(CancellationToken cancellationToken=default)=>throw new InvalidOperationException("The host captured once before recipe construction");
    public ArchitectureCompositionPlan Plan(IReadOnlyList<Artifact> inputs,DocumentationRendererProfile profile,IReadOnlyList<Artifact> code)=>throw new InvalidOperationException("Use the admitted stage plan");
    public Task<IReadOnlyList<Artifact>> RenderAsync(ArchitectureCompositionPlan plan,ArchitectureRendererContext context,IReadOnlyList<ToolArtifactReceipt> tools,CancellationToken cancellationToken=default)=>throw new InvalidOperationException("Use the shared subset preparation");
}
public static class FullArchitectureRecipeCases
{
    public static IEnumerable<object[]> Cases=>new[]{"FULL","ABSENT","MODEL-ONLY","EXPORT","PENDING","MODEL-FAIL","TOOL-FAIL","RENDER-FAIL","SOURCE-CHANGED"}.Select(scenario=>new object[]{scenario});
    public static async Task AssertRecipe(FullArchitectureRecipeEnvironment e,IDesignFullArchitectureRecipeFactory provider)
    {
        var bytes=e.Inputs.Select(input=>input.Bytes.ToArray()).ToArray();var services=new RecordingFullArchitectureServices(e);
        var recipe=provider.Architecture(e.Request,e.Selection,e.Snapshot,services);
        var required=e.Snapshot.Sources.Where(row=>row.Observation.Source.Role!=DocumentationInputRole.CanonicalContractOptional).Select(row=>new ArtifactSourceSpec(row.Observation.Source.RelativePath,row.Observation.Source.MediaType,row.Identity)).ToArray();
        Assert.Equal(required,recipe.Sources);Assert.Equal(12,recipe.Sources.Count);Assert.Empty(e.Trace);
        ArtifactGenerationPreparation? prepared=null;var prepareError=Record.Exception(()=>prepared=recipe.Prepare(e.Inputs));
        if(e.Scenario=="PENDING"){Assert.Equal("DESIGN_STAGE_PENDING",Assert.IsType<ArchitectureAssetException>(prepareError).Code);Assert.Empty(e.Trace);return;}
        if(e.Scenario=="MODEL-FAIL"){Assert.Equal(new[]{"outer model fixture"},Assert.IsType<ModelContractException>(prepareError).Errors);Assert.Equal(new[]{"profile","model"},e.Trace);return;}
        Assert.Null(prepareError);Assert.NotNull(prepared);Assert.NotNull(prepared.VerifyBeforeCommitAsync);
        Assert.Equal(e.Expected.Select(a=>a.RelativePath),prepared.OutputPaths.Order(StringComparer.Ordinal));
        var expectedPrepare=e.Selection.Stages.Contains(DesignStage.CodeExtract)?new[]{"profile","model","code","subset.prepare"}:new[]{"profile","model","subset.prepare"};Assert.Equal(expectedPrepare,e.Trace);
        IReadOnlyList<Artifact>? result=null;var error=await Record.ExceptionAsync(async()=>result=await prepared.MaterializeAsync(e.Architecture.Inner.Cancellation.Token));
        if(e.Scenario is "TOOL-FAIL" or "RENDER-FAIL")
        {
            if(e.Scenario=="TOOL-FAIL")Assert.Equal("TOOL_HASH",Assert.IsType<TransportException>(error).Code);else Assert.Equal("RENDERER_PROCESS",Assert.IsType<ArchitectureAssetException>(error).Code);
            Assert.Null(result);Assert.DoesNotContain("sources.verify",e.Trace);return;
        }
        Assert.Null(error);Assert.NotNull(result);Assert.Equal(e.Expected.Select(a=>a.RelativePath),result.OrderBy(a=>a.RelativePath,StringComparer.Ordinal).Select(a=>a.RelativePath));
        foreach(var expected in e.Expected){var actual=result.Single(a=>a.RelativePath==expected.RelativePath);Assert.Equal(expected.MediaType,actual.MediaType);Assert.Equal(expected.Bytes.ToArray(),actual.Bytes.ToArray());}
        Assert.DoesNotContain("sources.verify",e.Trace);var verify=await Record.ExceptionAsync(()=>prepared.VerifyBeforeCommitAsync(e.Architecture.Inner.Cancellation.Token));
        if(e.Scenario=="SOURCE-CHANGED")Assert.Equal("MODEL_SOURCE_CHANGED",Assert.IsType<DocumentationSourceCollectionException>(verify).Code);else Assert.Null(verify);
        Assert.Equal(expectedPrepare.Concat(["subset.materialize","bootstrap","render","sources.verify"]),e.Trace);Assert.All(e.Tokens,token=>Assert.Equal(e.Architecture.Inner.Cancellation.Token,token));
        for(var index=0;index<bytes.Length;index++)Assert.Equal(bytes[index],e.Inputs[index].Bytes.ToArray());
    }
}
public sealed class ScriptedFullArchitectureRecipeFactory(FullArchitectureRecipeEnvironment e,string mutation=""):IDesignFullArchitectureRecipeFactory
{
    public ArtifactGenerationRecipe Architecture(DesignArchitectureRequest request,DesignStagePlan selection,DocumentationModelSourceSnapshot snapshot,IDocumentationSourceCollection sourceCollection)
    {
        Assert.Same(e.Request,request);Assert.Same(e.Selection,selection);Assert.Same(e.Snapshot,snapshot);var services=new RecordingFullArchitectureServices(e);
        var specs=snapshot.Sources.Where(row=>row.Observation.Source.Role!=DocumentationInputRole.CanonicalContractOptional).Select(row=>new ArtifactSourceSpec(row.Observation.Source.RelativePath,row.Observation.Source.MediaType,row.Identity)).ToArray();
        if(mutation=="sources")specs=specs.Skip(1).ToArray();if(mutation=="identity")specs[0]=specs[0] with{ExpectedIdentity=null};
        return new(specs,inputs=>
        {
            if(selection.Stages.Any(stage=>stage is DesignStage.DocsVerify or DesignStage.Manifest or DesignStage.Package))throw new ArchitectureAssetException("DESIGN_STAGE_PENDING","Unsupported later-stage adapter");
            var profile=services.Read(inputs.Single(a=>a.RelativePath=="toolchain-lock.json"),inputs.Single(a=>a.RelativePath=="renderer-lock.json"));
            var model=services.Prepare(new(inputs,snapshot,selection.Stages.Contains(DesignStage.ModuleGraph)));
            var code=selection.Stages.Contains(DesignStage.CodeExtract)?services.ExtractCodeViews(new(inputs.Single(a=>a.RelativePath=="architecture/workspace.dsl"),inputs.Single(a=>a.RelativePath=="architecture/code-diagrams.md"))):Array.Empty<Artifact>();
            var inner=services.PrepareSubset(selection,inputs,profile,code,request.Renderer,async token=>{await services.AcquireAsync(profile,request.ToolAccess,request.OfflineGradleChecksumProof,mutation=="tool-token"?CancellationToken.None:token);});
            var paths=model.Ui.Artifacts.Select(a=>a.RelativePath).Concat(inner.OutputPaths).Append(snapshot.Receipt.RelativePath).ToArray();
            return new(paths,async token=>
            {
                var architecture=await inner.MaterializeAsync(token);var output=model.Ui.Artifacts.Concat(architecture).Append(snapshot.Receipt).ToArray();
                return mutation=="outputs"?output.Skip(1).ToArray():output;
            },mutation=="hook"?null:token=>sourceCollection.VerifyUnchangedAsync(snapshot,mutation=="hook-token"?CancellationToken.None:token));
        });
    }
    public ArtifactGenerationRecipe Architecture(DesignArchitectureRequest request)=>throw new InvalidOperationException("Legacy six-input recipe keeps its existing assertions");
    public ArtifactGenerationRecipe Package(DesignPackageRequest request)=>throw new InvalidOperationException("Existing package recipe keeps its existing assertions");
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DesignFullArchitectureRecipeMockTests
{
    [Theory,MemberData(nameof(FullArchitectureRecipeCases.Cases),MemberType=typeof(FullArchitectureRecipeCases))]
    public Task CorrectCapturedModelStageRecipesPass(string scenario){var e=new FullArchitectureRecipeEnvironment(scenario);return FullArchitectureRecipeCases.AssertRecipe(e,new ScriptedFullArchitectureRecipeFactory(e));}
    [Theory]
    [InlineData("sources")][InlineData("identity")][InlineData("tool-token")][InlineData("outputs")][InlineData("hook")][InlineData("hook-token")]
    public async Task EachSourceIdentityUnionAndCommitBypassIsRejected(string mutation){var e=new FullArchitectureRecipeEnvironment("FULL");Assert.NotNull(await Record.ExceptionAsync(()=>FullArchitectureRecipeCases.AssertRecipe(e,new ScriptedFullArchitectureRecipeFactory(e,mutation))));}
}
