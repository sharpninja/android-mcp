using System.Text;
using System.Text.Json.Nodes;
using System.Runtime.InteropServices;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed class CompositionEnvironment
{
    public string Scenario {get;}
    public List<string> Trace {get;}=[];
    public CancellationTokenSource Cancellation {get;}=new();
    public IReadOnlyList<Artifact> Inputs {get;}
    public IReadOnlyList<ArtifactSourceSpec> SourceSpecs {get;}
    public IReadOnlyList<Artifact> Output {get;}
    public IReadOnlyList<Artifact> CodeViews {get;}
    public DocumentationRendererProfile Profile {get;}=RendererProfileCases.Original().Expected;
    public DesignArchitectureRequest ArchitectureRequest {get;}
    public DesignPackageRequest PackageRequest {get;}
    public DocumentationPackageInput PackageInput {get;}
    public DocumentationPackage PackageOutput {get;}
    public ToolArtifactReceipt[] Tools {get;}
    public CompositionEnvironment(string scenario)
    {
        Scenario=scenario;var f=DocumentationFixture.Current;
        var context=new ArchitectureRendererContext(new("/fixture/docker",new string('a',64),"fixture"),"/fixture/work","/fixture/tools/plantuml.jar","/fixture/receipts","1000:1000");
        var offline=scenario=="ARCH-OFFLINE";ArchitectureRequest=new("generation-architecture",context,offline?ToolArtifactAccess.Offline:ToolArtifactAccess.Online,offline?"proof/gradle.json":null);
        Tools=new DocumentationBootstrapEnvironment(new("fixture",ArchitectureRequest.ToolAccess,ArchitectureRequest.OfflineGradleChecksumProof,"")).Receipts;
        var originalPackage=DocumentationPackageCases.Original().Input;
        PackageInput=originalPackage with{Artifacts=originalPackage.Artifacts.Select(a=>a with{Bytes=a.Bytes.ToArray()}).ToArray()};var checksums=DocumentationPackageCases.Checksums(PackageInput);
        PackageOutput=new(DocumentationPackageCases.FixtureArchive(PackageInput.Artifacts.Append(checksums).ToArray()),checksums);
        PackageRequest=new("generation-package",PackageInput.Artifacts.Select(a=>new StoredArtifact(a.RelativePath,DocumentationFixture.Hash(a.Bytes.Span),a.Bytes.Length,a.MediaType)).ToArray(),PackageInput.RequiredPaths,PackageInput.Policy);
        CodeViews=ArchitecturePreparationCases.CreateCode()[0].Expected;
        if(scenario.StartsWith("PACKAGE",StringComparison.Ordinal))
        {Inputs=PackageInput.Artifacts;SourceSpecs=PackageRequest.Sources.Select(s=>new ArtifactSourceSpec(s.RelativePath,s.MediaType,s)).ToArray();Output=[PackageOutput.Archive,PackageOutput.Checksums];}
        else
        {
            SourceSpecs=[new("architecture/workspace.dsl","text/plain"),new("architecture/code-diagrams.md","text/markdown"),new("architecture/module-contract.json","application/json"),new("architecture/render-skin.puml","text/plain"),new("toolchain-lock.json","application/json"),new("renderer-lock.json","application/json")];
            Inputs=SourceSpecs.Select(s=>new Artifact(s.RelativePath,s.RelativePath=="toolchain-lock.json"?RendererProfileCases.Original().Toolchain.Bytes:f.ReadOriginalDesignBytes(s.RelativePath),s.MediaType)).ToArray();
            Output=CodeViews.Concat(PackageInput.Artifacts.Where(a=>a.RelativePath.StartsWith("docs/diagrams/",StringComparison.Ordinal))).ToArray();Assert.Equal(27,Output.Count);
        }
    }
    public Artifact Input(string path)=>Inputs.Single(a=>a.RelativePath==path);
}
public sealed class RecordingCompositionServices(CompositionEnvironment e,string mutation=""):IDocumentationRendererProfileReader,IModuleGraphValidator,IArchitectureAssetPreparation,IArchitectureProjectionComposer,IDocumentationToolBootstrap,IDocumentationPackageBuilder
{
    public DocumentationRendererProfile Read(Artifact toolchain,Artifact renderer)
    {e.Trace.Add("profile");Assert.Same(e.Input("toolchain-lock.json"),toolchain);Assert.Same(e.Input("renderer-lock.json"),renderer);return e.Profile;}
    public ModuleValidationResult Validate(ModuleValidationInput input)
    {e.Trace.Add("module");Assert.Equal(Encoding.UTF8.GetString(e.Input("architecture/code-diagrams.md").Bytes.Span),input.CodeMarkdown);Assert.True(JsonNode.DeepEquals(JsonNode.Parse(e.Input("architecture/module-contract.json").Bytes.Span),input.ModuleContract));return new(96,[],e.Scenario=="ARCH-MODULE-FAIL"?[new("MODULE_CYCLE","architecture/module-contract.json","fixture")]:[]);}
    public IReadOnlyList<Artifact> ExtractCodeViews(ArchitecturePreparationInput input)
    {e.Trace.Add("code");Assert.Same(e.Input("architecture/workspace.dsl"),input.Workspace);Assert.Same(e.Input("architecture/code-diagrams.md"),input.CodeMarkdown);return e.CodeViews;}
    public Artifact NormalizeVector(Artifact rawSvg,string outputPath)=>throw new NotSupportedException("Vector normalization belongs to the renderer, not recipe composition");
    public ArchitectureCompositionPlan Plan(IReadOnlyList<Artifact> inputs,DocumentationRendererProfile profile,IReadOnlyList<Artifact> codeViews)
    {e.Trace.Add("plan");Assert.Same(e.Inputs,inputs);Assert.Same(e.Profile,profile);Assert.Same(e.CodeViews,codeViews);return new(inputs,profile,codeViews,e.Output.Select(a=>a.RelativePath).ToArray());}
    public Task<IReadOnlyList<ToolArtifactReceipt>> AcquireAsync(DocumentationRendererProfile profile,ToolArtifactAccess access,string? proof,CancellationToken cancellationToken=default)
    {e.Trace.Add("bootstrap");Assert.Same(e.Profile,profile);Assert.Equal(e.ArchitectureRequest.ToolAccess,access);Assert.Equal(e.ArchitectureRequest.OfflineGradleChecksumProof,proof);Assert.Equal(e.Cancellation.Token,cancellationToken);if(e.Scenario=="ARCH-TOOL-FAIL")throw new TransportException(mutation=="error-code"?"WRONG":"TOOL_HASH","fixture tool mismatch");return Task.FromResult<IReadOnlyList<ToolArtifactReceipt>>(e.Tools);}
    public Task<IReadOnlyList<Artifact>> RenderAsync(ArchitectureCompositionPlan plan,ArchitectureRendererContext renderer,IReadOnlyList<ToolArtifactReceipt> tools,CancellationToken cancellationToken=default)
    {e.Trace.Add("render");Assert.Same(e.Inputs,plan.Inputs);Assert.Same(e.Profile,plan.Profile);Assert.Same(e.CodeViews,plan.CodeViews);Assert.Same(e.ArchitectureRequest.Renderer,renderer);Assert.Same(e.Tools,tools);Assert.Equal(e.Cancellation.Token,cancellationToken);if(e.Scenario=="ARCH-RENDER-FAIL")throw new ArchitectureAssetException(mutation=="error-code"?"WRONG":"RENDERER_PROCESS","fixture render failure");return Task.FromResult(e.Output);}
    public DocumentationPackage Build(DocumentationPackageInput input)
    {e.Trace.Add("package");Assert.Same(e.Inputs,input.Artifacts);Assert.Equal(e.PackageInput.RequiredPaths,input.RequiredPaths);Assert.Equal(e.PackageInput.Policy.ToArray(),input.Policy.ToArray());if(e.Scenario=="PACKAGE-BUILD-FAIL")throw new ArchitectureAssetException(mutation=="error-code"?"WRONG":"PACKAGE_POLICY","fixture policy rejection");return e.PackageOutput;}
}
public sealed class ScriptedDesignRecipeFactory(CompositionEnvironment e,string mutation=""):IDesignCompositionRecipeFactory
{
    private readonly RecordingCompositionServices services=new(e,mutation);
    private IReadOnlyList<ArtifactSourceSpec> Sources()
    {
        var values=e.SourceSpecs.ToArray();if(mutation=="sources")return values.Skip(1).ToArray();
        if(mutation=="source-path")values[0]=values[0] with{RelativePath="wrong"};if(mutation=="source-media")values[0]=values[0] with{MediaType="wrong"};
        if(mutation=="source-identity")values[0]=values[0] with{ExpectedIdentity=new("wrong","wrong",0,"wrong")};return values;
    }
    private void MutateInput(IReadOnlyList<Artifact> inputs)
    {if(mutation!="mutate-input")return;Assert.True(MemoryMarshal.TryGetArray(inputs[0].Bytes,out var bytes));bytes.Array![bytes.Offset]^=1;}
    private IReadOnlyList<Artifact> Result(IReadOnlyList<Artifact> values)
    {
        if(mutation=="result")return [];var result=values.ToArray();
        if(mutation=="result-path")result[0]=result[0] with{RelativePath="wrong"};if(mutation=="result-media")result[0]=result[0] with{MediaType="wrong"};if(mutation=="result-bytes")result[0]=result[0] with{Bytes=new byte[]{0}};return result;
    }
    public ArtifactGenerationRecipe Architecture(DesignArchitectureRequest request)
    {
        Assert.Same(e.ArchitectureRequest,request);
        return new(Sources(),inputs=>
        {
            MutateInput(inputs);
            var profile=services.Read(e.Input("toolchain-lock.json"),e.Input("renderer-lock.json"));
            var model=services.Validate(new(JsonNode.Parse(e.Input("architecture/module-contract.json").Bytes.Span)!.AsObject(),Encoding.UTF8.GetString(e.Input("architecture/code-diagrams.md").Bytes.Span)));
            if(model.Issues.Count>0&&mutation!="skip-module")throw new ArchitectureAssetException(mutation=="error-code"?"WRONG":"MODULE_CONTRACT","Invalid module graph");
            var code=services.ExtractCodeViews(new(e.Input("architecture/workspace.dsl"),e.Input("architecture/code-diagrams.md")));
            var plan=services.Plan(inputs,profile,code);
            if(mutation=="early-effects")services.AcquireAsync(profile,request.ToolAccess,request.OfflineGradleChecksumProof,e.Cancellation.Token).GetAwaiter().GetResult();
            return new(mutation=="outputs"?[]:plan.OutputPaths,async token=>
            {
                var tools=mutation=="skip-bootstrap"?e.Tools:await services.AcquireAsync(profile,mutation=="access"?ToolArtifactAccess.Offline:request.ToolAccess,mutation=="proof"?"wrong":request.OfflineGradleChecksumProof,mutation=="token"?CancellationToken.None:token);
                var output=await services.RenderAsync(plan,request.Renderer,tools,token);return Result(output);
            });
        });
    }
    public ArtifactGenerationRecipe Package(DesignPackageRequest request)
    {
        Assert.Same(e.PackageRequest,request);
        return new(Sources(),inputs=>
        {
            MutateInput(inputs);
            var result=services.Build(new(inputs,request.RequiredPaths,request.Policy));
            return new(mutation=="outputs"?[]:new[]{result.Archive.RelativePath,result.Checksums.RelativePath},_=>Task.FromResult(Result([result.Archive,result.Checksums])));
        });
    }
}
public static class DesignCompositionAssertions
{
    public static async Task AssertRecipe(CompositionEnvironment e,IDesignCompositionRecipeFactory provider)
    {
        var snapshots=e.Inputs.Select(a=>a.Bytes.ToArray()).ToArray();
        void CheckInputs(){for(var index=0;index<snapshots.Length;index++)Assert.Equal(snapshots[index],e.Inputs[index].Bytes.ToArray());}
        var package=e.Scenario.StartsWith("PACKAGE",StringComparison.Ordinal);var recipe=package?provider.Package(e.PackageRequest):provider.Architecture(e.ArchitectureRequest);
        Assert.Equal(e.SourceSpecs,recipe.Sources);ArtifactGenerationPreparation? preparation=null;
        var prepareError=Record.Exception(()=>preparation=recipe.Prepare(e.Inputs));
        if(e.Scenario=="ARCH-MODULE-FAIL"){Assert.Equal("MODULE_CONTRACT",Assert.IsType<ArchitectureAssetException>(prepareError).Code);Assert.Equal(new[]{"profile","module"},e.Trace);CheckInputs();return;}
        if(e.Scenario=="PACKAGE-BUILD-FAIL"){Assert.Equal("PACKAGE_POLICY",Assert.IsType<ArchitectureAssetException>(prepareError).Code);Assert.Equal(new[]{"package"},e.Trace);CheckInputs();return;}
        Assert.Null(prepareError);Assert.Equal(e.Output.Select(a=>a.RelativePath),preparation!.OutputPaths);
        Assert.Equal(package?new[]{"package"}:new[]{"profile","module","code","plan"},e.Trace);
        // The shared generation runtime owns this barrier; materialization is deferred until it passes.
        e.Trace.Add("preflight");IReadOnlyList<Artifact>? output=null;var error=await Record.ExceptionAsync(async()=>output=await preparation.MaterializeAsync(e.Cancellation.Token));
        if(e.Scenario=="ARCH-TOOL-FAIL")Assert.Equal("TOOL_HASH",Assert.IsType<TransportException>(error).Code);
        else if(e.Scenario=="ARCH-RENDER-FAIL")Assert.Equal("RENDERER_PROCESS",Assert.IsType<ArchitectureAssetException>(error).Code);
        else {Assert.Null(error);Assert.Equal(e.Output,output);}
        var expected=package?new[]{"package","preflight"}:e.Scenario=="ARCH-TOOL-FAIL"?new[]{"profile","module","code","plan","preflight","bootstrap"}:new[]{"profile","module","code","plan","preflight","bootstrap","render"};Assert.Equal(expected,e.Trace);
        CheckInputs();
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DesignCompositionMockTests
{
    [Theory]
    [InlineData("ARCH-ONLINE")][InlineData("ARCH-OFFLINE")][InlineData("ARCH-MODULE-FAIL")][InlineData("ARCH-TOOL-FAIL")][InlineData("ARCH-RENDER-FAIL")][InlineData("PACKAGE-SUCCESS")][InlineData("PACKAGE-BUILD-FAIL")]
    public Task CorrectCompositionMocksPass(string scenario){var e=new CompositionEnvironment(scenario);return DesignCompositionAssertions.AssertRecipe(e,new ScriptedDesignRecipeFactory(e));}
    [Theory]
    [InlineData("ARCH-ONLINE","sources")][InlineData("ARCH-ONLINE","outputs")][InlineData("ARCH-ONLINE","early-effects")][InlineData("ARCH-ONLINE","access")][InlineData("ARCH-ONLINE","proof")][InlineData("ARCH-ONLINE","token")][InlineData("ARCH-ONLINE","result")]
    [InlineData("PACKAGE-SUCCESS","sources")][InlineData("PACKAGE-SUCCESS","outputs")][InlineData("PACKAGE-SUCCESS","result")]
    [InlineData("ARCH-ONLINE","source-path")][InlineData("ARCH-ONLINE","source-media")][InlineData("ARCH-ONLINE","source-identity")][InlineData("ARCH-ONLINE","result-path")][InlineData("ARCH-ONLINE","result-media")][InlineData("ARCH-ONLINE","result-bytes")][InlineData("ARCH-ONLINE","mutate-input")]
    [InlineData("PACKAGE-SUCCESS","source-path")][InlineData("PACKAGE-SUCCESS","source-media")][InlineData("PACKAGE-SUCCESS","source-identity")][InlineData("PACKAGE-SUCCESS","result-path")][InlineData("PACKAGE-SUCCESS","result-media")][InlineData("PACKAGE-SUCCESS","result-bytes")][InlineData("PACKAGE-SUCCESS","mutate-input")]
    [InlineData("ARCH-MODULE-FAIL","error-code")][InlineData("ARCH-TOOL-FAIL","error-code")][InlineData("ARCH-RENDER-FAIL","error-code")][InlineData("PACKAGE-BUILD-FAIL","error-code")][InlineData("ARCH-MODULE-FAIL","skip-module")][InlineData("ARCH-ONLINE","skip-bootstrap")]
    public async Task EachCompositionDriftIsRejected(string scenario,string mutation){var e=new CompositionEnvironment(scenario);Assert.NotNull(await Record.ExceptionAsync(()=>DesignCompositionAssertions.AssertRecipe(e,new ScriptedDesignRecipeFactory(e,mutation))));}
}
