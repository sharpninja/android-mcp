using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

// Routing is a read-only host concern. A role never authorizes an arbitrary path outside its bound store.
public enum DocumentationInputRole { ProjectRequired, ContractSnapshotRequired, CanonicalContractOptional }
public enum DocumentationContractId { OpenApi, ApiContract, SourceMetadataContract }
public sealed record DocumentationModelSourceSpec(string ArtifactId,DocumentationInputRole Role,
    DocumentationContractId? ContractId,string RelativePath,string MediaType,string? ExpectedSha256);
public sealed record DocumentationModelSourcePlan(IReadOnlyList<DocumentationModelSourceSpec> Sources);
// Null is an observed absence, allowed only for CanonicalContractOptional. A missing snapshot is an error.
public sealed record DocumentationModelSourceObservation(DocumentationModelSourceSpec Source,Artifact? Content);
public interface IDocumentationModelSourcePlanner
{
    DocumentationModelSourcePlan Plan(Artifact sourcePins);
}
public sealed class DocumentationModelSourceException(string message):IOException(message)
{
    public string Code=>"MODEL_SOURCE_PINS";
}
