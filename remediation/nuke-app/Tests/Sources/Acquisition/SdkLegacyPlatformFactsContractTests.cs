using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed record SdkLegacyPlatformFactsCase(SdkLegacyPlatformCase Scalar,string? FactMutation=null);
public interface ISdkLegacyPlatformFactsProbe{SdkLegacyPlatformObservation Run(SdkLegacyPlatformFactsCase c);}
public static class SdkLegacyPlatformFactsCases
{
    public static IEnumerable<object[]> All=>SdkLegacyPlatformCases.All.Select(row=>new object[]{new SdkLegacyPlatformFactsCase((SdkLegacyPlatformCase)row[0])})
        .Concat(new[]{"absent-kind","absent-value","present-missing-value","kind-value-mismatch"}.Select(m=>new object[]{new SdkLegacyPlatformFactsCase(new("invalid-"+m,null,"37.2",null,"SDK_PLATFORM_FACTS"),m)}));
    public static void AssertCase(SdkLegacyPlatformFactsCase c,ISdkLegacyPlatformFactsProbe probe)
    {var a=probe.Run(c);Assert.Equal(c.Scalar.Text,a.Text);Assert.Equal(c.Scalar.Error,a.Error);Assert.Equal(c.Scalar.NumericCalls,a.NumericCalls);Assert.True(a.InputsRetained);}
}
public sealed class ScriptedSdkLegacyPlatformFacts(string? mutation=null):ISdkLegacyPlatformFactsProbe
{public SdkLegacyPlatformObservation Run(SdkLegacyPlatformFactsCase c)=>new ScriptedSdkLegacyPlatform(mutation).Run(c.Scalar);}
[Trait("Gate","Mock")]
public sealed class SdkLegacyPlatformFactsMockTests
{
    [Theory,MemberData(nameof(SdkLegacyPlatformFactsCases.All),MemberType=typeof(SdkLegacyPlatformFactsCases))]
    public void CorrectMock(SdkLegacyPlatformFactsCase c)=>SdkLegacyPlatformFactsCases.AssertCase(c,new ScriptedSdkLegacyPlatformFacts());
    [Theory,MemberData(nameof(SdkLegacyPlatformFactsCases.All),MemberType=typeof(SdkLegacyPlatformFactsCases))]
    public void IndependentFactBoundaryControls(SdkLegacyPlatformFactsCase c)
    {foreach(var m in new[]{"text","error","numeric","retained"})Assert.NotNull(Record.Exception(()=>SdkLegacyPlatformFactsCases.AssertCase(c,new ScriptedSdkLegacyPlatformFacts(m))));}
}
