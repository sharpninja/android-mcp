using AndroidMcp.BuildTool.Documentation.Validation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Validation;

[Trait("Gate","Real"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class RepositoryDocumentationTreeRealTests
{
    [Theory,MemberData(nameof(RepositoryTreeCases.Cases),MemberType=typeof(RepositoryTreeCases))]
    public Task SameOriginalRootScopeCasesBindNativeCaller(string scenario)
    {var e=new RepositoryTreeEnvironment(scenario);return RepositoryTreeCases.AssertContract(e,new RepositoryDocumentationCheck(e,e));}
}
