using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Execution;
using AndroidMcp.BuildTool.Publication;
namespace AndroidMcp.BuildTool;

public sealed partial class Build
{
    private async Task PlanSources()
    {
        var input=Path.TrimEndingDirectorySeparator(Path.GetFullPath(ProjectRoot!));
        var output=Path.TrimEndingDirectorySeparator(Path.GetFullPath(OutputRoot!));
        var comparison=OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
        if(string.Equals(input,output,comparison))throw new InvalidOperationException("Source planning requires an output root separate from retained inputs.");
        _=ArtifactPaths.Resolve(input,".source-plan-admission");
        _=ArtifactPaths.Resolve(output,".source-plan-admission");
        if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())
            throw new InvalidOperationException("Source planning requires a new or empty output root; existing files are retained.");
        var operation=new SourcesPlanOperation(new RehydrationPlanner(new CanonicalJson()),
            new FileArtifactStore(output,bytes=>ContentHash.Sha256(bytes.Span)));
        var result=await operation.RunAsync(await ReadSourcesRecipe(input));
        Console.WriteLine(JsonSerializer.Serialize(new{result.Manifest,result.Plan.Release,result.Plan.RecipeSha256,
            result.Plan.SourceRows,result.Plan.UniqueSourcePaths,SelectedPaths=result.Plan.SourceProjection!.SelectedPhysicalSourceIndices.Count,
            result.Plan.Network,result.Plan.Complete,result.Plan.HistoricalByteIdentityClaimed,
            result.Plan.RequiredSnapshots,result.Plan.ArchiveMembers,result.Plan.SourceProjection,result.Plan.Caveats,OutputRoot=output}));
    }
}
