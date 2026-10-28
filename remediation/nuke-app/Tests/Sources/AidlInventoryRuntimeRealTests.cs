using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class AidlInventoryRuntimeRealTests
{
    [Theory, MemberData(nameof(AidlInventoryRuntimeContractCases.Cases), MemberType = typeof(AidlInventoryRuntimeContractCases))]
    public void SameFiniteCasesUseOneConcernFromTheActualFactory(string profile, string id) =>
        AidlInventoryRuntimeContractCases.AssertComposition(profile, id, new SourcesRuntimeFactory());

    [Fact]
    public void SameExistingRecordConstructorAndDeconstructionArePreserved() =>
        AidlInventoryRuntimeContractCases.AssertLegacyRecordShape();
}
