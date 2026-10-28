using AndroidMcp.BuildTool.Execution;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate","Real")][Trait("Requirement","TEST-BUILD-004")]
public sealed class SourcesExtractWorkflowRealTests
{
    [Theory][MemberData(nameof(SourcesExtractWorkflowMockTests.Modes),MemberType=typeof(SourcesExtractWorkflowMockTests))]
    public async Task SameCompositionAndFailBeforeEffectAssertions(string mode)
    {
        using var fixture=new ExtractWorkflowFixture(mode);
        await fixture.AssertContract(new SourcesExtractWorkflowOperation(new WorkflowPlannerProbe(fixture),
            new WorkflowInventoryProbe(fixture),new WorkflowIdentityProbe(fixture),new WorkflowExtractProbe(fixture),fixture.Resolve));
    }
}
