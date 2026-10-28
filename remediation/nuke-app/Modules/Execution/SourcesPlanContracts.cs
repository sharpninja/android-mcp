using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
namespace AndroidMcp.BuildTool.Execution;

public sealed record SourcesPlanResult(RehydrationPlan Plan,StoredArtifact Manifest);
public interface ISourcesPlanOperation
{
    Task<SourcesPlanResult> RunAsync(RehydrationPlanRequest request,CancellationToken cancellationToken=default);
}
public sealed class SourcesPlanException(string code,IReadOnlyList<PublicationIssue> issues)
    :InvalidOperationException(code+": "+string.Join("; ",issues.Select(x=>x.Code+": "+x.Path)))
{
    public string Code{get;}=code;
    public IReadOnlyList<PublicationIssue> Issues{get;}=Array.AsReadOnly(issues.ToArray());
}
