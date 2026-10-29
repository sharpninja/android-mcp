using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class NativeDescriptorSpellingRealTests
{
    [Theory, MemberData(nameof(NativeDescriptorSpellingContractCases.Cases), MemberType = typeof(NativeDescriptorSpellingContractCases))]
    public void SameOriginalSpellingCasesBindExistingDescriptorAuthority(string id) =>
        NativeDescriptorContractCases.AssertContract(NativeDescriptorSpellingContractCases.Get(id), new NativeDescriptorSourceFacts());
}
