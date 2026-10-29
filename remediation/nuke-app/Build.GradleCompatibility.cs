using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Documentation.Validation;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Sources;

namespace AndroidMcp.BuildTool;

// Staged caller binding. The narrow factory overload must pass its same-case
// gate before this partial is copied; existing GenerateArchitecture is retained.
public sealed partial class Build
{
    private async Task GenerateGradleCompatibility()
    {
        var stages=new DesignStagePlanner();
        var selection=stages.Select(new(DesignAliases!.Split(',',StringSplitOptions.None),DesignRegistrySha256!));
        if(selection.Errors.Count!=0)throw new InvalidOperationException(string.Join(", ",selection.Errors.Select(error=>error.Code+":"+error.Value)));
        bool Has(DesignStage stage)=>selection.Stages.Contains(stage);
        var input=AdmitWorkflowRoot(Path.GetFullPath(ProjectRoot!),".");
        var output=AdmitWorkflowRoot(input,OutputRoot!);var work=AdmitWorkflowRoot(input,WorkRoot!);
        var cache=AdmitWorkflowRoot(input,CacheRoot!);var receiptRoot=AdmitWorkflowRoot(input,ReceiptRoot!);
        var comparer=OperatingSystem.IsWindows()?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal;
        var effectRoots=new[]{output,work,receiptRoot};
        if(effectRoots.Distinct(comparer).Count()!=effectRoots.Length||effectRoots.Any(root=>comparer.Equals(root,input)||comparer.Equals(root,cache)))
            throw new InvalidOperationException("Design final, work and receipt roots must be separate from retained source/cache roots and each other.");
        var hashes=new NativeContentHash();var verifier=new ArtifactReceiptVerifier(hashes);
        var canonical=new CanonicalJson(bytes=>hashes.Sha256(bytes.Span));var strict=new StrictJsonInspector();
        var source=new FileArtifactStore(input,bytes=>hashes.Sha256(bytes.Span));
        var configuration=new List<Artifact>();
        var definition=new OperationRegistry().Find(new(Category.Design,Operation.GradleCompatibility))!;
        var projection=new NativeDesignGradleProjection(canonical,verifier);
        var expectedProjection=projection.Render(stages.Describe(),definition);
        foreach(var expected in new[]{expectedProjection.Descriptor,expectedProjection.BuildScript})
            configuration.Add(new(expected.RelativePath,await source.ReadAsync(expected.RelativePath),expected.MediaType));
        var stale=projection.Check(stages.Describe(),definition,configuration);
        if(stale.Count!=0)throw new InvalidOperationException("Retained Gradle projection is stale: "+string.Join(", ",stale.Select(issue=>issue.Code+":"+issue.RelativePath)));

        Artifact? policy=null;
        if(Has(DesignStage.Package))
        {
            if(string.IsNullOrWhiteSpace(PolicyRelativePath))throw new InvalidOperationException("Package selection requires explicit --policy-relative-path; no policy default is assumed.");
            _=ArtifactPaths.Resolve(input,PolicyRelativePath);
            policy=new(PolicyRelativePath,await source.ReadAsync(PolicyRelativePath),"application/json");
            configuration.Add(policy);
        }

        DocumentationSourceCollection? collector=null;DocumentationModelSourceSnapshot? snapshot=null;
        DocumentationModelSourcePlanner? sourcePlanner=null;IReadOnlyList<Artifact>? captured=null;
        if(Has(DesignStage.ModelContracts))
        {
            var expectedCatalog=Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(input,"..","catalog")));
            var catalog=AdmitWorkflowRoot(input,CatalogRoot??expectedCatalog);
            if(!comparer.Equals(catalog,expectedCatalog)||effectRoots.Any(root=>comparer.Equals(root,catalog)))
                throw new InvalidOperationException("The current source pins admit only the separate project-sibling catalog root.");
            sourcePlanner=new(strict,Has(DesignStage.DocsVerify)?DocumentationProjectSourceScope.GeneratedDesign:DocumentationProjectSourceScope.Model);
            var pins=new Artifact("contracts/source-pins.json",await source.ReadAsync("contracts/source-pins.json"),"application/json");
            collector=new(source,new FileArtifactStore(catalog,bytes=>hashes.Sha256(bytes.Span)),pins,sourcePlanner,verifier,canonical);
            snapshot=await collector.CaptureAsync();captured=snapshot.Sources.Where(row=>row.Observation.Source.Role!=DocumentationInputRole.CanonicalContractOptional).Select(row=>row.Observation.Content!).ToArray();
        }

        var profiles=new DocumentationRendererProfileReader();ArchitectureRendererContext? context=null;DocumentationRendererProfile? rendererProfile=null;
        if(Has(DesignStage.Tools))
        {
            Artifact Lock(string path)=>captured?.Single(artifact=>artifact.RelativePath==path)??configuration.Single(artifact=>artifact.RelativePath==path);
            if(captured is null)
                foreach(var descriptor in new DocumentationProjectSourceCatalog().DescribeProjectSources(selection).Where(item=>item.Kind is DocumentationProjectInputKind.ToolchainLock or DocumentationProjectInputKind.RendererLock))
                    configuration.Add(new(descriptor.Source.RelativePath,await source.ReadAsync(descriptor.Source.RelativePath),descriptor.Source.MediaType));
            var profile=rendererProfile=profiles.Read(Lock("toolchain-lock.json"),Lock("renderer-lock.json"));
            var jar=profile.Downloads.Single(pin=>pin.LocalPath==".tools/plantuml-"+profile.PlantUmlVersion+".jar");
            var docker=await ReadToolIdentity(DockerIdentity!,"Docker");
            context=new(docker,work,ArtifactPaths.Resolve(cache,jar.LocalPath),receiptRoot,RendererUser!);
        }

        var toolStore=new FileArtifactStore(cache,bytes=>hashes.Sha256(bytes.Span));
        var workStore=new FileArtifactStore(work,bytes=>hashes.Sha256(bytes.Span));var finalStore=new FileArtifactStore(output,bytes=>hashes.Sha256(bytes.Span));
        var generationManifests=new DocumentationGenerationManifestFactory();
        ArtifactGenerationRuntime Runtime(string root,IArtifactStore destination)=>new(source,destination,new FileArtifactLeaseProvider(Path.Combine(root,".leases")),verifier,generationManifests,"design-assets/"+hashes.Sha256(Encoding.UTF8.GetBytes(root)));
        var clock=new SystemClock();var processes=new NativeProcessRunner(clock);var transport=new ArchitectureRendererTransport(processes);
        var preparation=new ArchitectureAssetPreparation();var generation=Generation??"gradle-compatibility";
        ArtifactGenerationPreparation? buildPayload=null;
        if(Has(DesignStage.Package))
        {
            if(string.IsNullOrWhiteSpace(PipelineRequest)||string.IsNullOrWhiteSpace(PipelineRequestSha256)||PipelineRequestLength is null)
                throw new InvalidOperationException("Package selection requires pinned --pipeline-request, --pipeline-request-sha256 and --pipeline-request-length for the native build host.");
            var requestPin=new StoredArtifact(InputRelative(input,PipelineRequest),PipelineRequestSha256,PipelineRequestLength.Value,"application/json");
            var hostConfiguration=await new DocumentationBuildPayloadHostReader(new PinnedArtifactReader(source),strict).ReadAsync(requestPin,Path.GetFullPath(DotNetHost!));
            configuration.Add(new(requestPin.RelativePath,await new PinnedArtifactReader(source).ReadAsync(requestPin),requestPin.MediaType));
            var buildRoot=Path.GetFullPath(RootDirectory);
            var nativeRun=new TestGateProducerRequest(buildRoot,Path.Combine(buildRoot,"AndroidMcp.Build.csproj"),
                Path.Combine(buildRoot,"Tests/AndroidMcp.Build.Tests.csproj"),hostConfiguration.DotNet,
                Path.Combine(buildRoot,"artifacts","tests","documentation-source-"+hashes.Sha256(Encoding.UTF8.GetBytes(output))),null,[]);
            var native=await ComposeNativeTestServices(nativeRun,hostConfiguration.Native);
            var buildSource=new FileArtifactStore(buildRoot,bytes=>hashes.Sha256(bytes.Span));
            var payloadRoot=ArtifactPaths.Resolve(work,"build-source");var payloadStore=new FileArtifactStore(payloadRoot,bytes=>hashes.Sha256(bytes.Span));
            var payloadRuntime=new ArtifactGenerationRuntime(buildSource,payloadStore,new FileArtifactLeaseProvider(Path.Combine(payloadRoot,".leases")),verifier,generationManifests,
                "build-source/"+hashes.Sha256(Encoding.UTF8.GetBytes(payloadRoot)));
            buildPayload=await new DocumentationBuildPayloadPreparationFactory(nativeRun,hostConfiguration.Native,hostConfiguration.PrerequisiteScopes,
                native.Facts,new PinnedArtifactReader(buildSource),new PinnedArtifactReader(payloadStore),new DocumentationBuildPayloadPlanner(strict,verifier),canonical,
                payloadRuntime,generation+"-build-source").PrepareAsync();
            var support=new DocumentationPackageSupport(verifier);var supportArtifacts=new List<Artifact>();
            var admittedProfile=rendererProfile??throw new InvalidOperationException("Package support requires the selected tools profile.");
            foreach(var spec in support.Describe(admittedProfile))
            {
                var retained=configuration.SingleOrDefault(artifact=>artifact.RelativePath==spec.RelativePath);
                if(retained is null)
                {
                    var bytes=spec.ExpectedIdentity is {} pin?await new PinnedArtifactReader(source).ReadAsync(pin):await source.ReadAsync(spec.RelativePath);
                    retained=new(spec.RelativePath,bytes,spec.MediaType);configuration.Add(retained);
                }
                supportArtifacts.Add(retained);
            }
            buildPayload=support.Append(admittedProfile,buildPayload,supportArtifacts);
        }
        else if(PipelineRequest is not null||PipelineRequestSha256 is not null||PipelineRequestLength is not null)
            throw new InvalidOperationException("Native build host configuration is only consumed by the package selection.");
        var legacyReceipts=new DocumentationLegacyReceipts(canonical,strict,verifier);
        var composer=new ArchitectureProjectionComposer(Runtime(work,workStore),workStore,verifier,preparation,transport,
            new NativeRendererImageAdmission(processes,new ProcessCaptureReader(receiptRoot),transport,hashes),generation+"-work",legacyReceipts);
        var bootstrap=new DocumentationToolBootstrap(new ToolArtifactAcquirer(HttpByteTransport.Create(clock),toolStore,new FileArtifactLeaseProvider(Path.Combine(cache,".leases")),canonical));
        var modules=new ModuleGraphValidator();var projector=new DocumentationProjector(new DocumentationModelReader());
        var preflight=new DocumentationModelPreflight(new DocumentationModelReader(),projector,new ModelContractValidator(hashes),modules,strict);
        var manifests=new DocumentationManifestBuilder(verifier);var packages=new DocumentationPackageBuilder(new PublicationChecker(canonical),verifier,manifests);
        var html=new SourceHtmlDecoder();var documents=new GeneratedDesignValidator(new ArchitectureViewIndexReader(),new ArtifactReferenceResolver(html.Decode),strict,verifier,html);
        var factory=new DesignCompositionRecipeFactory(profiles,modules,preparation,composer,bootstrap,packages,preflight,documents,manifests,sourcePlanner,verifier,projector,legacyReceipts,buildPayload);
        // No fictitious ToolIdentity is created for CodeExtract; its admitted
        // subset requires no renderer context and never dereferences this value.
        var request=new DesignArchitectureRequest(generation,context!,ToolAccess!.Value,OfflineGradleChecksumProof,
            Has(DesignStage.DocsVerify)?new([],policy?.Bytes??ReadOnlyMemory<byte>.Empty):null);
        var recipe=snapshot is not null?factory.Architecture(request,selection,snapshot,collector!):factory.Architecture(request,selection);

        // Configuration is captured by the same immutable runtime but is not
        // appended to the18 published documentation routes. Existing source
        // locks retain their normal recipe role and are pinned rather than stripped.
        var basePaths=recipe.Sources.Select(spec=>spec.RelativePath).ToHashSet(StringComparer.Ordinal);
        if(policy is not null&&basePaths.Contains(policy.RelativePath))throw new InvalidOperationException("Package policy cannot replace a declared documentation source.");
        var identities=configuration.ToDictionary(artifact=>artifact.RelativePath,verifier.Describe,StringComparer.Ordinal);
        var inner=recipe;
        recipe=new(inner.Sources.Select(spec=>identities.TryGetValue(spec.RelativePath,out var identity)?spec with{ExpectedIdentity=identity}:spec)
            .Concat(configuration.Where(artifact=>!basePaths.Contains(artifact.RelativePath)).Select(artifact=>new ArtifactSourceSpec(artifact.RelativePath,artifact.MediaType,identities[artifact.RelativePath]))).ToArray(),all=>
        {
            var prepared=inner.Prepare(all.Where(artifact=>basePaths.Contains(artifact.RelativePath)).ToArray());
            if(policy is not null&&prepared.OutputPaths.Contains(policy.RelativePath,StringComparer.OrdinalIgnoreCase))throw new InvalidOperationException("Package policy is configuration and cannot be a published output.");
            return prepared;
        });
        var result=await Runtime(output,finalStore).ExecuteAsync(generation,recipe);
        Console.WriteLine(JsonSerializer.Serialize(new{result.GenerationId,selection.Aliases,Stages=selection.Stages.Select(stage=>stage.ToString()),InputCount=result.Inputs.Count,OutputCount=result.Outputs.Count,result.Manifest,OutputRoot=output,
            Scope="Native Core alias union with bound selected validation and documentation-completion receipts; complete portable build-source payload remains open"}));
    }
}
