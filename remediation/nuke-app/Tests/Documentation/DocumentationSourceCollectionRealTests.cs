using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationSourceCollectionRealTests
{
    private static IDocumentationSourceCollection Create(SourceCollectionEnvironment e)=>new DocumentationSourceCollection(e.Project,e.Catalog,e.Pins,e.Planner,e.Verifier,new CanonicalJson());
    [Theory,MemberData(nameof(DocumentationSourceCollectionCases.CaptureCases),MemberType=typeof(DocumentationSourceCollectionCases))]
    public Task NativeCollectorBindsEveryCaptureMockCase(DocumentationCaptureCase test){var e=new SourceCollectionEnvironment(test);return DocumentationSourceCollectionCases.AssertCapture(e,Create(e));}
    [Theory,MemberData(nameof(DocumentationSourceCollectionCases.VerifyCases),MemberType=typeof(DocumentationSourceCollectionCases))]
    public Task NativeCollectorBindsEveryCommitTimeMockCase(DocumentationVerifyCase test){var e=new SourceCollectionEnvironment(new("VERIFY",test.AbsentCanonicalMask));return DocumentationSourceCollectionCases.AssertVerify(test,e,Create(e));}
}
