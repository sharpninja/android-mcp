using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Publication;

[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class OperationHelpRealTests
{
    [Theory,MemberData(nameof(OperationHelpContractCases.Cases),MemberType=typeof(OperationHelpContractCases))]
    public void SameCasesAndAssertionsUseRegistryDerivedNativeHelp(HelpCase c)=>
        OperationHelpContractCases.AssertCase(c,new OperationHelpBuilder(new CanonicalJson()));
}
