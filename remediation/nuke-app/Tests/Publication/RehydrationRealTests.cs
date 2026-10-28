using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Publication;

[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class RehydrationRealTests
{
    [Theory,MemberData(nameof(RehydrationContractCases.Cases),MemberType=typeof(RehydrationContractCases))]
    public void SameCasesAndAssertionsUseNativePlanner(RehydrationCase c)=>
        RehydrationContractCases.AssertCase(c,new RehydrationPlanner(new CanonicalJson()));
}
