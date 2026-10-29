using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class ArchitectureViewIndexRealTests
{
    [Theory,MemberData(nameof(ArchitectureViewIndexCases.Cases),MemberType=typeof(ArchitectureViewIndexCases))]
    public void SameViewIndexCasesBindNativeAuthority(ArchitectureViewIndexCase test)
        =>ArchitectureViewIndexCases.AssertContract(test,new ArchitectureViewIndexReader());
}
