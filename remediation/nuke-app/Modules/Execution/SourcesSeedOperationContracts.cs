using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;

namespace AndroidMcp.BuildTool.Execution;

// This adapter selects the existing local-only capability; it introduces no recipe or pin model.
public sealed record SourcesSeedRequest(RehydrationPlanRequest Recipe,string GenerationId="source-seed",
    string ReceiptPath="sources/seed.json");
public interface ISourcesSeedOperation
{
    Task<SourceSeedResult> RunAsync(SourcesSeedRequest request,CancellationToken cancellationToken=default);
}
public sealed class SourcesSeedException(string code,IReadOnlyList<PublicationIssue> issues)
    :InvalidOperationException(code+": "+string.Join("; ",issues.Select(issue=>issue.Code+": "+issue.Path)))
{
    public string Code{get;}=code;
    public IReadOnlyList<PublicationIssue> Issues{get;}=Array.AsReadOnly(issues.ToArray());
}
