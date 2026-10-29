using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;

namespace AndroidMcp.BuildTool;

public sealed partial class Build
{
    private (string Root,IReleasePartsCodec Codec) CreateReleasePartsContext()
    {
        var root=AdmitWorkflowRoot(Path.GetFullPath(ProjectRoot!),".");
        var calls=new NativeArtifactCalls(new NativeArtifactKernel());
        var opener=new ArtifactReadOpener(calls,new NativeArtifactIdentityReader(calls));
        var store=new FileArtifactStore(root,bytes=>ContentHash.Sha256(bytes.Span),null,opener);
        return (root,new ReleasePartsCodec(store,store,new PinnedArtifactReader(store),new StrictJsonInspector(),
            new CanonicalJson(),new FileArtifactLeaseProvider(Path.Combine(root,".release-codec-leases"))));
    }

    private async Task SplitReleaseArtifact()
    {
        var context=CreateReleasePartsContext();
        var request=new ReleaseSplitRequest(InputRelative(context.Root,SourceArtifact!),InputRelative(context.Root,Generation!),ArtifactSha256!);
        if(PartBytes is {} size)request=request with{PartBytes=size};
        var result=await context.Codec.SplitAsync(request);
        Console.WriteLine(JsonSerializer.Serialize(new{Operation="Split",result.Manifest,result.ManifestArtifact,result.Parts,result.RetainedWhole,ProjectRoot=context.Root}));
    }

    private async Task ReassembleReleaseArtifact()
    {
        var context=CreateReleasePartsContext();
        var result=await context.Codec.ReassembleAsync(new(InputRelative(context.Root,PartsManifest!),InputRelative(context.Root,Generation!),
            InputRelative(context.Root,DestinationArtifact!),ManifestSha256!));
        Console.WriteLine(JsonSerializer.Serialize(new{Operation="Reassemble",result.Artifact,result.SuccessMarker,result.RetainedWhole,ProjectRoot=context.Root}));
    }
}
