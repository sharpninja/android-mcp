using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record BootstrapLifecycleObservation(string? Error,bool InitiallyMissing,bool ReadAfterWrapperMode,int Acquisitions,bool ManifestContainsProperties,bool OriginalCapturedBytesRetained);
public static class BootstrapLifecycleCases
{
    public static readonly string[] Names=["MISSING-MISSING","MISSING-CREATED","PRESENT-BAD","POSTCAPTURE-CHANGE"];
    public static BootstrapLifecycleObservation Correct(string scenario)=>new(scenario switch{"MISSING-MISSING"=>"missing","PRESENT-BAD"=>"WRAPPER_PROPERTIES","POSTCAPTURE-CHANGE"=>"INPUT_PIN_MISMATCH",_=>null},scenario.StartsWith("MISSING-",StringComparison.Ordinal),true,scenario is "MISSING-MISSING" or "PRESENT-BAD"?4:5,scenario=="MISSING-CREATED",true);
    public static async Task AssertContract(string scenario,Func<string,Task<BootstrapLifecycleObservation>> probe)
    {
        var actual=await probe(scenario);var expected=Correct(scenario);Assert.Equal(expected.Error,actual.Error);Assert.Equal(expected.InitiallyMissing,actual.InitiallyMissing);Assert.True(actual.ReadAfterWrapperMode);Assert.Equal(expected.Acquisitions,actual.Acquisitions);Assert.Equal(expected.ManifestContainsProperties,actual.ManifestContainsProperties);Assert.True(actual.OriginalCapturedBytesRetained);
    }
    public static BootstrapLifecycleObservation Wrong(string scenario,string mutation)
    {var value=Correct(scenario);return mutation switch{"error"=>value with{Error="wrong"},"initial"=>value with{InitiallyMissing=!value.InitiallyMissing},"order"=>value with{ReadAfterWrapperMode=false},"acquisitions"=>value with{Acquisitions=0},"manifest"=>value with{ManifestContainsProperties=!value.ManifestContainsProperties},_=>value with{OriginalCapturedBytesRetained=false}};}
    public static IEnumerable<object[]> Cases=>Names.Select(value=>new object[]{value});
    public static IEnumerable<object[]> Faults=>Names.SelectMany(scenario=>new[]{"error","initial","order","acquisitions","manifest","retained"}.Select(mutation=>new object[]{scenario,mutation}));
}
public sealed record RetainedToolObservation(string? Error,int Reads,bool OriginalRetained);
public static class RetainedToolCases
{
    public static readonly string[] Names=["GOOD","CHANGED","MISSING"];
    public static RetainedToolObservation Correct(string scenario)=>new(scenario=="GOOD"?null:scenario=="CHANGED"?"ARTIFACT_PIN_HASH":"missing",scenario=="GOOD"?6:1,true);
    public static async Task AssertContract(string scenario,Func<string,Task<RetainedToolObservation>> probe)
    {var actual=await probe(scenario);var expected=Correct(scenario);Assert.Equal(expected.Error,actual.Error);Assert.Equal(expected.Reads,actual.Reads);Assert.True(actual.OriginalRetained);}
    public static IEnumerable<object[]> Cases=>Names.Select(value=>new object[]{value});
    public static IEnumerable<object[]> Faults=>Names.SelectMany(scenario=>new[]{"error","reads","retained"}.Select(mutation=>new object[]{scenario,mutation}));
}
[Trait("Gate","Mock"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationBootstrapLifecycleMockTests
{
    [Theory,MemberData(nameof(BootstrapLifecycleCases.Cases),MemberType=typeof(BootstrapLifecycleCases))]
    public Task CorrectOriginalLifecycleObservationsPass(string scenario)=>BootstrapLifecycleCases.AssertContract(scenario,value=>Task.FromResult(BootstrapLifecycleCases.Correct(value)));
    [Theory,MemberData(nameof(BootstrapLifecycleCases.Faults),MemberType=typeof(BootstrapLifecycleCases))]
    public async Task EveryOriginalLifecycleObservationRejectsWrongMock(string scenario,string mutation)=>Assert.NotNull(await Record.ExceptionAsync(()=>BootstrapLifecycleCases.AssertContract(scenario,value=>Task.FromResult(BootstrapLifecycleCases.Wrong(value,mutation)))));
    [Theory,MemberData(nameof(RetainedToolCases.Cases),MemberType=typeof(RetainedToolCases))]
    public Task CorrectActualRetainedPinObservationsPass(string scenario)=>RetainedToolCases.AssertContract(scenario,value=>Task.FromResult(RetainedToolCases.Correct(value)));
    [Theory,MemberData(nameof(RetainedToolCases.Faults),MemberType=typeof(RetainedToolCases))]
    public async Task EachRetainedPinObservationRejectsWrongMock(string scenario,string mutation)=>Assert.NotNull(await Record.ExceptionAsync(()=>RetainedToolCases.AssertContract(scenario,value=>{
        var correct=RetainedToolCases.Correct(value);return Task.FromResult(mutation=="error"?correct with{Error="wrong"}:mutation=="reads"?correct with{Reads=0}:correct with{OriginalRetained=false});})));
}
