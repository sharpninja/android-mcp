using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationSelectedProjectSourceRealTests
{
    [Theory,MemberData(nameof(DocumentationSelectedProjectSourceCases.Scopes),MemberType=typeof(DocumentationSelectedProjectSourceCases))]
    public void SameTypedScopeAndUnchangedDefaultAssertionsBindNativeCatalog(DocumentationProjectSourceScope scope)
        =>DocumentationSelectedProjectSourceCases.AssertCatalog(scope,new DocumentationProjectSourceCatalog());
    [Fact]
    public void SameUnknownScopeRejectsNativeCatalog()=>Assert.Throws<ArgumentOutOfRangeException>(()=>new DocumentationProjectSourceCatalog().DescribeProjectSources((DocumentationProjectSourceScope)999));
    [Theory,MemberData(nameof(DocumentationSelectedProjectSourceCases.PlanCases),MemberType=typeof(DocumentationSelectedProjectSourceCases))]
    public void SameSelectedPlanAndOriginalPinGuardsBindNativePlanner(DocumentationProjectSourceScope scope,DocumentationSourcePlanCase test)
        =>DocumentationSelectedProjectSourceCases.AssertPlan(scope,test,new DocumentationModelSourcePlanner(new StrictJsonInspector(),scope));
}
