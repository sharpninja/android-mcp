using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

// tools/rehydrate.py main: --delay defaults to 1s and is applied AFTER each
// newly materialized source, including the final source. No legacy execution.
public sealed record SourceBatchPacingCase(string Id, AcquisitionCompatibilityProfile Profile,
    double? DelaySeconds, string Mode, string? Error, bool Sealed, bool Reused,
    IReadOnlyList<string> Events);
public sealed record SourceBatchPacingObservation(string? Error, bool Sealed, bool Reused,
    IReadOnlyList<string> Events, bool OriginalsRetained, bool? DelayTokenMatches,
    string? ReceiptRequestSha256);
public interface ISourceBatchPacingProbe
{
    Task<SourceBatchPacingObservation> RunAsync(SourceBatchPacingCase test);
}

public static class SourceBatchPacingCases
{
    private const AcquisitionCompatibilityProfile Generic=AcquisitionCompatibilityProfile.GenericRehydrate;
    private const AcquisitionCompatibilityProfile Canonical=AcquisitionCompatibilityProfile.CanonicalRestore;
    public static IEnumerable<object[]> All => new SourceBatchPacingCase[]
    {
        new("generic-default",Generic,null,"normal",null,true,false,
            ["fetch:raw/a","write:raw/a","delay:1","fetch:raw/b","write:raw/b","delay:1","seal"]),
        new("generic-zero",Generic,0,"normal",null,true,false,
            ["fetch:raw/a","write:raw/a","fetch:raw/b","write:raw/b","seal"]),
        new("generic-fraction",Generic,.25,"normal",null,true,false,
            ["fetch:raw/a","write:raw/a","delay:0.25","fetch:raw/b","write:raw/b","delay:0.25","seal"]),
        new("mixed-reuse",Generic,null,"mixed",null,true,false,
            ["fetch:raw/b","write:raw/b","delay:1","seal"]),
        new("all-reused",Generic,null,"all-reused",null,true,false,["seal"]),
        new("empty-selection",Generic,null,"empty",null,true,false,["seal"]),
        new("canonical-default-unchanged",Canonical,null,"normal",null,true,false,
            ["fetch:raw/a","write:raw/a","fetch:raw/b","write:raw/b","seal"]),
        new("canonical-explicit",Canonical,.5,"normal",null,true,false,
            ["fetch:raw/a","write:raw/a","delay:0.5","fetch:raw/b","write:raw/b","delay:0.5","seal"]),
        new("negative-before-effects",Generic,-1,"normal","BATCH_DELAY",false,false,[]),
        new("nonfinite-before-effects",Generic,double.NaN,"normal","BATCH_DELAY",false,false,[]),
        new("infinity-before-effects",Generic,double.PositiveInfinity,"normal","BATCH_DELAY",false,false,[]),
        new("second-transfer-failure",Generic,null,"fail-second","SCRIPTED_HTTP",false,false,
            ["fetch:raw/a","write:raw/a","delay:1","fetch:raw/b"]),
        new("cancel-before",Generic,null,"cancel-before","CANCELLED",false,false,[]),
        new("cancel-in-delay",Generic,null,"cancel-delay","CANCELLED",false,false,
            ["fetch:raw/a","write:raw/a","delay:1"]),
        new("delay-failure-no-seal",Generic,null,"delay-fault","DELAY_FAILURE",false,false,
            ["fetch:raw/a","write:raw/a","delay:1"]),
        new("sealed-replay-no-delay",Generic,null,"replay",null,true,true,[])
    }.Select(test=>new object[]{test});

    public static SourceBatchRequest Request(SourceBatchPacingCase test)
    {
        var a=SourceBatchCases.Pin("raw/a",test.Profile);
        var b=SourceBatchCases.Pin("raw/b",test.Profile) with{Url=new Uri("https://example.test/"+new string('a',40)+"/b")};
        return SourceBatchCases.Request(
            [SourceBatchCases.Physical(0,a),SourceBatchCases.Physical(1,b)],
            [SourceBatchCases.Row(0,0),SourceBatchCases.Row(1,1)],
            test.Mode=="empty"?[]:[0,1],profile:test.Profile);
    }
    public static async Task AssertCase(SourceBatchPacingCase test,ISourceBatchPacingProbe provider)
    {
        var actual=await provider.RunAsync(test);
        Assert.Equal(test.Error,actual.Error);
        Assert.Equal(test.Sealed,actual.Sealed);
        Assert.Equal(test.Reused,actual.Reused);
        Assert.Equal(test.Events,actual.Events);
        Assert.True(actual.OriginalsRetained);
        bool? expectedToken=test.Events.Any(e=>e.StartsWith("delay:",StringComparison.Ordinal))?true:null;
        Assert.Equal(expectedToken,actual.DelayTokenMatches);
        Assert.Equal(test.Sealed?SourceBatchCases.RequestHash(Request(test)):null,actual.ReceiptRequestSha256);
    }
}

public sealed class ScriptedSourceBatchPacingProbe(string? mutation=null):ISourceBatchPacingProbe
{
    public Task<SourceBatchPacingObservation> RunAsync(SourceBatchPacingCase test)
    {
        IReadOnlyList<string> events=test.Events.ToArray();
        if(mutation=="omission")events=events.Count==0?["unexpected"]:events.Skip(1).ToArray();
        if(mutation=="extra")events=events.Append("unexpected").ToArray();
        if(mutation=="order")events=events.Count>1?events.Reverse().ToArray():events.Append("wrong-order").ToArray();
        bool? token=test.Events.Any(e=>e.StartsWith("delay:",StringComparison.Ordinal))?true:null;
        return Task.FromResult(new SourceBatchPacingObservation(
            mutation=="error"?"WRONG":test.Error,
            mutation=="sealed"?!test.Sealed:test.Sealed,
            mutation=="reused"?!test.Reused:test.Reused,events,
            mutation!="retention",mutation=="token"?false:token,
            mutation=="request-identity"?"wrong":test.Sealed?SourceBatchCases.RequestHash(SourceBatchPacingCases.Request(test)):null));
    }
}

[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
public sealed class SourceBatchPacingMockTests
{
    [Theory,MemberData(nameof(SourceBatchPacingCases.All),MemberType=typeof(SourceBatchPacingCases))]
    public Task CorrectDeclaredPacing(SourceBatchPacingCase test)
        =>SourceBatchPacingCases.AssertCase(test,new ScriptedSourceBatchPacingProbe());

    [Theory,MemberData(nameof(SourceBatchPacingCases.All),MemberType=typeof(SourceBatchPacingCases))]
    public async Task EachOutcomeAndOrderedEffectDiscriminates(SourceBatchPacingCase test)
    {
        foreach(var mutation in new[]{"error","sealed","reused","omission","extra","order","retention","token","request-identity"})
            await Assert.ThrowsAnyAsync<Exception>(()=>SourceBatchPacingCases.AssertCase(test,new ScriptedSourceBatchPacingProbe(mutation)));
    }
}
