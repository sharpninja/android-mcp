using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using Nuke.Common;
namespace AndroidMcp.BuildTool;

public sealed partial class Build
{
    [Parameter("Snapshot caller behavior: Restore (default) or original Gitiles-only Fetch")]
    public SnapshotCallerMode? SnapshotMode{get;set;}
    [Parameter("Explicitly acquire missing snapshot groups; omission is a read-only dry run")]
    public bool Download{get;set;}

    private async Task SnapshotSources()
    {
        var mode=SnapshotMode??SnapshotCallerMode.Restore;
        var context=CreateSourceBatchContext(mode);
        ISnapshotMetadataProofReader? proofs=PipelineRequest is null?null:new DeferredSnapshotProofReader(async token=>
        {
            var canonical=new CanonicalJson();
            var projectInputs=new PinnedArtifactReader(new FileArtifactStore(context.InputRoot,bytes=>ContentHash.Sha256(bytes.Span)));
            var outer=new StoredArtifact(InputRelative(context.InputRoot,PipelineRequest),PipelineRequestSha256!,
                PipelineRequestLength??throw new InvalidOperationException("Explicit proof request length is required."),"application/json");
            var wire=JsonSerializer.Deserialize<SnapshotProofInputsFile>(canonical.Serialize(await projectInputs.ReadAsync(outer,token)))
                ??throw new InvalidOperationException("Pinned snapshot proof inputs are required.");
            if(wire.Pins is null)throw new InvalidOperationException("Explicit snapshot proof pins are required.");
            var root=wire.ProofRoot is null?context.OutputRoot:AdmitWorkflowRoot(context.InputRoot,wire.ProofRoot);
            return new SnapshotMetadataProofReader(new PinnedArtifactReader(new FileArtifactStore(root,bytes=>ContentHash.Sha256(bytes.Span))),wire.Pins,new SdkArchiveVersionValidator());
        });
        var operation=new SourcesSnapshotOperation(context.Planner,context.RawStore,_=>context.Batch,proofs);
        var result=await operation.RunAsync(new(await ReadSourcesRecipe(context.InputRoot),mode,Download,Generation??"snapshot-acquisition","sources/snapshot.json"));
        Console.WriteLine(JsonSerializer.Serialize(result));
        if(result.ExitCode!=0)throw new InvalidOperationException("Snapshot Fetch dry run; pass --download for permitted missing-source acquisition.");
    }
    private sealed record SnapshotProofInputsFile(IReadOnlyList<StoredArtifact>? Pins,string? ProofRoot=null);
    private sealed class DeferredSnapshotProofReader(Func<CancellationToken,Task<ISnapshotMetadataProofReader>> factory):ISnapshotMetadataProofReader
    {
        private Task<ISnapshotMetadataProofReader>? reader;
        public async Task<SnapshotMetadataProofAdmission> BindAsync(SourcePin source,RehydrationCanonicalMetadataReference reference,CancellationToken cancellationToken=default)
        {reader??=factory(cancellationToken);return await (await reader).BindAsync(source,reference,cancellationToken);}
    }
}
