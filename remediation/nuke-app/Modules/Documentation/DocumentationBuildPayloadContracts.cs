using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

// Planned pure projection boundary for the existing package-source duty.
// The caller obtains Snapshot by invoking the existing native facts collector;
// arbitrary JSON receipts are not a source-completeness authority.
public sealed record DocumentationBuildPrerequisiteScope(string Id,string Root);
public sealed record DocumentationBuildPayloadInput(string SourceRoot,string HostProject,
    TestGateSourceSnapshot Snapshot,Artifact SourceManifest,Artifact NativeEvaluation,
    TestGateNativeFactsConfiguration NativeConfiguration,
    IReadOnlyList<DocumentationBuildPrerequisiteScope> PrerequisiteScopes);
public sealed record DocumentationBuildPayloadRoute(ArtifactSourceSpec Source,string PublicationPath);
public sealed record DocumentationBuildPrerequisite(string ScopeId,string RelativePath,string Kind,
    string Sha256,long Length);
public sealed record DocumentationBuildPayloadPlan(IReadOnlyList<DocumentationBuildPayloadRoute> Sources,
    IReadOnlyList<DocumentationBuildPrerequisite> Prerequisites);
public interface IDocumentationBuildPayloadPlanner
{
    DocumentationBuildPayloadPlan Plan(DocumentationBuildPayloadInput input);
}
public sealed class DocumentationBuildPayloadException(string code):InvalidOperationException(code)
{ public string Code {get;}=code; }
