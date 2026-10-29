using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed record SourceTransportCompositionCase(string Id,SnapshotCallerMode? Mode,HttpByteTransportProfile Profile,string Outcome);
public sealed record SourceTransportCompositionObservation(HttpByteTransportProfile Profile,int FactoryCalls,int Sends,
    bool RequestPreserved,bool TokenPreserved,bool ResponseIdentity,bool ErrorIdentity,bool RetryPreserved);
public interface ISourceTransportCompositionProbe{Task<SourceTransportCompositionObservation> RunAsync(SourceTransportCompositionCase c);}
public static class SourceTransportCompositionCases
{
    public static IEnumerable<object[]> All=>new[]{
        (Mode:(SnapshotCallerMode?)null,Profile:HttpByteTransportProfile.Default,Name:"generic-fetch-seed"),
        (Mode:(SnapshotCallerMode?)SnapshotCallerMode.Restore,Profile:HttpByteTransportProfile.SourceSnapshotRestore,Name:"snapshot-restore-60"),
        (Mode:(SnapshotCallerMode?)SnapshotCallerMode.Fetch,Profile:HttpByteTransportProfile.SourceSnapshotFetch,Name:"snapshot-fetch-45")
    }.SelectMany(mode=>new[]{"success","failure","cancelled"}.Select(outcome=>new object[]{new SourceTransportCompositionCase(mode.Name+"-"+outcome,mode.Mode,mode.Profile,outcome)}));
    public static async Task AssertCase(SourceTransportCompositionCase c,ISourceTransportCompositionProbe probe)
    {
        var a=await probe.RunAsync(c);Assert.Equal(c.Profile,a.Profile);Assert.Equal(1,a.FactoryCalls);Assert.Equal(1,a.Sends);
        Assert.True(a.RequestPreserved);Assert.True(a.TokenPreserved);Assert.True(a.ResponseIdentity);Assert.True(a.ErrorIdentity);Assert.True(a.RetryPreserved);
    }
}
public sealed class ScriptedSourceTransportComposition(string? mutation=null):ISourceTransportCompositionProbe
{
    public Task<SourceTransportCompositionObservation> RunAsync(SourceTransportCompositionCase c)=>Task.FromResult(new SourceTransportCompositionObservation(
        mutation=="profile"?HttpByteTransportProfile.DocumentationBootstrap:c.Profile,mutation=="factory"?2:1,mutation=="send"?2:1,
        mutation!="request",mutation!="token",mutation!="response",mutation!="error",mutation!="retry"));
}
[Trait("Gate","Mock")]
public sealed class SourceTransportCompositionMockTests
{
    [Theory,MemberData(nameof(SourceTransportCompositionCases.All),MemberType=typeof(SourceTransportCompositionCases))]
    public Task CorrectMock(SourceTransportCompositionCase c)=>SourceTransportCompositionCases.AssertCase(c,new ScriptedSourceTransportComposition());
    [Theory,MemberData(nameof(SourceTransportCompositionCases.All),MemberType=typeof(SourceTransportCompositionCases))]
    public async Task IndependentNegativeControls(SourceTransportCompositionCase c)
    {foreach(var mutation in new[]{"profile","factory","send","request","token","response","error","retry"})Assert.NotNull(await Record.ExceptionAsync(()=>SourceTransportCompositionCases.AssertCase(c,new ScriptedSourceTransportComposition(mutation))));}
}
