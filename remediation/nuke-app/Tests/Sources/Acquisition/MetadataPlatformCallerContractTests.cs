using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

// Fixture input values only. Production DTO/projection remains Publication-owned.
public sealed record MetadataPlatformCallerInput(string? Row,string? LegacyTransport,string? NativeTransport=null);
public sealed record MetadataPlatformCallerCase(string Id,MetadataPlatformCallerInput Input,MetadataCallerCase Caller,string[] Platforms);
public sealed record MetadataPlatformCallerObservation(MetadataCallerObservation Caller,string[] Platforms);
public interface IMetadataPlatformCallerProbe{Task<MetadataPlatformCallerObservation> RunAsync(MetadataPlatformCallerCase c);}
public static class MetadataPlatformCallerCases
{
    private static MetadataCallerCase Refusal(string id,string error,int reads=1,int versions=0)=>new(id,Error:error,ProofReads:reads,BindCalls:1,Versions:versions,Network:false,SourceWrites:0,Writes:0,Sealed:false,Evidence:0,Factories:0);
    private static MetadataCallerCase Bypass(string id,bool download=true,bool existing=false,bool generic=false)=>new(id,Change:"missing-pins",Download:download,Existing:existing,Generic:generic,ProofReads:0,BindCalls:0,Versions:0,Network:generic,SourceWrites:generic?2:0,Writes:generic?3:0,Sealed:generic,Evidence:0,Factories:generic?1:0);
    public static IEnumerable<object[]> All=>new[]{
        new MetadataPlatformCallerCase("transport-numeric-exact-sdk-package",new(null,"37.2"),new("numeric"),["37.2","37.2"]),
        new("row-false-falls-back-despite-native-platform",new("false","\"37.2\"","99.0"),new("false-row"),["37.2","37.2"]),
        new("row-numeric-precedence-over-native-and-legacy",new("37.2","\"36.0\"","99.0"),new("numeric-row"),["37.2","37.2"]),
        new("row-string-precedence-over-number",new("\"37.2\"","36.0"),new("string-row"),["37.2","37.2"]),
        new("fallback-false-refusal-before-xml",new(null,"false"),Refusal("false","SDK_PLATFORM"),[]),
        new("fallback-zero-refusal-before-xml",new(null,"0"),Refusal("zero","SDK_PLATFORM"),[]),
        new("native-platform-does-not-substitute-original-key",new(null,null,"99.0"),Refusal("native-key","SDK_PLATFORM"),[]),
        new("whitespace-keeps-strict-sdk-admission",new("\" \"","\"37.2\""),Refusal("whitespace","SDK_PLATFORM",2,1),[" "]),
        new("wrong-xml-package-still-refused",new(null,"37.2"),Refusal("wrong-package","SDK_PACKAGE",2,1) with{Change="package"},["37.2"]),
        new("numeric-default-dry-run-no-proof-reads",new(null,"37.2"),Bypass("dry",download:false),[]),
        new("numeric-existing-targets-no-proof-reads",new(null,"37.2"),Bypass("reuse",existing:true),[]),
        new("explicit-generic-no-proof-conversion",new(null,"37.2"),Bypass("generic",generic:true),[]),
        new("nonempty-container-explicit-unsupported-domain",new(null,"{\"x\":1}"),Refusal("container","SDK_PLATFORM_KIND"),[])
    }.Select(c=>new object[]{c});
    public static async Task AssertCase(MetadataPlatformCallerCase c,IMetadataPlatformCallerProbe probe)
    {
        var a=await probe.RunAsync(c);await MetadataCallerCases.AssertCase(c.Caller,new Captured(a.Caller));Assert.Equal(c.Platforms,a.Platforms);
    }
    private sealed class Captured(MetadataCallerObservation observation):IMetadataCallerProbe
    {public Task<MetadataCallerObservation> RunAsync(MetadataCallerCase test)=>Task.FromResult(observation);}
}
public sealed class ScriptedMetadataPlatformCaller(string? mutation=null):IMetadataPlatformCallerProbe
{
    public async Task<MetadataPlatformCallerObservation> RunAsync(MetadataPlatformCallerCase c)
    {
        var caller=await new ScriptedMetadataCallerProbe(mutation=="platform"?null:mutation).RunAsync(c.Caller);
        return new(caller,mutation=="platform"?["wrong"]:c.Platforms);
    }
}
[Trait("Gate","Mock")]
public sealed class MetadataPlatformCallerMockTests
{
    [Theory,MemberData(nameof(MetadataPlatformCallerCases.All),MemberType=typeof(MetadataPlatformCallerCases))]
    public Task CorrectMock(MetadataPlatformCallerCase c)=>MetadataPlatformCallerCases.AssertCase(c,new ScriptedMetadataPlatformCaller());
    [Theory,MemberData(nameof(MetadataPlatformCallerCases.All),MemberType=typeof(MetadataPlatformCallerCases))]
    public async Task IndependentCountersEffectsAndPrecedenceControls(MetadataPlatformCallerCase c)
    {
        foreach(var mutation in new[]{"platform","error","reads","bind","version","network","source","writes","seal","evidence","factory","range","reuse","input","local","trace","whole"})
            Assert.NotNull(await Record.ExceptionAsync(()=>MetadataPlatformCallerCases.AssertCase(c,new ScriptedMetadataPlatformCaller(mutation))));
    }
}
