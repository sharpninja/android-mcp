using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;

namespace AndroidMcp.BuildTool;

public sealed partial class Build
{
    private async Task GenerateGradleProjection()
    {
        var output=AdmitWorkflowRoot(RootDirectory,OutputRoot!);
        var hashes=new NativeContentHash();var verifier=new ArtifactReceiptVerifier(hashes);
        var canonical=new CanonicalJson(bytes=>hashes.Sha256(bytes.Span));
        var projection=new NativeDesignGradleProjection(canonical,verifier);
        var definition=new OperationRegistry().Find(new(Category.Design,Operation.GradleCompatibility))!;
        var recipe=projection.CreateRecipe(new DesignStagePlanner().Describe(),definition);
        var runtime=new ArtifactGenerationRuntime(new FileArtifactStore(RootDirectory,bytes=>hashes.Sha256(bytes.Span)),
            new FileArtifactStore(output,bytes=>hashes.Sha256(bytes.Span)),new FileArtifactLeaseProvider(Path.Combine(output,".leases")),
            verifier,new DocumentationGenerationManifestFactory(),"design-assets/"+hashes.Sha256(Encoding.UTF8.GetBytes(output)));
        var result=await runtime.ExecuteAsync(Generation??"gradle-projection",recipe);
        Console.WriteLine(JsonSerializer.Serialize(new{result.GenerationId,InputCount=result.Inputs.Count,OutputCount=result.Outputs.Count,result.Manifest,OutputRoot=output,
            Scope="Native two-file Gradle projection; review generated files before checked-in integration"}));
    }
}
