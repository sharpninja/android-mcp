using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationModelReaderRealTests
{
    [Theory,MemberData(nameof(DocumentationModelReadCases.Data),MemberType=typeof(DocumentationModelReadCases))]
    public void SameReaderCasesBindCanonicalNativeReader(DocumentationModelReadCase test)=>DocumentationModelReadCases.AssertContract(test,new DocumentationModelReader());
}
