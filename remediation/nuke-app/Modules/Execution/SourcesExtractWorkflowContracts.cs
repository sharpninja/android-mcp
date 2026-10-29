using System.Text.Json.Serialization;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Sources;

namespace AndroidMcp.BuildTool.Execution;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SourcesExtractWorkflowFile(SourceWorkflowInputs Sources,SourceWorkflowStoreBindings Stores,
    StoredArtifact InventoryManifest,string GenerationRoot,string? ProfileId=null,StoredArtifact? TrustedPriorIndex=null,
    SourceJavaAdmission? Java=null,int? CacheSourceLimit=null);
// Host admission resolves explicit role roots and pinned recipe files before composition.
// Producer identity can only come from the observed Source-owned builder, never this request.
public sealed record SourcesExtractWorkflowRequest(RehydrationPlanRequest Recipe,StoredArtifact InventoryManifest,
    string GenerationRoot,string? ProfileId=null,StoredArtifact? TrustedPriorIndex=null,
    SourceJavaAdmission? Java=null,int? CacheSourceLimit=null);
public interface ISourcesExtractWorkflowOperation
{
    Task<SourceExtractResult> RunAsync(SourcesExtractWorkflowRequest request,CancellationToken cancellationToken=default);
}
public sealed class SourcesExtractWorkflowException(string code,IReadOnlyList<PublicationIssue> issues)
    :InvalidOperationException(code+": "+string.Join("; ",issues.Select(issue=>issue.Code+": "+issue.Path)))
{
    public string Code{get;}=code;
    public IReadOnlyList<PublicationIssue> Issues{get;}=Array.AsReadOnly(issues.ToArray());
}
