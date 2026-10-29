using System.Text.Json;
using AndroidMcp.BuildTool.Publication;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed class NativeSdkLegacyPlatformFacts:ISdkLegacyPlatformFactsProbe
{
    public SdkLegacyPlatformObservation Run(SdkLegacyPlatformFactsCase c)=>new NativeSdkLegacyPlatform((service,row,transport)=>
    {
        static RehydrationLegacyPlatformValue Fact(JsonElement? value)=>new(value.HasValue,value?.ValueKind??JsonValueKind.Undefined,value?.Clone());
        var rowFact=Fact(row);var transportFact=Fact(transport);
        rowFact=c.FactMutation switch
        {
            "absent-kind"=>new(false,JsonValueKind.Null,null),
            "absent-value"=>new(false,JsonValueKind.Undefined,JsonSerializer.SerializeToElement(1)),
            "present-missing-value"=>new(true,JsonValueKind.Number,null),
            "kind-value-mismatch"=>new(true,JsonValueKind.String,JsonSerializer.SerializeToElement(1)),
            _=>rowFact
        };
        return service.Resolve(new RehydrationLegacyPlatformFacts(rowFact,transportFact));
    }).Run(c.Scalar);
}
[Trait("Gate","Real")]
public sealed class SdkLegacyPlatformFactsRealTests
{
    [Theory,MemberData(nameof(SdkLegacyPlatformFactsCases.All),MemberType=typeof(SdkLegacyPlatformFactsCases))]
    public void SameTypedFactsUseOneScalarAuthority(SdkLegacyPlatformFactsCase c)=>SdkLegacyPlatformFactsCases.AssertCase(c,new NativeSdkLegacyPlatformFacts());
}
