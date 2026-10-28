using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-003")]
public sealed class ProcessSessionStreamHashRealTests
{
    [Theory,MemberData(nameof(ProcessStreamHashCases.Cases),MemberType=typeof(ProcessStreamHashCases))]
    public void IdenticalStreamVectorsBindToTheSharedHashAuthority(ProcessStreamHashCase test)=>ProcessStreamHashCases.AssertContract(test,ContentHash.Sha256);
    [Theory,InlineData(true),InlineData(false)]
    public void IdenticalNullAndUnreadableContractsBindToTheSharedHashAuthority(bool missing)=>ProcessStreamHashCases.AssertInvalid(missing,stream=>ContentHash.Sha256(stream!));
}
