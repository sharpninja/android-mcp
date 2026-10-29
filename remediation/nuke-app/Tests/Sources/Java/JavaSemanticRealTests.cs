using AndroidMcp.BuildTool.Sources.Java;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaSemanticRealTests
{
    [Theory,MemberData(nameof(JvmContractCases.Cases),MemberType=typeof(JvmContractCases))]
    public void SameJvmCasesBindToRealLinker(string id)=>JvmContractCases.AssertContract(JvmContractCases.Get(id),new JvmSourceLinker());
    [Theory,MemberData(nameof(JavaContextCases.Cases),MemberType=typeof(JavaContextCases))]
    public void SameCacheContextCasesBindToRealAdapter(string id)=>JavaContextCases.AssertContract(JavaContextCases.Get(id),new JavaFileContext());
}
