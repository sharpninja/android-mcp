using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;

namespace AndroidMcp.BuildTool.Documentation;

public sealed record DocumentationPackageInput(IReadOnlyList<Artifact> Artifacts,IReadOnlyList<string> RequiredPaths,ReadOnlyMemory<byte> Policy);
public sealed record DocumentationPackage(Artifact Archive,Artifact Checksums);
public interface IDocumentationPackageBuilder
{
    DocumentationPackage Build(DocumentationPackageInput input);
}
