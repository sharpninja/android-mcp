using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationBuildPayloadRealTests
{
    [Theory,MemberData(nameof(DocumentationBuildPayloadCases.Cases),MemberType=typeof(DocumentationBuildPayloadCases))]
    public void SameRoutePrerequisiteAndErrorAssertionsBindNativeProjection(DocumentationBuildPayloadCase test)
        =>DocumentationBuildPayloadCases.AssertContract(test,new DocumentationBuildPayloadPlanner(new StrictJsonInspector(),new ArtifactReceiptVerifier(new NativeContentHash())));
}
