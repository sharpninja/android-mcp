using AndroidMcp.BuildTool.Modules.Sources.Acquisition;

namespace AndroidMcp.BuildTool.Execution;

// Physical source outcomes for this invocation, not the immutable receipt's original action history.
public sealed record SourcesFetchInvocationCounts(int Downloaded,int Reused,int SelectedPaths,int TotalPaths,bool Complete);
public interface ISourcesFetchInvocationProjection
{
    // Input is the verified result returned by the existing SourceBatch engine.
    // This view performs no acquisition, JSON parsing or receipt mutation.
    SourcesFetchInvocationCounts Project(SourceBatchResult acquisition);
}
