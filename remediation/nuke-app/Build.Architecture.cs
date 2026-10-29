using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;

namespace AndroidMcp.BuildTool;

public sealed partial class Build
{
    private async Task GenerateArchitecture()
    {
        var input=Path.TrimEndingDirectorySeparator(Path.GetFullPath(ProjectRoot!));
        var output=Path.TrimEndingDirectorySeparator(Path.GetFullPath(OutputRoot!));
        var work=Path.TrimEndingDirectorySeparator(Path.GetFullPath(WorkRoot!));
        var cache=Path.TrimEndingDirectorySeparator(Path.GetFullPath(CacheRoot!));
        var receiptsRoot=Path.TrimEndingDirectorySeparator(Path.GetFullPath(ReceiptRoot!));
        var comparer=OperatingSystem.IsWindows()?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal;
        var effectRoots=new[]{output,work,receiptsRoot};
        if(effectRoots.Distinct(comparer).Count()!=effectRoots.Length||effectRoots.Any(root=>comparer.Equals(root,input)||comparer.Equals(root,cache)))
            throw new InvalidOperationException("Architecture work, final and receipt roots must be separate from each other and retained source/cache roots.");
        foreach(var root in effectRoots.Concat(new[]{input,cache}))_=ArtifactPaths.Resolve(root,".architecture-admission");
        var docker=await ReadToolIdentity(DockerIdentity!,"Docker");
        var hashes=new NativeContentHash();var verifier=new ArtifactReceiptVerifier(hashes);
        var canonical=new CanonicalJson(bytes=>hashes.Sha256(bytes.Span));
        var source=new FileArtifactStore(input,bytes=>hashes.Sha256(bytes.Span));
        var toolStore=new FileArtifactStore(cache,bytes=>hashes.Sha256(bytes.Span));
        var workStore=new FileArtifactStore(work,bytes=>hashes.Sha256(bytes.Span));
        var finalStore=new FileArtifactStore(output,bytes=>hashes.Sha256(bytes.Span));
        var manifests=new DocumentationGenerationManifestFactory();
        ArtifactGenerationRuntime Runtime(string root,IArtifactStore destination)=>new(source,destination,
            new FileArtifactLeaseProvider(Path.Combine(root,".leases")),verifier,manifests,
            "design-assets/"+hashes.Sha256(Encoding.UTF8.GetBytes(root)));
        var clock=new SystemClock();var processes=new NativeProcessRunner(clock);
        var captures=new ProcessCaptureReader(receiptsRoot);
        var nativeRenderer=new ArchitectureRendererTransport(processes);
        var imageAdmission=new NativeRendererImageAdmission(processes,captures,nativeRenderer,hashes);
        var bootstrap=new DocumentationToolBootstrap(new ToolArtifactAcquirer(HttpByteTransport.Create(clock),toolStore,
            new FileArtifactLeaseProvider(Path.Combine(cache,".leases")),canonical));
        var preparation=new ArchitectureAssetPreparation();var generation=Generation??"architecture";
        var composer=new ArchitectureProjectionComposer(Runtime(work,workStore),workStore,verifier,preparation,nativeRenderer,imageAdmission,generation+"-work");
        var profiles=new DocumentationRendererProfileReader();
        var factory=new DesignCompositionRecipeFactory(profiles,new ModuleGraphValidator(),preparation,
            composer,bootstrap,new DocumentationPackageBuilder(new PublicationChecker(canonical),verifier));
        // Resolve the exact jar path from the same retained lock and bound cache used by native bootstrap.
        var locks=new Artifact[]{new("toolchain-lock.json",await source.ReadAsync("toolchain-lock.json"),"application/json"),
            new("renderer-lock.json",await source.ReadAsync("renderer-lock.json"),"application/json")};
        var profile=profiles.Read(locks[0],locks[1]);
        var jar=profile.Downloads.Single(pin=>pin.LocalPath==".tools/plantuml-"+profile.PlantUmlVersion+".jar");
        var context=new ArchitectureRendererContext(docker,work,ArtifactPaths.Resolve(cache,jar.LocalPath),receiptsRoot,RendererUser!);
        var request=new DesignArchitectureRequest(generation,context,ToolAccess!.Value,OfflineGradleChecksumProof);
        var recipe=factory.Architecture(request);var identities=locks.ToDictionary(a=>a.RelativePath,verifier.Describe,StringComparer.Ordinal);
        // Existing source identity checks reject a lock change between host binding and runtime admission.
        recipe=recipe with{Sources=recipe.Sources.Select(spec=>identities.TryGetValue(spec.RelativePath,out var identity)?spec with{ExpectedIdentity=identity}:spec).ToArray()};
        var result=await Runtime(output,finalStore).ExecuteAsync(generation,recipe);
        Console.WriteLine(JsonSerializer.Serialize(new{result.GenerationId,InputCount=result.Inputs.Count,OutputCount=result.Outputs.Count,
            result.Manifest,OutputRoot=output,WorkRoot=work,ReceiptRoot=receiptsRoot,
            Scope="Native architecture generation; full legacy model-contract preflight remains pending"}));
    }
}
