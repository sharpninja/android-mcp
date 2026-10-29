using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
namespace AndroidMcp.BuildTool;

public sealed partial class Build
{
    // Shared Fetch/Plan admission preserves the lock's complete relative recipe name.
    private async Task<RehydrationPlanRequest> ReadSourcesRecipe(string inputRoot)
    {
        var recipePath=InputRelative(inputRoot,SourceManifest!);
        var lockPath=InputRelative(inputRoot,RecipeLock!);
        var source=new FileArtifactStore(inputRoot,bytes=>ContentHash.Sha256(bytes.Span));
        var recipe=new Artifact(recipePath,await source.ReadAsync(recipePath),recipePath.EndsWith(".gz",StringComparison.Ordinal)?"application/gzip":"application/json");
        var recipeLock=new Artifact(lockPath,await source.ReadAsync(lockPath),"application/json");
        return new(recipe,recipeLock,Release,Limit,Profile:AcquisitionProfile??AcquisitionCompatibilityProfile.CanonicalRestore);
    }
}
