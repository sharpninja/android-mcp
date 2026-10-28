using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation.Validation;

namespace AndroidMcp.BuildTool.Documentation;

/// <summary>Domain composition only; stores, leases, final writes and commit are shared runtime concerns.</summary>
public sealed class DesignCompositionRecipeFactory(IDocumentationRendererProfileReader profiles,IModuleGraphValidator modules,
    IArchitectureAssetPreparation preparation,IArchitectureProjectionComposer renderer,IDocumentationToolBootstrap tools,
    IDocumentationPackageBuilder packages,IDocumentationModelPreflight? modelPreflight=null,
    IGeneratedDesignValidator? documents=null,IDocumentationManifestBuilder? manifests=null,
    IDocumentationModelSourcePlanner? documentationSources=null,IArtifactReceiptVerifier? artifactReceipts=null,
    IDocumentationProjector? uiProjector=null,DocumentationLegacyReceipts? legacyReceipts=null):IDesignFullArchitectureRecipeFactory
{
    private const string ChecksumPath="docs/checksums.sha256",ArchivePath="package/android-mcp-design-source.zip";
    private readonly IDesignStagePlanner stages=new DesignStagePlanner();
    private readonly string renderSkinPath=new DocumentationProjectSourceCatalog().DescribeProjectSources(DocumentationProjectSourceScope.GeneratedDesign)
        .Single(source=>source.Kind==DocumentationProjectInputKind.RenderSkin).Source.RelativePath;

    public ArtifactGenerationRecipe Architecture(DesignArchitectureRequest request,DesignStagePlan selection)
    {
        var descriptors=new DocumentationProjectSourceCatalog().DescribeProjectSources(selection);
        if(selection.Stages.Contains(DesignStage.ModelContracts))
            throw new ArchitectureAssetException("MODEL_PREFLIGHT_REQUIRED","Selected model stages require the existing captured-source recipe");
        bool Has(DesignStage stage)=>selection.Stages.Contains(stage);
        return new(descriptors.Select(input=>new ArtifactSourceSpec(input.Source.RelativePath,input.Source.MediaType)).ToArray(),inputs=>
        {
            Artifact Input(string path)=>inputs.Single(artifact=>artifact.RelativePath==path);
            var profile=Has(DesignStage.Tools)?profiles.Read(Input("toolchain-lock.json"),Input("renderer-lock.json")):null;
            if(Has(DesignStage.ModuleGraph))
            {
                var inspected=new StrictJsonInspector().Inspect(Input("architecture/module-contract.json").Bytes);
                if(inspected.CanonicalDiagnostics.Count!=0||inspected.Value is null)
                    throw new ArchitectureAssetException("MODULE_CONTRACT","Module contract failed shared strict JSON admission");
                var module=modules.Validate(new(JsonNode.Parse(inspected.Value.Value.GetRawText())!.AsObject(),Encoding.UTF8.GetString(Input("architecture/code-diagrams.md").Bytes.Span)));
                if(module.Issues.Count!=0)throw new ArchitectureAssetException("MODULE_CONTRACT","Selected module graph failed");
            }
            var ui=Has(DesignStage.UiAssets)
                ?(uiProjector??throw new ArchitectureAssetException("DESIGN_STAGE_PENDING","Bind the existing UI projector for the selected narrow stage"))
                    .Project(new(Input(DocumentationAssetPaths.Blueprint),Input(DocumentationAssetPaths.Design))).Artifacts
                :Array.Empty<Artifact>();
            var code=Has(DesignStage.CodeExtract)?preparation.ExtractCodeViews(new(Input("architecture/workspace.dsl"),Input("architecture/code-diagrams.md"))):Array.Empty<Artifact>();
            var subset=renderer as IArchitectureStageProjectionComposer
                ??throw new ArchitectureAssetException("DESIGN_STAGE_SELECTION","Bind the existing architecture subset composer");
            Func<CancellationToken,Task>? admit=Has(DesignStage.Tools)?async token=>{await tools.AcquireAsync(profile!,request.ToolAccess,request.OfflineGradleChecksumProof,token);}:null;
            var prepared=subset.PrepareSubset(selection,inputs,profile,code,Has(DesignStage.Tools)?request.Renderer:null,admit);
            return new(ui.Select(artifact=>artifact.RelativePath).Concat(prepared.OutputPaths).ToArray(),async token=>
            {
                token.ThrowIfCancellationRequested();
                return ui.Concat(await prepared.MaterializeAsync(token)).ToArray();
            });
        });
    }

    public ArtifactGenerationRecipe Architecture(DesignArchitectureRequest request,DesignStagePlan selection,
        DocumentationModelSourceSnapshot capturedSources,IDocumentationSourceCollection sourceCollection)=>new(
        capturedSources.Sources.Where(row=>row.Observation.Source.Role!=DocumentationInputRole.CanonicalContractOptional)
            .Select(row=>new ArtifactSourceSpec(row.Observation.Source.RelativePath,row.Observation.Source.MediaType,row.Identity)).ToArray(),inputs=>
        {
            var complete=selection.Stages.Any(stage=>stage is DesignStage.DocsVerify or DesignStage.Manifest or DesignStage.Package);
            if(complete)
            {
                if(request.DocumentationStages is null||documents is null||documentationSources is null||artifactReceipts is null||
                    (selection.Stages.Contains(DesignStage.Manifest)&&manifests is null)||
                    (selection.Stages.Contains(DesignStage.Package)&&packages is not IDocumentationManifestPackageBuilder))
                    throw new ArchitectureAssetException("DESIGN_STAGE_PENDING","Bind the existing document, manifest and package completion services");
                var admitted=documentationSources.Plan(inputs.Single(a=>a.RelativePath=="contracts/source-pins.json"));
                if(!admitted.Sources.SequenceEqual(capturedSources.Sources.Select(row=>row.Observation.Source))||
                    admitted.Sources.Where(source=>source.Role!=DocumentationInputRole.CanonicalContractOptional).Any(source=>!ArtifactPaths.IsRelative(source.RelativePath)))
                    throw new ArchitectureAssetException("DESIGN_SOURCE_SCOPE","Only the exact admitted selected-source routes may be published");
            }
            var profile=profiles.Read(inputs.Single(a=>a.RelativePath=="toolchain-lock.json"),inputs.Single(a=>a.RelativePath=="renderer-lock.json"));
            var preflight=modelPreflight??throw new ArchitectureAssetException("MODEL_PREFLIGHT","Bind the shared canonical model preflight");
            var model=preflight.Prepare(new(inputs,capturedSources,selection.Stages.Contains(DesignStage.ModuleGraph)));
            var views=selection.Stages.Contains(DesignStage.CodeExtract)
                ?preparation.ExtractCodeViews(new(inputs.Single(a=>a.RelativePath=="architecture/workspace.dsl"),inputs.Single(a=>a.RelativePath=="architecture/code-diagrams.md")))
                :Array.Empty<Artifact>();
            var subset=renderer as IArchitectureStageProjectionComposer
                ??throw new ArchitectureAssetException("DESIGN_STAGE_SELECTION","Bind the shared architecture subset composer");
            var prepared=subset.PrepareSubset(selection,inputs,profile,views,request.Renderer,
                async token=>{await tools.AcquireAsync(profile,request.ToolAccess,request.OfflineGradleChecksumProof,token);});
            if(!complete)
            {
                // Preserve the existing 250-output full-architecture recipe exactly.
                return new(model.Ui.Artifacts.Select(a=>a.RelativePath).Concat(prepared.OutputPaths).Append(capturedSources.Receipt.RelativePath).ToArray(),async token=>
                {
                    var architecture=await prepared.MaterializeAsync(token);
                    return model.Ui.Artifacts.Concat(architecture).Append(capturedSources.Receipt).ToArray();
                },token=>sourceCollection.VerifyUnchangedAsync(capturedSources,token));
            }
            var architectureReceipt=DocumentationLegacyReceipts.Publication(DesignStage.ArchitectureValidate).Path;
            var additionalReceipts=legacyReceipts?.Describe(selection,true).Where(path=>path!=architectureReceipt).ToArray()??[];
            if(legacyReceipts is not null)
            {
                if(!prepared.OutputPaths.Contains(architectureReceipt,StringComparer.Ordinal))
                    throw new ArchitectureAssetException("LEGACY_RECEIPT_PENDING","Bind the existing validation-phase receipt producer");
                var owned=additionalReceipts.ToHashSet(StringComparer.OrdinalIgnoreCase);
                if(prepared.OutputPaths.Any(owned.Contains))
                    throw new ArchitectureAssetException("DESIGN_OUTPUT_COLLISION","Receipt output ownership collides");
            }
            var prerequisiteReceipts=legacyReceipts is null?Array.Empty<Artifact>():new[]{legacyReceipts.Module(model.Modules),
                legacyReceipts.Model(model.Contracts),legacyReceipts.Ui(model.Ui.Artifacts.Single(artifact=>artifact.RelativePath=="docs/ui-asset-manifest.json"))};
            var exportSupport=ExplicitExport(selection);
            var paths=CompletionPaths(inputs,model.Ui.Artifacts,prepared.OutputPaths,capturedSources.Receipt.RelativePath,selection,exportSupport,additionalReceipts);
            return new(paths,async token=>
            {
                token.ThrowIfCancellationRequested();
                var architecture=await prepared.MaterializeAsync(token);
                var union=PublishedUnion(inputs,model.Ui.Artifacts,architecture,capturedSources.Receipt,exportSupport);
                foreach(var receipt in prerequisiteReceipts)Add(union,receipt);
                var admitted=union.Values.OrderBy(artifact=>artifact.RelativePath,StringComparer.Ordinal).ToArray();
                var workspace=inputs.Single(a=>a.RelativePath=="architecture/workspace.dsl");
                var uiManifest=model.Ui.Artifacts.Single(a=>a.RelativePath=="docs/ui-asset-manifest.json");
                var before=documents!.Validate(new(admitted,model.Model,workspace,uiManifest,DocumentationValidationPhase.BeforeManifest));
                RequireValid(before);
                if(legacyReceipts is not null)
                {
                    Add(union,legacyReceipts.Docs(before));
                    union=legacyReceipts.AddPackageAliases(union.Values.ToArray(),selection.Stages.Contains(DesignStage.Package))
                        .ToDictionary(artifact=>artifact.RelativePath,StringComparer.Ordinal);
                    admitted=union.Values.OrderBy(artifact=>artifact.RelativePath,StringComparer.Ordinal).ToArray();
                }
                DocumentationManifest? manifest=null;
                if(selection.Stages.Contains(DesignStage.Manifest))
                {
                    manifest=manifests!.Build(admitted);Add(union,manifest.Checksums);
                    RequireValid(documents.Validate(new(union.Values.ToArray(),model.Model,workspace,uiManifest,DocumentationValidationPhase.AfterManifest)));
                }
                if(selection.Stages.Contains(DesignStage.Package))
                {
                    var options=request.DocumentationStages!;
                    var package=((IDocumentationManifestPackageBuilder)packages).Build(new(admitted,options.RequiredPackagePaths,options.PackagePolicy),manifest!);
                    Add(union,package.Archive);
                }
                return union.Values.ToArray();
            },token=>sourceCollection.VerifyUnchangedAsync(capturedSources,token));
        });

    private bool ExplicitExport(DesignStagePlan selection)
    {
        var aliases=stages.Describe().Metadata.Aliases;
        return selection.Aliases.Any(alias=>aliases.Any(definition=>definition.Name==alias&&definition.Selects.Contains(DesignStage.ArchitectureExport)));
    }

    private IReadOnlyList<string> CompletionPaths(IReadOnlyList<Artifact> inputs,IReadOnlyList<Artifact> ui,
        IReadOnlyList<string> architecture,string observationPath,DesignStagePlan selection,bool exportSupport,IReadOnlyList<string> additionalReceipts)
    {
        var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var artifact in inputs)Declare(artifact.RelativePath);
        foreach(var artifact in ui)
        {
            if(artifact.RelativePath==DocumentationAssetPaths.Design&&paths.Contains(artifact.RelativePath))continue;
            Declare(artifact.RelativePath);
        }
        foreach(var path in architecture)
        {
            if(exportSupport&&path==renderSkinPath&&inputs.Any(source=>source.RelativePath==path))continue;
            Declare(path);
        }
        Declare(observationPath);
        foreach(var path in additionalReceipts)Declare(path);
        if(selection.Stages.Contains(DesignStage.Manifest))Declare(ChecksumPath);
        if(selection.Stages.Contains(DesignStage.Package))Declare(ArchivePath);
        return paths.Order(StringComparer.Ordinal).ToArray();
        void Declare(string path)
        {
            if(!ArtifactPaths.IsRelative(path)||!paths.Add(path))throw new ArchitectureAssetException("DESIGN_OUTPUT_COLLISION","Unconfined or conflicting document publication path");
        }
    }

    private Dictionary<string,Artifact> PublishedUnion(IReadOnlyList<Artifact> inputs,IReadOnlyList<Artifact> ui,
        IReadOnlyList<Artifact> architecture,Artifact observations,bool exportSupport)
    {
        var union=new Dictionary<string,Artifact>(StringComparer.Ordinal);
        foreach(var artifact in inputs)Add(union,artifact);
        foreach(var artifact in ui)
        {
            if(artifact.RelativePath==DocumentationAssetPaths.Design&&union.ContainsKey(artifact.RelativePath))union[artifact.RelativePath]=artifact;
            else Add(union,artifact);
        }
        foreach(var artifact in architecture)
        {
            if(exportSupport&&artifact.RelativePath==renderSkinPath&&union.TryGetValue(artifact.RelativePath,out var retained))
            {
                if(!artifactReceipts!.Matches(artifact,artifactReceipts.Describe(retained)))
                    throw new ArchitectureAssetException("DESIGN_OUTPUT_COLLISION","Declared render-skin support differs from its captured source");
                // Retain the captured source artifact and its established identity.
                continue;
            }
            Add(union,artifact);
        }
        Add(union,observations);return union;
    }
    private static void Add(IDictionary<string,Artifact> union,Artifact artifact)
    {
        if(!union.TryAdd(artifact.RelativePath,artifact))throw new ArchitectureAssetException("DESIGN_OUTPUT_COLLISION","Undeclared document output collision");
    }
    private static void RequireValid(GeneratedDesignValidationResult result)
    {
        if(!result.IsValid)throw new DesignDocumentationValidationException(result);
    }

    public ArtifactGenerationRecipe Architecture(DesignArchitectureRequest request)=>new(
        [new("architecture/workspace.dsl","text/plain"),new("architecture/code-diagrams.md","text/markdown"),new("architecture/module-contract.json","application/json"),new("architecture/render-skin.puml","text/plain"),new("toolchain-lock.json","application/json"),new("renderer-lock.json","application/json")],inputs=>
        {
            Artifact Input(string path)=>inputs.Single(a=>a.RelativePath==path);
            var profile=profiles.Read(Input("toolchain-lock.json"),Input("renderer-lock.json"));
            var code=Input("architecture/code-diagrams.md");
            var module=modules.Validate(new(JsonNode.Parse(Input("architecture/module-contract.json").Bytes.Span)!.AsObject(),Encoding.UTF8.GetString(code.Bytes.Span)));
            if(module.Issues.Count>0)throw new ArchitectureAssetException("MODULE_CONTRACT","Architecture module contract failed");
            var views=preparation.ExtractCodeViews(new(Input("architecture/workspace.dsl"),code));
            var plan=renderer.Plan(inputs,profile,views);
            return new(plan.OutputPaths,async token=>
            {
                var admitted=await tools.AcquireAsync(profile,request.ToolAccess,request.OfflineGradleChecksumProof,token);
                return await renderer.RenderAsync(plan,request.Renderer,admitted,token);
            });
        });
    public ArtifactGenerationRecipe Package(DesignPackageRequest request)=>new(
        request.Sources.Select(s=>new ArtifactSourceSpec(s.RelativePath,s.MediaType,s)).ToArray(),inputs=>
        {
            var package=packages.Build(new(inputs,request.RequiredPaths,request.Policy));
            IReadOnlyList<Artifact> artifacts=[package.Archive,package.Checksums];
            return new(artifacts.Select(a=>a.RelativePath).ToArray(),_=>Task.FromResult(artifacts));
        });
}
