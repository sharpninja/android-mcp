using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Documentation.Validation;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Tests.Documentation.Validation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed class DesignDocumentationStageEnvironment
{
    public string Scenario {get;}
    public FullArchitectureRecipeEnvironment Original {get;}=new("FULL");
    public List<string> Trace {get;}=[];
    public CancellationTokenSource Cancellation {get;}=new();
    public IReadOnlyList<Artifact> Inputs {get;}
    public DocumentationModelSourcePlan SourcePlan {get;}=DocumentationSelectedProjectSourceCases.ExpectedPlan(DocumentationProjectSourceScope.GeneratedDesign);
    public DocumentationModelSourceSnapshot Snapshot {get;}
    public DocumentationModelPreflightResult Preflight {get;}
    public DesignArchitectureRequest Request {get;}
    public DesignStagePlan Selection {get;}
    public IReadOnlyList<Artifact> Architecture {get;}
    public IReadOnlyList<Artifact> BaseUnion {get;}
    public DocumentationManifest Manifest {get;}
    public DocumentationPackage Package {get;}
    public GeneratedDesignValidationResult Before {get;}
    public GeneratedDesignValidationResult After {get;}
    public string RenderSkinPath=>DocumentationSelectedProjectSourceCases.Expected(DocumentationProjectSourceScope.GeneratedDesign)
        .Single(input=>input.Kind==DocumentationProjectInputKind.RenderSkin).Source.RelativePath;
    public bool ExplicitExport=>Scenario.StartsWith("EXPORT-",StringComparison.Ordinal);
    public bool Has(DesignStage stage)=>Selection.Stages.Contains(stage);
    public DesignDocumentationStageEnvironment(string scenario)
    {
        Scenario=scenario;var corpus=GeneratedDesignValidationCorpusCases.Create()[0];var fixture=DocumentationFixture.Current;
        Inputs=SourcePlan.Sources.Where(source=>source.Role!=DocumentationInputRole.CanonicalContractOptional)
            .Select(source=>source.RelativePath==DocumentationAssetPaths.Design?fixture.Input.DesignMarkdown:corpus.Input.Artifacts.Single(a=>a.RelativePath==source.RelativePath)).ToArray();
        Assert.Equal(18,Inputs.Count);
        var rows=SourcePlan.Sources.Select(source=>
        {
            var content=source.Role==DocumentationInputRole.CanonicalContractOptional?null:Inputs.Single(a=>a.RelativePath==source.RelativePath);
            return new DocumentationCapturedSource(new(source,content),content is null?null:new ArtifactReceiptVerifier(new NativeContentHash()).Describe(content));
        }).ToArray();
        if(scenario is "UNKNOWN-ROUTE" or "UNSAFE-ROUTE" or "FOREIGN-ROLE" or "FOREIGN-ID" or "FOREIGN-MEDIA" or "FOREIGN-CONTRACT" or "FOREIGN-HASH")
        {
            var row=rows.Single(value=>value.Observation.Source.RelativePath=="README.md");var index=Array.IndexOf(rows,row);
            var changed=scenario switch
            {
                "UNKNOWN-ROUTE"=>row.Observation.Source with{RelativePath="contracts/unmapped.md"},
                "UNSAFE-ROUTE"=>row.Observation.Source with{RelativePath="../outside.md"},
                "FOREIGN-ROLE"=>row.Observation.Source with{Role=DocumentationInputRole.CanonicalContractOptional},
                "FOREIGN-ID"=>row.Observation.Source with{ArtifactId="project:unmapped"},
                "FOREIGN-MEDIA"=>row.Observation.Source with{MediaType="text/plain"},
                "FOREIGN-CONTRACT"=>row.Observation.Source with{ContractId=DocumentationContractId.OpenApi},
                _=>row.Observation.Source with{ExpectedSha256=new string('a',64)}
            };
            rows[index]=row with{Observation=row.Observation with{Source=changed}};
        }
        Snapshot=new(rows,SourceCollectionEnvironment.ExpectedReceipt(rows));
        Preflight=new(corpus.Input.Model,fixture.Correct,Original.Model.Modules,Original.Model.Validated);
        var planner=new DesignStagePlanner();var aliases=ExplicitExport?new[]{"exportArchitecture","packageDesignDocs"}:scenario=="UNION"?new[]{"verifyDesignDocs","generateArtifactManifest","packageDesignDocs"}:
            scenario is "VERIFY" or "BEFORE-FAIL" or "UNKNOWN-ROUTE" or "UNSAFE-ROUTE" or "UNKNOWN-COLLISION"?new[]{"verifyDesignDocs"}:
            scenario=="MANIFEST"?new[]{"generateArtifactManifest"}:new[]{"packageDesignDocs"};
        Selection=planner.Select(new(aliases,planner.Describe().Sha256));Assert.Empty(Selection.Errors);
        Request=Original.Request with{DocumentationStages=new([],new byte[]{7,8,9})};
        if(scenario=="OPTIONS-MISSING")Request=Request with{DocumentationStages=null};
        Architecture=ExplicitExport?new ArchitectureSubsetEnvironment(new("COMPLETION-EXPORT",aliases)).ExpectedPublic:
            Original.Architecture.Code.Concat(corpus.Input.Artifacts.Where(a=>a.RelativePath.StartsWith("docs/diagrams/",StringComparison.Ordinal))).ToArray();
        if(scenario is "EXPORT-SKIN-BYTES" or "EXPORT-SKIN-LENGTH" or "EXPORT-SKIN-MEDIA")
        {
            Architecture=Architecture.Select(artifact=>artifact.RelativePath!=RenderSkinPath?artifact:scenario switch
            {
                "EXPORT-SKIN-BYTES"=>artifact with{Bytes=Enumerable.Repeat((byte)'x',artifact.Bytes.Length).ToArray()},
                "EXPORT-SKIN-LENGTH"=>artifact with{Bytes=artifact.Bytes.ToArray().Append((byte)'x').ToArray()},
                _=>artifact with{MediaType="application/octet-stream"}
            }).ToArray();
        }
        if(scenario=="EXPORT-UNDECLARED")Architecture=Architecture.Append(Inputs.Single(a=>a.RelativePath=="README.md") with{}).ToArray();
        if(scenario=="UNKNOWN-COLLISION")Architecture=Architecture.Append(Inputs.Single(a=>a.RelativePath=="README.md") with{Bytes=new byte[]{0}}).ToArray();
        var union=Inputs.ToDictionary(a=>a.RelativePath,StringComparer.Ordinal);
        foreach(var output in Preflight.Ui.Artifacts)union[output.RelativePath]=output;
        foreach(var output in Architecture)if(!union.ContainsKey(output.RelativePath))union.Add(output.RelativePath,output);
        union.Add(Snapshot.Receipt.RelativePath,Snapshot.Receipt);BaseUnion=union.Values.OrderBy(a=>a.RelativePath,StringComparer.Ordinal).ToArray();
        var checksum=DocumentationPackageCases.Checksums(new(BaseUnion,[],ReadOnlyMemory<byte>.Empty));
        Manifest=new(checksum,BaseUnion.Select(a=>new ArtifactReceiptVerifier(new NativeContentHash()).Describe(a)).ToArray());
        Package=new(DocumentationPackageCases.FixtureArchive(BaseUnion.Append(checksum).ToArray()),checksum);
        Before=corpus.Expected;After=Before with{Phase=DocumentationValidationPhase.AfterManifest,Counters=Before.Counters with{ChecksumsChecked=BaseUnion.Count}};
        if(scenario=="BEFORE-FAIL")Before=Before with{Issues=[new("DOCS_LINK_MISSING","README.md","fixture")],ParseDiagnostics=[new("docs/diagrams/fixture.svg",3,5,"retained before diagnostic")]};
        if(scenario=="AFTER-FAIL")After=After with{Issues=[new("DOCS_CHECKSUM_HASH","docs/checksums.sha256","fixture")],ParseDiagnostics=[new("docs/diagrams/fixture.svg",7,11,"retained after diagnostic")]};
        if(scenario=="CANCEL")Cancellation.Cancel();
    }
}
public sealed class RecordingDesignDocumentationStageServices(DesignDocumentationStageEnvironment e):
    IDocumentationRendererProfileReader,IDocumentationModelSourcePlanner,IDocumentationModelPreflight,
    IArchitectureAssetPreparation,IArchitectureStageProjectionComposer,IDocumentationToolBootstrap,
    IGeneratedDesignValidator,IDocumentationManifestBuilder,IDocumentationManifestPackageBuilder,IDocumentationSourceCollection
{
    public DocumentationModelSourcePlan Plan(Artifact pins)
    {e.Trace.Add("sources.plan");Assert.Same(e.Inputs.Single(a=>a.RelativePath=="contracts/source-pins.json"),pins);return e.SourcePlan;}
    public DocumentationRendererProfile Read(Artifact toolchain,Artifact renderer)
    {e.Trace.Add("profile");Assert.Same(e.Inputs.Single(a=>a.RelativePath=="toolchain-lock.json"),toolchain);Assert.Same(e.Inputs.Single(a=>a.RelativePath=="renderer-lock.json"),renderer);return e.Original.Architecture.Inner.Composition.Profile;}
    public DocumentationModelPreflightResult Prepare(DocumentationModelPreflightInput input)
    {e.Trace.Add("model");Assert.Same(e.Inputs,input.RequiredArtifacts);Assert.Same(e.Snapshot,input.Sources);Assert.True(input.EnforceModuleGraph);return e.Preflight;}
    public IReadOnlyList<Artifact> ExtractCodeViews(ArchitecturePreparationInput input)
    {e.Trace.Add("code");Assert.Same(e.Inputs.Single(a=>a.RelativePath=="architecture/workspace.dsl"),input.Workspace);Assert.Same(e.Inputs.Single(a=>a.RelativePath=="architecture/code-diagrams.md"),input.CodeMarkdown);return e.Original.Architecture.Code;}
    public Artifact NormalizeVector(Artifact raw,string path)=>throw new InvalidOperationException("Existing renderer owns normalization");
    public ArtifactGenerationPreparation PrepareSubset(DesignStagePlan selection,IReadOnlyList<Artifact> inputs,DocumentationRendererProfile? profile,IReadOnlyList<Artifact> code,ArchitectureRendererContext? context,Func<CancellationToken,Task>? admit)
    {
        e.Trace.Add("subset.prepare");Assert.Same(e.Selection,selection);Assert.Same(e.Inputs,inputs);Assert.Same(e.Request.Renderer,context);Assert.Equal(e.Original.Architecture.Code,code);Assert.NotNull(admit);
        return new(e.Architecture.Select(a=>a.RelativePath).ToArray(),async token=>{e.Trace.Add("subset.materialize");Assert.Equal(e.Cancellation.Token,token);await admit(token);e.Trace.Add("render");if(e.Scenario=="RENDER-FAIL")throw new ArchitectureAssetException("RENDERER_PROCESS","fixture");return e.Architecture;});
    }
    public Task<IReadOnlyList<ToolArtifactReceipt>> AcquireAsync(DocumentationRendererProfile profile,ToolArtifactAccess access,string? proof,CancellationToken token=default)
    {e.Trace.Add("bootstrap");Assert.Equal(e.Cancellation.Token,token);Assert.Equal(e.Request.ToolAccess,access);Assert.Equal(e.Request.OfflineGradleChecksumProof,proof);return Task.FromResult<IReadOnlyList<ToolArtifactReceipt>>(e.Original.Architecture.Inner.Composition.Tools);}
    public GeneratedDesignValidationResult Validate(GeneratedDesignValidationInput input)
    {
        var after=input.Phase==DocumentationValidationPhase.AfterManifest;e.Trace.Add(after?"docs.after":"docs.before");
        Assert.Same(e.Preflight.Model,input.Model);Assert.Same(e.Inputs.Single(a=>a.RelativePath=="architecture/workspace.dsl"),input.WorkspaceDsl);
        Assert.Same(e.Preflight.Ui.Artifacts.Single(a=>a.RelativePath=="docs/ui-asset-manifest.json"),input.UiManifest);
        DesignDocumentationStageCases.AssertArtifacts(after?e.BaseUnion.Append(e.Manifest.Checksums).ToArray():e.BaseUnion,input.Artifacts);
        return after?e.After:e.Before;
    }
    public DocumentationManifest Build(IReadOnlyList<Artifact> artifacts)
    {e.Trace.Add("manifest");DesignDocumentationStageCases.AssertArtifacts(e.BaseUnion,artifacts);return e.Manifest;}
    public void Verify(IReadOnlyList<Artifact> artifacts,DocumentationManifest manifest)=>throw new InvalidOperationException("Package overload owns prebuilt-manifest admission, covered by its separate cases");
    public DocumentationPackage Build(DocumentationPackageInput input,DocumentationManifest manifest)
    {
        e.Trace.Add("package");DesignDocumentationStageCases.AssertArtifacts(e.BaseUnion,input.Artifacts);Assert.Same(e.Manifest,manifest);
        Assert.Equal(e.Request.DocumentationStages!.RequiredPackagePaths,input.RequiredPaths);Assert.Equal(e.Request.DocumentationStages.PackagePolicy.ToArray(),input.Policy.ToArray());
        if(e.Scenario=="PACKAGE-FAIL")throw new ArchitectureAssetException("PACKAGE_POLICY","fixture");return e.Package;
    }
    public Task VerifyUnchangedAsync(DocumentationModelSourceSnapshot snapshot,CancellationToken token=default)
    {e.Trace.Add("sources.verify");Assert.Same(e.Snapshot,snapshot);Assert.Equal(e.Cancellation.Token,token);if(e.Scenario=="SOURCE-CHANGED")throw new DocumentationSourceCollectionException("MODEL_SOURCE_CHANGED",["project:README.md"]);return Task.CompletedTask;}
    public Task<DocumentationModelSourceSnapshot> CaptureAsync(CancellationToken token=default)=>throw new InvalidOperationException("Host captures the one admitted source set");
    public DocumentationPackage Build(DocumentationPackageInput input)=>throw new InvalidOperationException("Use the already-built manifest overload");
    public ArchitectureCompositionPlan Plan(IReadOnlyList<Artifact> inputs,DocumentationRendererProfile profile,IReadOnlyList<Artifact> code)=>throw new InvalidOperationException("One existing subset engine");
    public Task<IReadOnlyList<Artifact>> RenderAsync(ArchitectureCompositionPlan plan,ArchitectureRendererContext context,IReadOnlyList<ToolArtifactReceipt> tools,CancellationToken token=default)=>throw new InvalidOperationException("One existing subset engine");
}
public static class DesignDocumentationStageCases
{
    public static IEnumerable<object[]> Cases=>new[]{"VERIFY","MANIFEST","PACKAGE","UNION","EXPORT-UNION","EXPORT-SKIN-BYTES","EXPORT-SKIN-LENGTH","EXPORT-SKIN-MEDIA","EXPORT-UNDECLARED","BEFORE-FAIL","AFTER-FAIL","PACKAGE-FAIL","SOURCE-CHANGED","RENDER-FAIL","CANCEL","UNKNOWN-ROUTE","UNSAFE-ROUTE","FOREIGN-ROLE","FOREIGN-ID","FOREIGN-MEDIA","FOREIGN-CONTRACT","FOREIGN-HASH","UNKNOWN-COLLISION","OPTIONS-MISSING"}.Select(s=>new object[]{s});
    public static void AssertArtifacts(IReadOnlyList<Artifact> expected,IReadOnlyList<Artifact> actual)
    {
        Assert.Equal(expected.Select(a=>a.RelativePath).Order(StringComparer.Ordinal),actual.Select(a=>a.RelativePath).Order(StringComparer.Ordinal));
        foreach(var artifact in expected){var found=actual.Single(a=>a.RelativePath==artifact.RelativePath);Assert.Equal(artifact.MediaType,found.MediaType);Assert.Equal(artifact.Bytes.ToArray(),found.Bytes.ToArray());}
    }
    public static async Task AssertContract(DesignDocumentationStageEnvironment e,IDesignFullArchitectureRecipeFactory provider)
    {
        var sourceBytes=e.Inputs.Select(a=>a.Bytes.ToArray()).ToArray();var services=new RecordingDesignDocumentationStageServices(e);
        var recipe=provider.Architecture(e.Request,e.Selection,e.Snapshot,services);Assert.Empty(e.Trace);
        Assert.Equal(e.Snapshot.Sources.Where(row=>row.Observation.Source.Role!=DocumentationInputRole.CanonicalContractOptional).Select(row=>new ArtifactSourceSpec(row.Observation.Source.RelativePath,row.Observation.Source.MediaType,row.Identity)),recipe.Sources);
        ArtifactGenerationPreparation? preparation=null;var early=Record.Exception(()=>preparation=recipe.Prepare(e.Inputs));
        if(e.Scenario=="OPTIONS-MISSING"){Assert.Equal("DESIGN_STAGE_PENDING",Assert.IsType<ArchitectureAssetException>(early).Code);Assert.Empty(e.Trace);return;}
        if(e.Scenario is "UNKNOWN-ROUTE" or "UNSAFE-ROUTE" or "FOREIGN-ROLE" or "FOREIGN-ID" or "FOREIGN-MEDIA" or "FOREIGN-CONTRACT" or "FOREIGN-HASH")
        {Assert.Equal("DESIGN_SOURCE_SCOPE",Assert.IsType<ArchitectureAssetException>(early).Code);Assert.Equal(new[]{"sources.plan"},e.Trace);return;}
        if(e.Scenario is "UNKNOWN-COLLISION" or "EXPORT-UNDECLARED")
        {Assert.Equal("DESIGN_OUTPUT_COLLISION",Assert.IsType<ArchitectureAssetException>(early).Code);Assert.DoesNotContain("bootstrap",e.Trace);Assert.DoesNotContain("docs.before",e.Trace);return;}
        Assert.Null(early);Assert.NotNull(preparation);Assert.NotNull(preparation.VerifyBeforeCommitAsync);
        var expected=e.BaseUnion.Concat(e.Has(DesignStage.Manifest)?new[]{e.Manifest.Checksums}:Array.Empty<Artifact>()).Concat(e.Has(DesignStage.Package)?new[]{e.Package.Archive}:Array.Empty<Artifact>()).ToArray();
        Assert.Equal(expected.Select(a=>a.RelativePath).Order(StringComparer.Ordinal),preparation.OutputPaths.Order(StringComparer.Ordinal));
        Assert.Equal(new[]{"sources.plan","profile","model","code","subset.prepare"},e.Trace);
        IReadOnlyList<Artifact>? result=null;var error=await Record.ExceptionAsync(async()=>result=await preparation.MaterializeAsync(e.Cancellation.Token));
        if(e.Scenario=="CANCEL"){Assert.IsAssignableFrom<OperationCanceledException>(error);Assert.Equal(5,e.Trace.Count);return;}
        if(e.Scenario=="RENDER-FAIL"){Assert.Equal("RENDERER_PROCESS",Assert.IsType<ArchitectureAssetException>(error).Code);Assert.DoesNotContain("docs.before",e.Trace);return;}
        if(e.Scenario is "EXPORT-SKIN-BYTES" or "EXPORT-SKIN-LENGTH" or "EXPORT-SKIN-MEDIA")
        {Assert.Equal("DESIGN_OUTPUT_COLLISION",Assert.IsType<ArchitectureAssetException>(error).Code);Assert.DoesNotContain("docs.before",e.Trace);Assert.DoesNotContain("manifest",e.Trace);Assert.DoesNotContain("package",e.Trace);Assert.DoesNotContain("sources.verify",e.Trace);return;}
        if(e.Scenario is "BEFORE-FAIL" or "AFTER-FAIL")
        {
            var failure=Assert.IsType<DesignDocumentationValidationException>(error);Assert.Equal("DESIGN_DOCUMENTATION_INVALID",failure.Code);Assert.Same(e.Scenario=="BEFORE-FAIL"?e.Before:e.After,failure.Result);
            Assert.Single(failure.Result.ParseDiagnostics);Assert.DoesNotContain("package",e.Trace);Assert.DoesNotContain("sources.verify",e.Trace);if(e.Scenario=="BEFORE-FAIL")Assert.DoesNotContain("manifest",e.Trace);return;
        }
        if(e.Scenario=="PACKAGE-FAIL"){Assert.Equal("PACKAGE_POLICY",Assert.IsType<ArchitectureAssetException>(error).Code);Assert.DoesNotContain("sources.verify",e.Trace);return;}
        Assert.Null(error);Assert.NotNull(result);AssertArtifacts(expected,result);
        // All admitted sources are physically published, not merely validated in memory.
        foreach(var source in e.Inputs.Where(a=>a.RelativePath!=DocumentationAssetPaths.Design))Assert.Same(source,result.Single(a=>a.RelativePath==source.RelativePath));
        Assert.Same(e.Preflight.Ui.Artifacts.Single(a=>a.RelativePath==DocumentationAssetPaths.Design),result.Single(a=>a.RelativePath==DocumentationAssetPaths.Design));
        Assert.DoesNotContain("sources.verify",e.Trace);var check=await Record.ExceptionAsync(()=>preparation.VerifyBeforeCommitAsync(e.Cancellation.Token));
        if(e.Scenario=="SOURCE-CHANGED")Assert.Equal("MODEL_SOURCE_CHANGED",Assert.IsType<DocumentationSourceCollectionException>(check).Code);else Assert.Null(check);
        var trace=new List<string>{"sources.plan","profile","model","code","subset.prepare","subset.materialize","bootstrap","render","docs.before"};
        if(e.Has(DesignStage.Manifest))trace.AddRange(["manifest","docs.after"]);if(e.Has(DesignStage.Package))trace.Add("package");trace.Add("sources.verify");Assert.Equal(trace,e.Trace);
        for(var index=0;index<sourceBytes.Length;index++)Assert.Equal(sourceBytes[index],e.Inputs[index].Bytes.ToArray());
    }
}
public sealed class ScriptedDesignDocumentationStageFactory(DesignDocumentationStageEnvironment e,string mutation=""):IDesignFullArchitectureRecipeFactory
{
    public ArtifactGenerationRecipe Architecture(DesignArchitectureRequest request,DesignStagePlan selected,DocumentationModelSourceSnapshot snapshot,IDocumentationSourceCollection sources)
    {
        var s=new RecordingDesignDocumentationStageServices(e);
        return new(snapshot.Sources.Where(row=>row.Observation.Source.Role!=DocumentationInputRole.CanonicalContractOptional).Select(row=>new ArtifactSourceSpec(row.Observation.Source.RelativePath,row.Observation.Source.MediaType,row.Identity)).ToArray(),inputs=>
        {
            if(request.DocumentationStages is null)throw new ArchitectureAssetException("DESIGN_STAGE_PENDING","Bind existing completion services and options");
            var plan=s.Plan(inputs.Single(a=>a.RelativePath=="contracts/source-pins.json"));
            if(mutation!="route"&&!plan.Sources.SequenceEqual(snapshot.Sources.Select(row=>row.Observation.Source)))throw new ArchitectureAssetException("DESIGN_SOURCE_SCOPE","Only admitted selected sources can be published");
            var profile=s.Read(inputs.Single(a=>a.RelativePath=="toolchain-lock.json"),inputs.Single(a=>a.RelativePath=="renderer-lock.json"));var model=s.Prepare(new(inputs,snapshot,true));
            var code=s.ExtractCodeViews(new(inputs.Single(a=>a.RelativePath=="architecture/workspace.dsl"),inputs.Single(a=>a.RelativePath=="architecture/code-diagrams.md")));
            var inner=s.PrepareSubset(selected,inputs,profile,code,request.Renderer,async token=>{await s.AcquireAsync(profile,request.ToolAccess,request.OfflineGradleChecksumProof,token);});
            var permitted=new HashSet<string>(inputs.Select(a=>a.RelativePath),StringComparer.Ordinal);permitted.Remove(DocumentationAssetPaths.Design);
            if(e.ExplicitExport)permitted.Remove(e.RenderSkinPath);
            if(mutation!="collision"&&inner.OutputPaths.Concat(model.Ui.Artifacts.Select(a=>a.RelativePath)).Any(path=>permitted.Contains(path)))throw new ArchitectureAssetException("DESIGN_OUTPUT_COLLISION","Only the owned generated design Markdown can replace its source");
            var output=e.BaseUnion.Select(a=>a.RelativePath).Concat(e.Has(DesignStage.Manifest)?new[]{e.Manifest.Checksums.RelativePath}:Array.Empty<string>()).Concat(e.Has(DesignStage.Package)?new[]{e.Package.Archive.RelativePath}:Array.Empty<string>()).ToArray();
            return new(output,async token=>
            {
                if(mutation!="cancel")token.ThrowIfCancellationRequested();var rendered=await inner.MaterializeAsync(token);
                var union=inputs.ToDictionary(a=>a.RelativePath,StringComparer.Ordinal);foreach(var value in model.Ui.Artifacts)union[value.RelativePath]=value;
                var receipts=new ArtifactReceiptVerifier(new NativeContentHash());
                foreach(var value in rendered)
                {
                    if(e.ExplicitExport&&value.RelativePath==e.RenderSkinPath&&union.TryGetValue(value.RelativePath,out var retained))
                    {
                        if(mutation!="skin-identity"&&!receipts.Matches(value,receipts.Describe(retained)))throw new ArchitectureAssetException("DESIGN_OUTPUT_COLLISION","Declared render-skin support differs from its captured source");
                        if(mutation=="replace-skin")union[value.RelativePath]=value;
                        continue;
                    }
                    if(mutation!="collision"&&union.ContainsKey(value.RelativePath))throw new ArchitectureAssetException("DESIGN_OUTPUT_COLLISION","Undeclared output collision");
                    union[value.RelativePath]=value;
                }
                union.Add(snapshot.Receipt.RelativePath,snapshot.Receipt);
                var admitted=union.Values.OrderBy(a=>a.RelativePath,StringComparer.Ordinal).ToArray();
                var before=s.Validate(new(admitted,mutation=="model"?e.Original.Model.Model:model.Model,inputs.Single(a=>a.RelativePath=="architecture/workspace.dsl"),model.Ui.Artifacts.Single(a=>a.RelativePath=="docs/ui-asset-manifest.json"),DocumentationValidationPhase.BeforeManifest));
                if(!before.IsValid&&mutation!="before")throw new DesignDocumentationValidationException(mutation=="before-result"?before with{ParseDiagnostics=[]}:before);
                if(mutation=="omit-source")admitted=admitted.Where(a=>a.RelativePath!="README.md").ToArray();
                DocumentationManifest? manifest=null;if(e.Has(DesignStage.Manifest))
                {
                    manifest=s.Build(admitted);if(mutation=="twice")s.Build(admitted);union.Add(manifest.Checksums.RelativePath,manifest.Checksums);
                    var after=s.Validate(new(union.Values.ToArray(),model.Model,inputs.Single(a=>a.RelativePath=="architecture/workspace.dsl"),model.Ui.Artifacts.Single(a=>a.RelativePath=="docs/ui-asset-manifest.json"),DocumentationValidationPhase.AfterManifest));
                    if(!after.IsValid&&mutation!="after")throw new DesignDocumentationValidationException(mutation=="after-result"?after with{ParseDiagnostics=[]}:after);
                }
                if(e.Has(DesignStage.Package)){var package=s.Build(new(admitted,request.DocumentationStages.RequiredPackagePaths,request.DocumentationStages.PackagePolicy),manifest!);union.Add(package.Archive.RelativePath,package.Archive);}
                var result=union.Values.ToArray();return mutation=="result-source"?result.Where(a=>a.RelativePath!="README.md").ToArray():result;
            },mutation=="hook"?null:token=>sources.VerifyUnchangedAsync(snapshot,mutation=="hook-token"?CancellationToken.None:token));
        });
    }
    public ArtifactGenerationRecipe Architecture(DesignArchitectureRequest request)=>throw new InvalidOperationException("Existing legacy recipe assertions remain unchanged");
    public ArtifactGenerationRecipe Package(DesignPackageRequest request)=>throw new InvalidOperationException("Existing package recipe assertions remain unchanged");
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DesignDocumentationStageMockTests
{
    [Theory,MemberData(nameof(DesignDocumentationStageCases.Cases),MemberType=typeof(DesignDocumentationStageCases))]
    public Task CorrectExistingStageConjunctionMocksPass(string scenario)
    {var e=new DesignDocumentationStageEnvironment(scenario);return DesignDocumentationStageCases.AssertContract(e,new ScriptedDesignDocumentationStageFactory(e));}
    [Theory]
    [InlineData("UNKNOWN-ROUTE","route")][InlineData("UNKNOWN-COLLISION","collision")][InlineData("BEFORE-FAIL","before")][InlineData("AFTER-FAIL","after")]
    [InlineData("BEFORE-FAIL","before-result")][InlineData("AFTER-FAIL","after-result")]
    [InlineData("EXPORT-SKIN-BYTES","skin-identity")][InlineData("EXPORT-SKIN-LENGTH","skin-identity")][InlineData("EXPORT-SKIN-MEDIA","skin-identity")]
    [InlineData("EXPORT-UNDECLARED","collision")][InlineData("EXPORT-UNION","replace-skin")]
    [InlineData("PACKAGE","twice")][InlineData("PACKAGE","model")][InlineData("PACKAGE","omit-source")][InlineData("VERIFY","result-source")]
    [InlineData("VERIFY","hook")][InlineData("VERIFY","hook-token")][InlineData("CANCEL","cancel")]
    public async Task EachRouteValidationCoveragePublicationAndCommitBypassIsRejected(string scenario,string mutation)
    {var e=new DesignDocumentationStageEnvironment(scenario);Assert.NotNull(await Record.ExceptionAsync(()=>DesignDocumentationStageCases.AssertContract(e,new ScriptedDesignDocumentationStageFactory(e,mutation))));}
}
