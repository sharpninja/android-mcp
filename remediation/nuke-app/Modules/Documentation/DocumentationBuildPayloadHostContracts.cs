using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

// Machine configuration only. It cannot supply the evaluated source file list.
public sealed record DocumentationBuildPayloadHostConfiguration(int SchemaVersion,ToolIdentity DotNet,
    TestGateNativeFactsConfiguration Native,IReadOnlyList<DocumentationBuildPrerequisiteScope> PrerequisiteScopes);
public interface IDocumentationBuildPayloadHostReader
{
    Task<DocumentationBuildPayloadHostConfiguration> ReadAsync(StoredArtifact request,string expectedDotNetHost,
        CancellationToken cancellationToken=default);
}
