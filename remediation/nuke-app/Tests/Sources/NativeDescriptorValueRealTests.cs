using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class NativeDescriptorValueRealTests
{
    [Theory, MemberData(nameof(NativeDescriptorValueContractCases.Cases), MemberType = typeof(NativeDescriptorValueContractCases))]
    public void SameOriginalInputValueCasesBindNativeDescriptor(string id) =>
        NativeDescriptorValueContractCases.AssertContract(NativeDescriptorValueContractCases.Get(id), new NativeDescriptorSourceFacts());
}
