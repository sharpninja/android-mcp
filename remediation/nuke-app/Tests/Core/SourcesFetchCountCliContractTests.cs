using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Execution;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed record SourcesFetchCountCliObservation(SourcesFetchCliObservation Acquisition,JsonElement FirstSummary,JsonElement ReplaySummary);
public interface ISourcesFetchCountCliProbe{Task<SourcesFetchCountCliObservation> RunAsync();}
public static class SourcesFetchCountCliCases
{
    public static async Task AssertCase(ISourcesFetchCountCliProbe provider)
    {
        var actual=await provider.RunAsync();
        // Keep every existing byte/pin/argv/retention assertion on the same two-child fixture.
        await SourcesFetchCliCases.AssertCase(new Retained(actual.Acquisition));
        var summaries=new[]{actual.FirstSummary,actual.ReplaySummary};
        for(var index=0;index<2;index++){
            var counts=summaries[index].GetProperty("CurrentCounts");
            Assert.Equal(index==0?2:0,counts.GetProperty("Downloaded").GetInt32());
            Assert.Equal(index==0?0:2,counts.GetProperty("Reused").GetInt32());
            Assert.Equal(2,counts.GetProperty("SelectedPaths").GetInt32());Assert.Equal(2,counts.GetProperty("TotalPaths").GetInt32());Assert.True(counts.GetProperty("Complete").GetBoolean());
        }
    }
    private sealed class Retained(SourcesFetchCliObservation observed):ISourcesFetchCliProbe
    {public Task<SourcesFetchCliObservation> RunAsync()=>Task.FromResult(observed);}
}
public sealed class ScriptedSourcesFetchCountCliProbe(string mutation=""):ISourcesFetchCountCliProbe
{
    public async Task<SourcesFetchCountCliObservation> RunAsync()
    {
        var original=await new ScriptedSourcesFetchCliProbe().RunAsync();
        JsonObject Summary(bool replay)=>JsonSerializer.SerializeToNode(new{CurrentCounts=new SourcesFetchInvocationCounts(replay?0:2,replay?2:0,2,2,true)})!.AsObject();
        var first=Summary(false);var second=Summary(true);
        if(mutation=="first-counts")first.Remove("CurrentCounts");if(mutation=="replay-counts")second.Remove("CurrentCounts");
        if(mutation.StartsWith("first:",StringComparison.Ordinal))first["CurrentCounts"]!.AsObject().Remove(mutation[6..]);
        if(mutation.StartsWith("replay:",StringComparison.Ordinal))second["CurrentCounts"]!.AsObject().Remove(mutation[7..]);
        if(mutation=="original-downloads-on-replay"){second["CurrentCounts"]!["Downloaded"]=2;second["CurrentCounts"]!["Reused"]=0;}
        if(mutation=="acquisition")original=original with{InputsRetained=false};
        return new(original,JsonSerializer.SerializeToElement(first),JsonSerializer.SerializeToElement(second));
    }
}
[Trait("Gate","Mock")][Trait("Requirement","TEST-BUILD-004")]
public sealed class SourcesFetchCountCliMockTests
{
    [Fact]public Task CorrectSameFixtureInvocationSummaries()=>SourcesFetchCountCliCases.AssertCase(new ScriptedSourcesFetchCountCliProbe());
    [Fact]public async Task EveryCurrentCounterAndOriginalFixtureAssertionDiscriminates()
    {
        var mutations=new[]{"first-counts","replay-counts","original-downloads-on-replay","acquisition"}
            .Concat(new[]{"Downloaded","Reused","SelectedPaths","TotalPaths","Complete"}.SelectMany(name=>new[]{"first:"+name,"replay:"+name}));
        foreach(var mutation in mutations)Assert.NotNull(await Record.ExceptionAsync(()=>SourcesFetchCountCliCases.AssertCase(new ScriptedSourcesFetchCountCliProbe(mutation))));
    }
}
