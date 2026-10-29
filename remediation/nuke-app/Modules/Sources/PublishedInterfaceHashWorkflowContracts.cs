using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Sources;

public enum SourceExtractTask { Declarations, InterfaceHashes }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PublishedInterfaceHashWorkflowFile(StoredArtifact Inventory, string GenerationStoreRoot, string GenerationRoot, string? ProfileId = null);
public sealed record PublishedInterfaceHashWorkflowRequest(StoredArtifact Inventory, string GenerationRoot,
    SourceExtractionProfile Profile, SourceExtractProducerIdentity Producer);
public sealed record PublishedInterfaceHashWorkflowResult(JsonObject Report, JsonObject Summary,
    StoredArtifact Facts, StoredArtifact ReportFile, StoredArtifact Index);
public interface IPublishedInterfaceHashWorkflow
{
    Task<PublishedInterfaceHashWorkflowResult> RunAsync(PublishedInterfaceHashWorkflowRequest request, CancellationToken cancellationToken = default);
}
