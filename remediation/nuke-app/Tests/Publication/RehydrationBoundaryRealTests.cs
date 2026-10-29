using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Publication;

[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class RehydrationBoundaryRealTests
{
    [Theory,MemberData(nameof(RehydrationBoundaryCases.Cases),MemberType=typeof(RehydrationBoundaryCases))]
    public void SameCasesAndAssertionsBindNativePlanner(RehydrationBoundaryCase c)=>
        RehydrationBoundaryCases.AssertCase(c,new RehydrationPlanner(new CanonicalJson()));
}
