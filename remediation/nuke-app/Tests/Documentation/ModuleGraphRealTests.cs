using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class ModuleGraphRealTests
{
    [Theory,MemberData(nameof(ModuleGraphContractCases.Cases),MemberType=typeof(ModuleGraphContractCases))]
    public void SameOriginalAndBoundaryCasesBindNativeValidator(ModuleGraphCase test)=>ModuleGraphContractCases.AssertContract(test,new ModuleGraphValidator());
}
