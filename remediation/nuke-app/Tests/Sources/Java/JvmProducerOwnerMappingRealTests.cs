using AndroidMcp.BuildTool.Sources.Java;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JvmProducerOwnerMappingRealTests
{
    [Theory,MemberData(nameof(JvmProducerOwnerMappingCases.Cases),MemberType=typeof(JvmProducerOwnerMappingCases))]
    public void SameOriginalFalseyOwnerCasesBindToExistingLinker(string id)=>
        JvmProducerOwnerMappingCases.AssertContract(JvmProducerOwnerMappingCases.Get(id),new JvmSourceLinker());
}
