using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate", "Real"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class LegacyJsonValueEqualityRealTests
{
    [Theory, MemberData(nameof(LegacyJsonValueEqualityContractCases.Cases), MemberType = typeof(LegacyJsonValueEqualityContractCases))]
    public void SameAdmittedNumericAndStructuralCasesBindSharedNativeAuthority(string id) =>
        LegacyJsonValueEqualityContractCases.AssertContract(LegacyJsonValueEqualityContractCases.Get(id), new LegacyJsonValueEquality());
}
