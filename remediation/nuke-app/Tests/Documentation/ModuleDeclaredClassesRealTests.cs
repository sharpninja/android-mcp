using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class ModuleDeclaredClassesRealTests
{
    [Theory,MemberData(nameof(ModuleGraphContractCases.Cases),MemberType=typeof(ModuleGraphContractCases))]
    public void SameLegacyCasesBindExistingNativeClassAuthority(ModuleGraphCase test)=>ModuleDeclaredClassCases.AssertContract(test,new ModuleGraphValidator());
}
