using AndroidMcp.BuildTool.Documentation.Browser;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Browser;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-005")]
public sealed class BrowserShutdownRealTests
{
    public static IEnumerable<object[]> Cases=>BrowserShutdownCases.Cases;
    [Theory,MemberData(nameof(Cases))]
    public void SameSevenTerminalCasesBindToTheNarrowSessionPolicy(string id)=>
        BrowserShutdownCases.AssertContract(BrowserShutdownCases.Get(id),BrowserSession.IsExpectedShutdownTransportEnd);
}
