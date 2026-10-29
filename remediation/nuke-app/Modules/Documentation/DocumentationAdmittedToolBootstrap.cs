using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;

namespace AndroidMcp.BuildTool.Documentation;

// Receipt reuse only. The same bootstrap already acquired and applied original
// effects. Every later selected Tools use rechecks retained pins through the
// existing pinned reader; no installation, observation or second graph exists.
public sealed class DocumentationAdmittedToolBootstrap:IDocumentationToolBootstrap
{
    private readonly DocumentationRendererProfile admitted;
    private readonly ToolArtifactAccess access;
    private readonly string? proof;
    private readonly IReadOnlyList<ToolArtifactReceipt> receipts;
    private readonly IPinnedArtifactReader reader;
    public DocumentationAdmittedToolBootstrap(DocumentationRendererProfile admitted,ToolArtifactAccess access,string? proof,
        IReadOnlyList<ToolArtifactReceipt> receipts,IPinnedArtifactReader reader)
    {
        this.admitted=admitted with{Downloads=Array.AsReadOnly(admitted.Downloads.ToArray()),RendererFiles=Array.AsReadOnly(admitted.RendererFiles.ToArray()),
            ToolRedirects=admitted.ToolRedirects?.ToDictionary(pair=>pair.Key,pair=>(IReadOnlyList<Uri>)Array.AsReadOnly(pair.Value.ToArray()),StringComparer.Ordinal)};
        this.access=access;this.proof=proof;this.reader=reader;
        this.receipts=Array.AsReadOnly(receipts.Select(receipt=>receipt with{Files=Array.AsReadOnly(receipt.Files.ToArray())}).ToArray());
    }
    public async Task<IReadOnlyList<ToolArtifactReceipt>> AcquireAsync(DocumentationRendererProfile profile,ToolArtifactAccess access,string? offlineGradleChecksumProof,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if(access!=this.access||offlineGradleChecksumProof!=proof||!Same(profile,admitted)||receipts.Count!=admitted.Downloads.Count)throw Mismatch();
        for(var index=0;index<receipts.Count;index++){
            var receipt=receipts[index];var pin=admitted.Downloads[index];
            if(receipt.Access!=access||receipt.Pin.Id!=pin.LocalPath||receipt.Pin.Version!=pin.Revision||receipt.Pin.Url!=pin.Url||receipt.Pin.Sha256!=pin.Sha256||receipt.Pin.SizeBytes!=pin.SizeBytes||
                receipt.Artifact.RelativePath!=pin.LocalPath||receipt.Artifact.Sha256!=pin.Sha256||receipt.Artifact.Length!=pin.SizeBytes||
                access==ToolArtifactAccess.Offline&&receipt.Pin.PublishedChecksumUrl is not null&&receipt.ChecksumProof?.RelativePath!=proof)throw Mismatch();
        }
        foreach(var receipt in receipts){
            await reader.ReadAsync(receipt.Artifact,cancellationToken);
            foreach(var file in receipt.Files)await reader.ReadAsync(file,cancellationToken);
            if(receipt.ChecksumProof is {} retained)await reader.ReadAsync(retained,cancellationToken);
        }
        return receipts;
    }
    private static bool Same(DocumentationRendererProfile left,DocumentationRendererProfile right)=>
        left.Image==right.Image&&left.JavaVersion==right.JavaVersion&&left.StructurizrVersion==right.StructurizrVersion&&left.PlantUmlVersion==right.PlantUmlVersion&&left.GradleVersion==right.GradleVersion&&
        left.Downloads.SequenceEqual(right.Downloads)&&left.RendererFiles.SequenceEqual(right.RendererFiles)&&
        (left.ToolRedirects?.Count??0)==(right.ToolRedirects?.Count??0)&&
        (left.ToolRedirects is null||left.ToolRedirects.All(pair=>right.ToolRedirects is not null&&right.ToolRedirects.TryGetValue(pair.Key,out var urls)&&pair.Value.SequenceEqual(urls)));
    private static ArchitectureAssetException Mismatch()=>new("BOOTSTRAP_ADMISSION","Selected profile, access, checksum proof or retained receipts differ from the admitted bootstrap");
}
