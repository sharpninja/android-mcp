using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate","Real")][Trait("Requirement","TEST-BUILD-004")]
public sealed class SourceWorkflowInputsRealTests
{
    [Theory][InlineData("normal")][InlineData("options")][InlineData("recipe-failure")][InlineData("lock-failure")][InlineData("cancelled")]
    public async Task SamePinnedFileAndOptionAssertions(string mode)
    {
        using var fixture=new WorkflowInputsFixture(mode);
        await fixture.AssertContract(new SourceWorkflowInputsReader(fixture));
    }
}
