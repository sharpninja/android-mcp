using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;

namespace AndroidMcp.BuildTool.Execution;

public sealed record SourcesFetchRequest(RehydrationPlanRequest Recipe,
    string GenerationId, string ReceiptPath,
    SourceBatchCachePolicy CachePolicy = SourceBatchCachePolicy.PreserveAliases);
public interface ISourcesFetchOperation
{
    Task<SourceBatchResult> RunAsync(SourcesFetchRequest request, CancellationToken cancellationToken = default);
}
public sealed class SourcesFetchException(string code, IReadOnlyList<PublicationIssue> issues)
    : InvalidOperationException(code + ": " + string.Join("; ", issues.Select(i => i.Code + ": " + i.Path)))
{
    public string Code { get; } = code;
    public IReadOnlyList<PublicationIssue> Issues { get; } = Array.AsReadOnly(issues.ToArray());
}
