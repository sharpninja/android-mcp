using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationRendererProfileRealTests
{
    [Theory,MemberData(nameof(RendererProfileCases.Data),MemberType=typeof(RendererProfileCases))]
    public void SamePinnedProfileCasesBindNativeReader(RendererProfileCase test)=>RendererProfileCases.AssertProfile(test,new DocumentationRendererProfileReader());
}
