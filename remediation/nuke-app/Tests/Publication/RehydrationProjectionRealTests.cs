using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Publication;

[Trait("Requirement","TEST-BUILD-007")]
public sealed class RehydrationProjectionRealTests
{
    [Theory,MemberData(nameof(RehydrationProjectionCases.Cases),MemberType=typeof(RehydrationProjectionCases))]
    public void SameProjectionCasesBindSingleNativeRecipePlanner(ProjectionCase c)=>
        RehydrationProjectionCases.AssertCase(c,new RehydrationPlanner(new CanonicalJson()));
}
