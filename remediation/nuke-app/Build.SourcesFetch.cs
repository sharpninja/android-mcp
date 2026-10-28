using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Execution;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;

namespace AndroidMcp.BuildTool;

public sealed partial class Build
{
    private async Task FetchSources()
    {
        var context=CreateSourceBatchContext();
        var operation=new SourcesFetchOperation(context.Planner,context.Batch);
        var result=await operation.RunAsync(new(await ReadSourcesRecipe(context.InputRoot),
            Generation??"source-acquisition","sources/acquisition.json",CachePolicy??SourceBatchCachePolicy.PreserveAliases));
        Console.WriteLine(JsonSerializer.Serialize(new{result.Manifest,result.Reused,result.Receipt.GenerationId,
            CurrentCounts=new SourcesFetchInvocationProjection().Project(result),
            result.Receipt.TotalRows,result.Receipt.SelectedRows,result.Receipt.ProcessedRows,
            result.Receipt.TotalPhysicalSources,result.Receipt.SelectedPhysicalSources,result.Receipt.Complete,
            result.Receipt.HistoricalByteIdentityClaimed,OutputRoot=context.OutputRoot}));
    }

    private (string InputRoot,string OutputRoot,RehydrationPlanner Planner,SourceBatch Batch) CreateSourceBatchContext()
    {
        var input=Path.TrimEndingDirectorySeparator(Path.GetFullPath(ProjectRoot!));
        var output=Path.TrimEndingDirectorySeparator(Path.GetFullPath(OutputRoot!));
        var bundled=Path.TrimEndingDirectorySeparator(Path.GetFullPath(BundleRoot!));
        var comparison=OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
        if(string.Equals(input,output,comparison)||string.Equals(bundled,output,comparison))
            throw new InvalidOperationException("Source acquisition requires an output root separate from the input and bundle roots.");
        // Shared path policy rejects reparse ancestors before any acquisition effects.
        _=ArtifactPaths.Resolve(input,".source-fetch-admission");
        _=ArtifactPaths.Resolve(output,".source-fetch-admission");
        _=ArtifactPaths.Resolve(bundled,".source-fetch-admission");
        var canonical=new CanonicalJson();
        var destination=new FileArtifactStore(output,bytes=>ContentHash.Sha256(bytes.Span));
        var bundle=new FileArtifactStore(bundled,bytes=>ContentHash.Sha256(bytes.Span));
        return (input,output,new RehydrationPlanner(canonical),new SourceBatch(HttpByteTransport.Create(),destination,bundle,
            new FileArtifactLeaseProvider(Path.Combine(output,".leases")),canonical,FetchDelaySeconds));
    }

    private static string InputRelative(string root,string requested)
    {
        var relative=Path.GetRelativePath(root,Path.GetFullPath(requested,root)).Replace(Path.DirectorySeparatorChar,'/');
        _=ArtifactPaths.Resolve(root,relative);
        return relative;
    }
}
