using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;

namespace AndroidMcp.BuildTool.Documentation;

// Bootstrap owns a child immutable generation. Existing runtime/store/pinned
// reader own publication, source rechecks and subsequent byte admission.
public sealed class DocumentationBootstrapCompletion(IArtifactGenerationRuntime runtime,IPinnedArtifactReader published,
    IDocumentationBootstrapReportProjector projector,IArtifactReceiptVerifier receipts):IDocumentationBootstrapCompletion
{
    public async Task<DocumentationBootstrapReport> CompleteAsync(string generation,DocumentationRendererProfile profile,
        IReadOnlyList<ToolArtifactReceipt> tools,Artifact capturedProperties,CancellationToken cancellationToken=default)
    {
        DocumentationBootstrapReport? report=null;
        var recipe=new ArtifactGenerationRecipe([new("toolchain-lock.json","application/json"),
            new(capturedProperties.RelativePath,capturedProperties.MediaType,receipts.Describe(capturedProperties))],inputs=>{
            report=projector.Project(profile,inputs.Single(artifact=>artifact.RelativePath=="toolchain-lock.json"),tools);
            IReadOnlyList<Artifact> output=[report.Receipt];
            return new([report.Receipt.RelativePath],token=>{token.ThrowIfCancellationRequested();return Task.FromResult(output);});
        });
        var committed=await runtime.ExecuteAsync(generation,recipe,cancellationToken);
        var identity=committed.Outputs.Single(artifact=>artifact.RelativePath==report!.Receipt.RelativePath);
        return report! with{Receipt=new(identity.RelativePath,await published.ReadAsync(identity,cancellationToken),identity.MediaType)};
    }
}
