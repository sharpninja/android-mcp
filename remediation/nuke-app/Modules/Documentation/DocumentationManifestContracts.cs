using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation.Validation;

namespace AndroidMcp.BuildTool.Documentation;

// The checksum artifact and its exact covered identities travel together.
// Public construction never grants trust: Verify must admit both against input.
public sealed record DocumentationManifest(Artifact Checksums,IReadOnlyList<StoredArtifact> CoveredInputs);
public interface IDocumentationManifestBuilder
{
    DocumentationManifest Build(IReadOnlyList<Artifact> artifacts);
    void Verify(IReadOnlyList<Artifact> artifacts,DocumentationManifest manifest);
}
public interface IDocumentationManifestPackageBuilder:IDocumentationPackageBuilder
{
    DocumentationPackage Build(DocumentationPackageInput input,DocumentationManifest manifest);
}
public sealed record DesignDocumentationStageOptions(IReadOnlyList<string> RequiredPackagePaths,
    ReadOnlyMemory<byte> PackagePolicy);
public sealed class DesignDocumentationValidationException(GeneratedDesignValidationResult result)
    :IOException("DESIGN_DOCUMENTATION_INVALID: "+result.Phase)
{
    public string Code=>"DESIGN_DOCUMENTATION_INVALID";
    public GeneratedDesignValidationResult Result {get;}=result;
    public override string ToString()=>base.ToString()+Environment.NewLine+"validationResult="+System.Text.Json.JsonSerializer.Serialize(Result);
}
