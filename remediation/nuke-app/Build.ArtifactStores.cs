using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool;

public sealed partial class Build
{
    internal static FileArtifactStore CreateConfiguredArtifactStore(string root)
    {
        var calls=new NativeArtifactCalls(new NativeArtifactKernel());
        var opener=new ArtifactReadOpener(calls,new NativeArtifactIdentityReader(calls));
        return new FileArtifactStore(root,bytes=>ContentHash.Sha256(bytes.Span),null,opener);
    }
}
