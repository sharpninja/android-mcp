using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationPrebuiltPackageRealTests
{
    [Theory,MemberData(nameof(DocumentationPrebuiltPackageCases.Cases),MemberType=typeof(DocumentationPrebuiltPackageCases))]
    public void SamePrebuiltCoverageAndArchiveCasesBindNativeOverload(DocumentationPrebuiltPackageCase test)
    {
        var admission=new RecordingPrebuiltManifestAdmission(test);var checker=new RecordingDocumentationPackageChecker(false);
        DocumentationPrebuiltPackageCases.AssertContract(test,new DocumentationPackageBuilder(checker,new ArtifactReceiptVerifier(new NativeContentHash()),admission),admission,checker);
    }
}
