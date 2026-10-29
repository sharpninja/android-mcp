using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Sources;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InterfaceModuleWorkflowFile(StoredArtifact Inventory, StoredArtifact DeclaredSources,
    string GenerationStoreRoot, string GenerationRoot, string? ProfileId = null);
public sealed record InterfaceModuleWorkflowRequest(StoredArtifact Inventory, StoredArtifact DeclaredSources,
    string GenerationRoot, SourceExtractionProfile Profile, SourceExtractProducerIdentity Producer);
public sealed record InterfaceModuleWorkflowResult(JsonObject Report, StoredArtifact Facts,
    StoredArtifact ReportFile, StoredArtifact Index);
public interface IInterfaceModuleWorkflow
{
    Task<InterfaceModuleWorkflowResult> RunAsync(InterfaceModuleWorkflowRequest request, CancellationToken cancellationToken = default);
}
