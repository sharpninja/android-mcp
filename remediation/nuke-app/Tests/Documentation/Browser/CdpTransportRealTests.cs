using AndroidMcp.BuildTool.Documentation.Browser;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Browser;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-005")]
public sealed class CdpTransportRealTests
{
    public static IEnumerable<object[]> Cases=>CdpTransportCases.Cases;
    [Theory,MemberData(nameof(Cases))]
    public async Task SameFramingCasesBindToNativeAssembly(string id)
    {var test=CdpTransportCases.Get(id);await using var transport=new NativeCdpTransport(new FixtureWebSocket(test.Frames));await CdpTransportCases.AssertContract(test,transport);}
}
