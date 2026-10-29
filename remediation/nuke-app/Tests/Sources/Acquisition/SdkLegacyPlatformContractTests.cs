using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed record SdkLegacyPlatformCase(string Id,string? Row,string? Transport,string? Text,string? Error=null,int NumericCalls=0);
public sealed record SdkLegacyPlatformObservation(string? Text,string? Error,int NumericCalls,bool InputsRetained);
public interface ISdkLegacyPlatformProbe{SdkLegacyPlatformObservation Run(SdkLegacyPlatformCase c);}
public static class SdkLegacyPlatformCases
{
    // Scalar expected spellings are pinned original Python truth/str behavior;
    // number rendering belongs to the existing canonical JSON authority.
    public static IEnumerable<object[]> All=>new[]{
        new SdkLegacyPlatformCase("absent-row-string-fallback",null,"\"37.2\"","37.2"),
        new("null-row-string-fallback","null","\"37.2\"","37.2"),
        new("empty-row-string-fallback","\"\"","\"37.2\"","37.2"),
        new("false-row-string-fallback","false","\"37.2\"","37.2"),
        new("zero-row-string-fallback","0","\"37.2\"","37.2"),
        new("negative-zero-row-string-fallback","-0.0","\"37.2\"","37.2"),
        new("underflow-row-string-fallback","1e-400","\"37.2\"","37.2"),
        new("empty-array-row-string-fallback","[]","\"37.2\"","37.2"),
        new("empty-object-row-string-fallback","{}","\"37.2\"","37.2"),
        new("row-string-dominates-number","\"37.2\"","36.0","37.2"),
        new("row-string-dominates-object","\"37.2\"","{\"x\":1}","37.2"),
        new("whitespace-remains-validator-input","\" \"","\"37.2\""," "),
        new("raw-string-is-not-json-quoted","\"a\\\"b\"",null,"a\"b"),
        new("row-number-dominates-fallback","37.2","\"36.0\"","37.2",NumericCalls:1),
        new("transport-number-fallback",null,"37.2","37.2",NumericCalls:1),
        new("integer-spelling",null,"37","37",NumericCalls:1),
        new("integral-float-spelling",null,"37.0","37.0",NumericCalls:1),
        new("exponent-integral-float",null,"1e3","1000.0",NumericCalls:1),
        new("small-exponent",null,"1e-5","1e-05",NumericCalls:1),
        new("large-exponent",null,"1e16","1e+16",NumericCalls:1),
        new("large-integer",null,"9007199254740993","9007199254740993",NumericCalls:1),
        new("row-true-spelling","true","\"37.2\"","True"),
        new("fallback-true-spelling",null,"true","True"),
        new("both-absent",null,null,null,"SDK_PLATFORM"),
        new("fallback-false",null,"false",null,"SDK_PLATFORM"),
        new("fallback-zero",null,"0",null,"SDK_PLATFORM"),
        new("fallback-empty",null,"\"\"",null,"SDK_PLATFORM"),
        new("row-nonempty-array-unsupported","[1]","\"37.2\"",null,"SDK_PLATFORM_KIND"),
        new("fallback-nonempty-object-unsupported",null,"{\"x\":1}",null,"SDK_PLATFORM_KIND")
    }.Select(c=>new object[]{c});
    public static void AssertCase(SdkLegacyPlatformCase c,ISdkLegacyPlatformProbe probe)
    {var a=probe.Run(c);Assert.Equal(c.Text,a.Text);Assert.Equal(c.Error,a.Error);Assert.Equal(c.NumericCalls,a.NumericCalls);Assert.True(a.InputsRetained);}
}
public sealed class ScriptedSdkLegacyPlatform(string? mutation=null):ISdkLegacyPlatformProbe
{
    public SdkLegacyPlatformObservation Run(SdkLegacyPlatformCase c)=>new(mutation=="text"?"wrong":c.Text,mutation=="error"?"wrong":c.Error,mutation=="numeric"?c.NumericCalls+1:c.NumericCalls,mutation!="retained");
}
[Trait("Gate","Mock")]
public sealed class SdkLegacyPlatformMockTests
{
    [Theory,MemberData(nameof(SdkLegacyPlatformCases.All),MemberType=typeof(SdkLegacyPlatformCases))]
    public void CorrectMock(SdkLegacyPlatformCase c)=>SdkLegacyPlatformCases.AssertCase(c,new ScriptedSdkLegacyPlatform());
    [Theory,MemberData(nameof(SdkLegacyPlatformCases.All),MemberType=typeof(SdkLegacyPlatformCases))]
    public void IndependentNegativeControls(SdkLegacyPlatformCase c)
    {foreach(var mutation in new[]{"text","error","numeric","retained"})Assert.NotNull(Record.Exception(()=>SdkLegacyPlatformCases.AssertCase(c,new ScriptedSdkLegacyPlatform(mutation))));}
}
