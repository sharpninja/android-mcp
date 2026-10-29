using AndroidMcp.BuildTool.Sources.Java;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaHelperBodyRealTests
{
    [Theory,MemberData(nameof(JavaHelperBodyCases.ContextCases),MemberType=typeof(JavaHelperBodyCases))]
    public void SameOriginalContextBodyBindsOneManagedAdapter(string id)=>JavaHelperBodyCases.AssertContext(JavaHelperBodyCases.ContextCase(id),new JavaFileContext());
    [Theory,MemberData(nameof(JavaHelperBodyCases.WhitespaceCases),MemberType=typeof(JavaHelperBodyCases))]
    public void SameOriginalStripBoundariesBindOneLinkerAndSharedSourceText(string id)=>JvmContractCases.AssertContract(JavaHelperBodyCases.WhitespaceCase(id),new JvmSourceLinker());
}
