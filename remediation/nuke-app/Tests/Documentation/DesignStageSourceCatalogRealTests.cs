using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DesignStageSourceCatalogRealTests
{
    [Theory,MemberData(nameof(DesignStageSourceCases.Cases),MemberType=typeof(DesignStageSourceCases))]
    public void SameExactAliasUnionAndAdmissionCasesBindExistingCatalogOverload(DesignStageSourceCase test)=>
        DesignStageSourceCases.AssertSelection(test,new DocumentationProjectSourceCatalog().DescribeProjectSources);
}
