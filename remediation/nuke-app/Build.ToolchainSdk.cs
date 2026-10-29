using System.Text.Json;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool;

// Root-owned params/registry/dispatch compose this one explicit normal SDK check.
// No new reader, executable resolver, timestamp helper, shell or SDK evaluator.
public sealed partial class Build
{
    private async Task CheckSdkReference()
    {
        var root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(ProjectRoot!));
        var store=new FileArtifactStore(root,bytes=>ContentHash.Sha256(bytes.Span));
        var pin=new StoredArtifact(InputRelative(root,PipelineRequest!),PipelineRequestSha256!,
            PipelineRequestLength??throw new InvalidOperationException("Explicit pipeline request length is required."),"application/json");
        var query=new NativeTestGateResolvedReferenceQuery(new NativeProcessRunner(new SystemClock()),
            new ProcessCaptureReader(root),store);
        var host=new ToolchainSdkQueryHost(new PinnedArtifactReader(store),query);
        // Pinned request carries the selected ToolIdentity; host binds its path to --dotnet-host.
        // NativeProcessSessionFactory verifies native image bytes and SHA before process creation.
        var result=await host.ExecuteAsync(new(AndroidMcp.BuildTool.Core.ToolchainCheck.SdkReferenceCopy,
            root,pin,Path.GetFullPath(DotNetHost!)));
        Console.WriteLine(JsonSerializer.Serialize(result)); // Scoped public label/code/count/hashes only.
        if(result.Code!="OK")throw new InvalidOperationException(result.Check+": "+result.Code);
    }
}
