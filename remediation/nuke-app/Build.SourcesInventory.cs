using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;

namespace AndroidMcp.BuildTool;

// Staged for checkpoint006. Root owns the four Acquisition* parameters and registry/dispatch.
public sealed partial class Build
{
    private async Task InventorySources()
    {
        var input=Path.TrimEndingDirectorySeparator(Path.GetFullPath(ProjectRoot!));
        var acquired=Path.TrimEndingDirectorySeparator(Path.GetFullPath(AcquisitionRoot!));
        var output=Path.TrimEndingDirectorySeparator(Path.GetFullPath(OutputRoot!));
        var comparison=OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
        if(string.Equals(input,output,comparison)||string.Equals(acquired,output,comparison))
            throw new InvalidOperationException("Source inventory requires an output root separate from retained recipe and acquisition inputs.");
        _=ArtifactPaths.Resolve(input,".source-inventory-admission");
        _=ArtifactPaths.Resolve(acquired,".source-inventory-admission");
        _=ArtifactPaths.Resolve(output,".source-inventory-admission");
        if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())
            throw new InvalidOperationException("Source inventory requires a new or empty output root; existing files are retained.");
        var pin=new StoredArtifact(InputRelative(acquired,AcquisitionManifest!),AcquisitionManifestSha256!,
            AcquisitionManifestLength??throw new InvalidOperationException("Explicit acquisition manifest length required."),"application/json");
        var canonical=new CanonicalJson();
        var source=new FileArtifactStore(acquired,bytes=>ContentHash.Sha256(bytes.Span));
        // Read intent is enforced inside the shared engine before any acquisition loop.
        // Leases are execution receipts under NUKE's root, not changes to either data root.
        ISourceBatchReader reader=new SourceBatch(HttpByteTransport.Create(),source,source,
            new FileArtifactLeaseProvider(Path.Combine(RootDirectory.ToString(),".nuke","source-inventory-leases")),canonical);
        var operation=new SourcesInventoryOperation(new RehydrationPlanner(canonical),reader,source,
            new FileArtifactStore(output,bytes=>ContentHash.Sha256(bytes.Span)),canonical);
        var result=await operation.RunAsync(new(await ReadSourcesRecipe(input),pin));
        var receipt=result.Receipt;
        Console.WriteLine(JsonSerializer.Serialize(new{result.Manifest,receipt.Acquisition.Receipt.Projection.RecipeSha256,
            SourceRows=receipt.Acquisition.Receipt.Projection.Rows.Count,receipt.VerifiedPhysicalSources,
            receipt.UnselectedPhysicalSources,receipt.RawComplete,receipt.ExtractionComplete,receipt.HistoricalByteIdentityClaimed,
            AcquisitionManifest=receipt.Acquisition.Manifest,OutputRoot=output}));
    }
}
