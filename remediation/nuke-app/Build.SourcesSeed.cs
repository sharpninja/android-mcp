using System.Text.Json;
using AndroidMcp.BuildTool.Execution;

namespace AndroidMcp.BuildTool;

public sealed partial class Build
{
    private async Task SeedSources()
    {
        var context=CreateSourceBatchContext();
        var operation=new SourcesSeedOperation(context.Planner,context.Batch);
        var result=await operation.RunAsync(new(await ReadSourcesRecipe(context.InputRoot),Generation??"source-seed"));
        // This is the seed manifest only. Fetch must subsequently seal acquisition
        // before the existing Inventory, Extract and Catalog callers can consume it.
        Console.WriteLine(JsonSerializer.Serialize(new{result.Manifest,result.Reused,
            GenerationId=result.Receipt.Acquisition.GenerationId,result.CurrentCounts,result.Receipt.OriginalCounts,
            result.Receipt.RawComplete,result.Receipt.HistoricalByteIdentityClaimed,OutputRoot=context.OutputRoot}));
    }
}
