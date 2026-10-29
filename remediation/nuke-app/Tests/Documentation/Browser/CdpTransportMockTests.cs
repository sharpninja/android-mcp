using AndroidMcp.BuildTool.Documentation.Browser;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Browser;

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-005")]
public sealed class CdpTransportMockTests
{
    public static IEnumerable<object[]> Cases=>CdpTransportCases.Cases;
    [Theory,MemberData(nameof(Cases))]
    public async Task CorrectFramingMocksDistinguishCleanCloseEmptyTextAndTruncatedFragments(string id)
    {var test=CdpTransportCases.Get(id);await using var transport=new FixtureMessageTransport(test);await CdpTransportCases.AssertContract(test,transport);}
    [Theory,MemberData(nameof(Cases))]
    public async Task IndependentNullEmptyTruncationAndErrorControlsAreRejected(string id)
    {
        var test=CdpTransportCases.Get(id);await using var wrong=new FixtureMessageTransport(test,test.Error is null?"flip-null":"error");Assert.NotNull(await Record.ExceptionAsync(()=>CdpTransportCases.AssertContract(test,wrong)));
        if(test.Expected is {Length:>0}){await using var truncated=new FixtureMessageTransport(test,"truncate");Assert.NotNull(await Record.ExceptionAsync(()=>CdpTransportCases.AssertContract(test,truncated)));}
    }
}
