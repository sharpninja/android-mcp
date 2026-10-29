using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed record MetadataCallerCase(string Id,string? Change=null,string? Error=null,bool Download=true,bool Existing=false,bool Generic=false,
    int ProofReads=2,int BindCalls=2,int Versions=2,bool Network=true,int SourceWrites=2,int Writes=3,bool Sealed=true,int Evidence=2,int Factories=1);
public sealed record MetadataCallerObservation(string? Error,int ProofReads,int BindCalls,int Versions,bool Network,int SourceWrites,int Writes,
    bool Sealed,int Evidence,int Factories,bool BoundedRanges,bool ArchiveReused,bool InputsRetained,bool LocalOnly,bool Traceable,bool WholeArchiveIdentityClaimed);
public interface IMetadataCallerProbe { Task<MetadataCallerObservation> RunAsync(MetadataCallerCase test); }
public static class MetadataCallerCases
{
    public static IEnumerable<object[]> All=>new[]{
        new MetadataCallerCase("canonical-external-proof-to-shared-range-engine"),
        new MetadataCallerCase("dry-run-never-reads-external-proof",Change:"missing-pins",Download:false,ProofReads:0,BindCalls:0,Versions:0,Network:false,SourceWrites:0,Writes:0,Sealed:false,Evidence:0,Factories:0),
        new MetadataCallerCase("exact-targets-never-read-external-proof",Change:"missing-pins",Existing:true,ProofReads:0,BindCalls:0,Versions:0,Network:false,SourceWrites:0,Writes:0,Sealed:false,Evidence:0,Factories:0),
        new MetadataCallerCase("explicit-generic-keeps-independent-range-profile",Change:"bad-json",Generic:true,ProofReads:0,BindCalls:0,Versions:0,Evidence:0),
        new MetadataCallerCase("missing-trusted-pin-no-effects",Change:"missing-pins",Error:"METADATA_PIN_REQUIRED",ProofReads:0,BindCalls:1,Versions:0,Network:false,SourceWrites:0,Writes:0,Sealed:false,Evidence:0,Factories:0),
        new MetadataCallerCase("receipt-pin-mismatch-no-effects",Change:"hash",Error:"ARTIFACT_PIN_HASH",ProofReads:1,BindCalls:1,Versions:0,Network:false,SourceWrites:0,Writes:0,Sealed:false,Evidence:0,Factories:0),
        new MetadataCallerCase("xml-package-mismatch-no-effects",Change:"package",Error:"SDK_PACKAGE",BindCalls:1,Versions:1,Network:false,SourceWrites:0,Writes:0,Sealed:false,Evidence:0,Factories:0),
        new MetadataCallerCase("original-legacy-transport-platform-fallback",Change:"platform-fallback"),
        new MetadataCallerCase("range-version-refusal-no-seal",Change:"range",Error:"CONTENT_RANGE",SourceWrites:0,Writes:0,Sealed:false),
        new MetadataCallerCase("second-member-hash-failure-retains-first-no-seal",Change:"member",Error:"SOURCE_HASH",SourceWrites:1,Writes:1,Sealed:false),
        new MetadataCallerCase("canonical-proof-reader-is-required",Change:"no-reader",Error:"METADATA_PROOF_REQUIRED",ProofReads:0,BindCalls:0,Versions:0,Network:false,SourceWrites:0,Writes:0,Sealed:false,Evidence:0,Factories:0),
        new MetadataCallerCase("bounded-kind-never-downloads-whole-container",Change:"declared-hash")
    }.Select(value=>new object[]{value});
    public static MetadataCallerObservation Expected(MetadataCallerCase test)=>new(test.Error,test.ProofReads,test.BindCalls,test.Versions,
        test.Network,test.SourceWrites,test.Writes,test.Sealed,test.Evidence,test.Factories,true,true,true,true,true,false);
    public static async Task AssertCase(MetadataCallerCase test,IMetadataCallerProbe probe)
    {
        var expected=Expected(test);var actual=await probe.RunAsync(test);
        Assert.Equal(expected.Error,actual.Error);Assert.Equal(expected.ProofReads,actual.ProofReads);Assert.Equal(expected.BindCalls,actual.BindCalls);
        Assert.Equal(expected.Versions,actual.Versions);Assert.Equal(expected.Network,actual.Network);Assert.Equal(expected.SourceWrites,actual.SourceWrites);
        Assert.Equal(expected.Writes,actual.Writes);Assert.Equal(expected.Sealed,actual.Sealed);Assert.Equal(expected.Evidence,actual.Evidence);
        Assert.Equal(expected.Factories,actual.Factories);Assert.True(actual.BoundedRanges);Assert.True(actual.ArchiveReused);Assert.True(actual.InputsRetained);
        Assert.True(actual.LocalOnly);Assert.True(actual.Traceable);Assert.False(actual.WholeArchiveIdentityClaimed);
    }
}
public sealed class ScriptedMetadataCallerProbe(string? mutation=null):IMetadataCallerProbe
{
    public Task<MetadataCallerObservation> RunAsync(MetadataCallerCase test)
    {
        var value=MetadataCallerCases.Expected(test);
        return Task.FromResult(mutation switch{
            "error"=>value with{Error="WRONG"},"reads"=>value with{ProofReads=99},"bind"=>value with{BindCalls=99},"version"=>value with{Versions=99},
            "network"=>value with{Network=!value.Network},"source"=>value with{SourceWrites=99},"writes"=>value with{Writes=99},
            "seal"=>value with{Sealed=!value.Sealed},"evidence"=>value with{Evidence=99},"factory"=>value with{Factories=99},
            "range"=>value with{BoundedRanges=false},"reuse"=>value with{ArchiveReused=false},"input"=>value with{InputsRetained=false},
            "local"=>value with{LocalOnly=false},"trace"=>value with{Traceable=false},"whole"=>value with{WholeArchiveIdentityClaimed=true},_=>value});
    }
}
[Trait("Gate","Mock")]
public sealed class MetadataCallerMockTests
{
    [Theory,MemberData(nameof(MetadataCallerCases.All),MemberType=typeof(MetadataCallerCases))]
    public Task CorrectOriginalCallerProofComposition(MetadataCallerCase test)=>MetadataCallerCases.AssertCase(test,new ScriptedMetadataCallerProbe());
    [Theory,MemberData(nameof(MetadataCallerCases.All),MemberType=typeof(MetadataCallerCases))]
    public async Task EveryBoundaryCounterIdentityAndEffectControlDiscriminates(MetadataCallerCase test)
    {
        foreach(var mutation in new[]{"error","reads","bind","version","network","source","writes","seal","evidence","factory","range","reuse","input","local","trace","whole"})
            Assert.NotNull(await Record.ExceptionAsync(()=>MetadataCallerCases.AssertCase(test,new ScriptedMetadataCallerProbe(mutation))));
    }
}
