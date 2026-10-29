using System.Text.Json;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

/// <summary>Binds native source observations to the same immutable recipe runtime.
/// Source reads, pin verification, writes and manifest commit remain shared authorities.</summary>
public sealed class DocumentationBuildPayloadPreparationFactory(TestGateProducerRequest run,
    TestGateNativeFactsConfiguration configuration,IReadOnlyList<DocumentationBuildPrerequisiteScope> scopes,
    ITestGateNativeFacts facts,IPinnedArtifactReader evidence,IPinnedArtifactReader produced,
    IDocumentationBuildPayloadPlanner planner,ICanonicalJson canonical,IArtifactGenerationRuntime runtime,string generationId)
    :IDocumentationBuildPayloadPreparationFactory
{
    private const string PrerequisitePath="build-tool/prerequisites.json",PublicManifestPath="build-tool/source-generation.json";
    public async Task<ArtifactGenerationPreparation> PrepareAsync(CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot=await facts.CaptureSourceAsync(run,TestGateSnapshotPoint.BeforeBuild,cancellationToken);
        var source=await evidence.ReadAsync(snapshot.Manifest,cancellationToken);
        var native=await evidence.ReadAsync(snapshot.NativeEvaluationReceipt,cancellationToken);
        var plan=planner.Plan(new(Path.GetDirectoryName(run.HostProject)!,run.HostProject,snapshot,
            new(snapshot.Manifest.RelativePath,source,snapshot.Manifest.MediaType),
            new(snapshot.NativeEvaluationReceipt.RelativePath,native,snapshot.NativeEvaluationReceipt.MediaType),configuration,scopes));
        var metadata=new Artifact(PrerequisitePath,canonical.Serialize(JsonSerializer.SerializeToElement(new
        {
            schema_version="android-mcp-native-build-prerequisites-1",configuration=configuration.Configuration,
            target_framework=configuration.TargetFramework,sdk_files=configuration.Sdk.AssemblyFiles,
            participating_prerequisites=plan.Prerequisites,restore_generated_hashes_are_observations=true
        })),"application/json");
        var childPaths=plan.Sources.Select(route=>route.PublicationPath).Append(metadata.RelativePath).ToArray();
        async Task Verify(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var observed=await facts.CaptureSourceAsync(run,TestGateSnapshotPoint.AfterExecution,token);
            if(observed.ContentFingerprint!=snapshot.ContentFingerprint)throw new DocumentationBuildPayloadException("BUILD_SOURCE_CHANGED");
        }
        var recipe=new ArtifactGenerationRecipe(plan.Sources.Select(route=>route.Source).ToArray(),inputs=>
            new(childPaths,token=>
            {
                token.ThrowIfCancellationRequested();
                var byPath=inputs.ToDictionary(artifact=>artifact.RelativePath,StringComparer.Ordinal);
                return Task.FromResult<IReadOnlyList<Artifact>>(plan.Sources.Select(route=>byPath[route.Source.RelativePath] with{RelativePath=route.PublicationPath}).Append(metadata).ToArray());
            },Verify));
        return new(childPaths.Append(PublicManifestPath).ToArray(),async token=>
        {
            token.ThrowIfCancellationRequested();
            var result=await runtime.ExecuteAsync(generationId,recipe,token);
            var outputs=new List<Artifact>();
            foreach(var pin in result.Outputs)outputs.Add(new(pin.RelativePath,await produced.ReadAsync(pin,token),pin.MediaType));
            outputs.Add(new(PublicManifestPath,await produced.ReadAsync(result.Manifest,token),result.Manifest.MediaType));
            return outputs;
        },Verify);
    }
}
