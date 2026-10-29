using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class ModelContractRealTests
{
    [Theory,MemberData(nameof(ModelContractCases.Data),MemberType=typeof(ModelContractCases))]
    public void SameOriginalClauseCasesBindNativeValidator(ModelContractCase test)
    {var input=test.Input();ModelContractAssertions.AssertContract(test,input,new ModelContractValidator(new NativeContentHash()));}
}
