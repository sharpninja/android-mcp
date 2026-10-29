using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class AidlLexicalInventoryRealTests
{
    [Theory, MemberData(nameof(AidlLexicalInventoryContractCases.Cases), MemberType = typeof(AidlLexicalInventoryContractCases))]
    public void SameElevenCasesUseTheSharedNativeLexicalInventory(string id) =>
        AidlLexicalInventoryContractCases.AssertContract(AidlLexicalInventoryContractCases.Get(id),
            new AidlLexicalInventorySourceFacts());

    [Fact]
    public void SameNullCallerPreconditionUsesTheNativeService() =>
        AidlLexicalInventoryContractCases.AssertNullPrecondition(new AidlLexicalInventorySourceFacts());
}
