using AndroidMcp.BuildTool.Execution;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate","Real")][Trait("Requirement","TEST-BUILD-004")]
public sealed class SourcesSeedOperationRealTests
{
    [Theory][MemberData(nameof(SourcesSeedOperationMockTests.Modes),MemberType=typeof(SourcesSeedOperationMockTests))]
    public async Task SameOnePassSeedCapabilityAssertions(string mode)
    {
        using var fixture=new SourcesSeedOperationFixture(mode);
        await fixture.AssertContract(new SourcesSeedOperation(new SeedOperationPlannerProbe(fixture),new SeedOperationEngineProbe(fixture)));
    }
}
