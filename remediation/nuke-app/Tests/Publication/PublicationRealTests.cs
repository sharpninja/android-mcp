using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Publication;

[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class PublicationRealTests
{
    [Theory,MemberData(nameof(PublicationContractCases.Cases),MemberType=typeof(PublicationContractCases))]
    public void SameCasesAndAssertionsUseNativeChecker(PublicationCase c)=>
        PublicationContractCases.AssertCase(c,new PublicationChecker(new CanonicalJson()));
}
