using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

// Reuse the existing alias/source/snapshot/replay assertions and single native
// launcher. This adds only the new scheduling argument's caller obligation.
public static class SourcesFetchPacingCliCases
{
    public static async Task AssertCase(ISourcesFetchCliProbe provider)
    {
        var observation=await provider.RunAsync();
        await SourcesFetchAliasCliCases.AssertCase(new ObservationProbe(observation));
        Assert.Equal(2,observation.Arguments.Count);
        foreach(var arguments in observation.Arguments)
        {
            Assert.Equal(1,arguments.Count(value=>value=="--fetch-delay-seconds"));
            var index=arguments.ToList().IndexOf("--fetch-delay-seconds");
            Assert.InRange(index,0,arguments.Count-2);
            Assert.Equal("0",arguments[index+1]);
        }
    }
    private sealed class ObservationProbe(SourcesFetchCliObservation observation):ISourcesFetchCliProbe
    {public Task<SourcesFetchCliObservation> RunAsync()=>Task.FromResult(observation);}
}

public sealed class ScriptedSourcesFetchPacingCliProbe(string? mutation=null):ISourcesFetchCliProbe
{
    public async Task<SourcesFetchCliObservation> RunAsync()
    {
        var observation=await new ScriptedSourcesFetchAliasCliProbe().RunAsync();
        var arguments=observation.Arguments.Select((original,index)=>
        {
            var values=original.ToList();
            if(mutation!="omitted"&&!(mutation=="resume-omitted"&&index==1))
                values.AddRange(["--fetch-delay-seconds",mutation=="wrong-value"?"1":"0"]);
            if(mutation=="duplicate")values.AddRange(["--fetch-delay-seconds","0"]);
            return (IReadOnlyList<string>)values.ToArray();
        }).ToArray();
        return observation with{Arguments=arguments};
    }
}

[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
public sealed class SourcesFetchPacingCliMockTests
{
    [Fact]
    public Task CorrectZeroDelayAndReplayCallerUsesExistingAssertions()
        =>SourcesFetchPacingCliCases.AssertCase(new ScriptedSourcesFetchPacingCliProbe());
    [Theory]
    [InlineData("omitted")][InlineData("resume-omitted")][InlineData("wrong-value")][InlineData("duplicate")]
    public async Task EachArgumentOmissionValueAndMultiplicityDriftIsRejected(string mutation)
        =>Assert.NotNull(await Record.ExceptionAsync(()=>SourcesFetchPacingCliCases.AssertCase(new ScriptedSourcesFetchPacingCliProbe(mutation))));
}
