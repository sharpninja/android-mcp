namespace AndroidMcp.BuildTool.Core;

public enum ToolchainCheck { None,SdkReferenceCopy }
public sealed record ToolchainSdkQueryCall(ToolchainCheck Check,string ProjectRoot,StoredArtifact PipelineRequest,string DotNetHost);
public sealed record ToolchainSdkQueryPublicResult(string Check,string Code,int Copies,IReadOnlyList<StoredArtifact> Receipts);
public interface IToolchainSdkQueryHost
{
    // Existing Validate/Toolchain with explicit SdkReferenceCopy scope. The Root-owned NUKE
    // adapter uses common pipeline-request/hash/length flags and the one shared pin reader.
    Task<ToolchainSdkQueryPublicResult> ExecuteAsync(ToolchainSdkQueryCall call,CancellationToken cancellationToken=default);
}
