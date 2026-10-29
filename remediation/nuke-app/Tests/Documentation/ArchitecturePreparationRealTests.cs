using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class ArchitecturePreparationRealTests
{
    [Theory,MemberData(nameof(ArchitecturePreparationCases.CodeCases),MemberType=typeof(ArchitecturePreparationCases))]
    public void SameEditableCodeViewCasesBindNativePreparation(CodeViewCase test)=>ArchitecturePreparationCases.AssertCode(test,new ArchitectureAssetPreparation());
    [Theory,MemberData(nameof(ArchitecturePreparationCases.VectorCases),MemberType=typeof(ArchitecturePreparationCases))]
    public void SameVectorCasesBindNativePreparation(VectorPreparationCase test)=>ArchitecturePreparationCases.AssertVector(test,new ArchitectureAssetPreparation());
}
