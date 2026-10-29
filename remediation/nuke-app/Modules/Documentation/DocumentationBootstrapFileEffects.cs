using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;

namespace AndroidMcp.BuildTool.Documentation;

// Only original bootstrap selection/order rules live here. All native mode
// reads/writes, descriptor identity and optional byte pins remain in Core.
public sealed class DocumentationBootstrapFileEffects(IArtifactTreeReader tree,IArtifactPermissionEditor permissions,
    IDocumentationWrapperPropertiesValidator properties,Artifact capturedWrapperProperties):IDocumentationBootstrapFileEffects
{
    public async Task GradleExtractedAsync(DocumentationRendererProfile profile,ToolArtifactReceipt gradle,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bin=".tools/gradle-"+profile.GradleVersion+"/bin";
        var entries=await tree.EnumerateAsync(bin,false,cancellationToken);
        var requests=new List<ArtifactPermissionRequest>();
        foreach(var entry in entries){
            if(!ArtifactPaths.IsRelative(entry.RelativePath)||Path.GetDirectoryName(entry.RelativePath)?.Replace('\\','/')!=bin||
                entry.Kind is not (NativeArtifactKind.RegularFile or NativeArtifactKind.Directory)||entry.Native is null||entry.Native.Kind!=entry.Kind)throw Files();
            requests.Add(new(new(entry.RelativePath,entry.Native),ArtifactPermissionOperation.AddExecuteBits,
                entry.Kind==NativeArtifactKind.RegularFile?gradle.Files.SingleOrDefault(file=>file.RelativePath==entry.RelativePath):null));
        }
        foreach(var request in requests)await permissions.ApplyAsync(request,cancellationToken);
    }
    public async Task WrappersAcquiredAsync(DocumentationRendererProfile profile,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entries=await tree.EnumerateAsync(null,false,cancellationToken);
        var wrappers=entries.Where(entry=>entry.RelativePath=="gradlew").ToArray();
        if(wrappers.Length!=1||wrappers[0].Kind!=NativeArtifactKind.RegularFile||wrappers[0].Native is null||wrappers[0].Native!.Kind!=NativeArtifactKind.RegularFile)throw Files();
        var pin=profile.Downloads.Single(value=>value.LocalPath=="gradlew");
        await permissions.ApplyAsync(new(new("gradlew",wrappers[0].Native!),ArtifactPermissionOperation.SetMode0755,
            new(pin.LocalPath,pin.Sha256,pin.SizeBytes!.Value,"application/octet-stream")),cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        properties.Validate(profile,capturedWrapperProperties);
    }
    private static ArchitectureAssetException Files()=>new("BOOTSTRAP_FILES","Bootstrap file effects require the admitted original local file/directory scope");
}
