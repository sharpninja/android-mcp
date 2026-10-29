using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class LegacyGuardOriginalMethodRealTests
{
    [Theory,MemberData(nameof(LegacyGuardOriginalCases.Data),MemberType=typeof(LegacyGuardOriginalCases))]
    public void SameOriginalScenariosBindExistingNativeValidator(LegacyGuardCase test)
    {
        var input=LegacyGuardOriginalCases.Input(test);
        LegacyGuardOriginalCases.AssertOriginal(test,input,new ModelContractValidator(new NativeContentHash()));
    }
}
