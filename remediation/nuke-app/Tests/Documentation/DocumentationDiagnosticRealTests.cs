using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Documentation.Validation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationDiagnosticRealTests
{
    [Theory,MemberData(nameof(DocumentationDiagnosticCases.Cases),MemberType=typeof(DocumentationDiagnosticCases))]
    public void SameCompleteDiagnosticAssertionsBindTheSharedException(DocumentationValidationPhase phase)
        =>DocumentationDiagnosticCases.AssertDiagnostic(phase,result=>new DesignDocumentationValidationException(result).ToString());
}
