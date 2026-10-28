using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
public sealed class PermissionReferenceRealTests
{
    [Theory, MemberData(nameof(PermissionReferenceContractCases.Cases), MemberType = typeof(PermissionReferenceContractCases))]
    public void SameCasesAndAssertionsBindNativeDocumentationFacts(string id) => PermissionReferenceContractCases.AssertContract(
        PermissionReferenceContractCases.Get(id), new PermissionReferenceSourceFacts(new SourceHtmlDecoder()));
}
