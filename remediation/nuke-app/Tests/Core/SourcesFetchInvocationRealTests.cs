using AndroidMcp.BuildTool.Execution;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate","Real")][Trait("Requirement","TEST-BUILD-004")]
public sealed class SourcesFetchInvocationRealTests
{
    [Theory][MemberData(nameof(SourcesFetchInvocationMockTests.Cases),MemberType=typeof(SourcesFetchInvocationMockTests))]
    public void SamePhysicalInvocationAndReceiptRetentionAssertions(string id)
    {
        var test=new SourcesFetchInvocationCase(id);
        test.AssertContract(new SourcesFetchInvocationProjection());
    }
}
