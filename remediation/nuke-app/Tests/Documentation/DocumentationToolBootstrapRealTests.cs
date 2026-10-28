using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationToolBootstrapRealTests
{
    [Theory,MemberData(nameof(DocumentationBootstrapCases.Data),MemberType=typeof(DocumentationBootstrapCases))]
    public Task SameTypedToolRequestsBindNativeBootstrap(DocumentationBootstrapCase test)
    {var environment=new DocumentationBootstrapEnvironment(test);return DocumentationBootstrapCases.AssertContract(test,environment,new DocumentationToolBootstrap(new RecordingDocumentationToolAcquirer(environment)));}
}
