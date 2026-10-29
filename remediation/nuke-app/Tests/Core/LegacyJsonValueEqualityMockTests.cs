using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate", "Mock"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class LegacyJsonValueEqualityMockTests
{
    [Theory, MemberData(nameof(LegacyJsonValueEqualityContractCases.Cases), MemberType = typeof(LegacyJsonValueEqualityContractCases))]
    public void CorrectAndIncorrectMocksUseSameNumericAndStructuralAssertions(string id)
    {
        var test = LegacyJsonValueEqualityContractCases.Get(id); var correct = new ScriptedLegacyJsonEquality(test.Expected);
        LegacyJsonValueEqualityContractCases.AssertContract(test, correct);
        Assert.Equal(2, correct.Calls.Count);
        Assert.Equal((test.Left, test.Right), correct.Calls[0]); Assert.Equal((test.Right, test.Left), correct.Calls[1]);
        Assert.NotNull(Record.Exception(() => LegacyJsonValueEqualityContractCases.AssertContract(test, new ScriptedLegacyJsonEquality(!test.Expected))));
    }
}
