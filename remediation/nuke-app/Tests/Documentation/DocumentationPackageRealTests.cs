using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationPackageRealTests
{
    [Theory,MemberData(nameof(DocumentationPackageCases.Data),MemberType=typeof(DocumentationPackageCases))]
    public void SameFullPackageCasesBindNativeBuilder(DocumentationPackageCase test)
    {var checker=new RecordingDocumentationPackageChecker(test.Error=="PACKAGE_POLICY");DocumentationPackageCases.AssertContract(test,checker,new DocumentationPackageBuilder(checker,new ArtifactReceiptVerifier(new NativeContentHash())));}
}
