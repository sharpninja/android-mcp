using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;

// Read intent is separate from the historical SourceBatchRequest serialization and identity.
// The engine must check its own receipt read against RequiredManifest before parsing it.
public sealed record SourceBatchResumeRequest(SourceBatchRequest Acquisition,StoredArtifact RequiredManifest);
public interface ISourceBatchReader
{
    // A missing/changed receipt fails before the acquisition loop. Never HTTP, import or write.
    Task<SourceBatchResult> ResumeOnlyAsync(SourceBatchResumeRequest request,CancellationToken cancellationToken=default);
}
