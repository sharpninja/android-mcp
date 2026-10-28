using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Mock"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class AidlLexicalInventoryMockTests
{
    [Theory, MemberData(nameof(AidlLexicalInventoryContractCases.Cases), MemberType = typeof(AidlLexicalInventoryContractCases))]
    public void CorrectMocksPreserveExactSourceAndOriginalLexicalCounts(string id)
    {
        var test = AidlLexicalInventoryContractCases.Get(id);
        var provider = new ScriptedAidlLexicalInventory(_ => test.Correct());
        AidlLexicalInventoryContractCases.AssertContract(test, provider);
        Assert.Same(test.Text, Assert.Single(provider.Received));
    }

    [Theory, MemberData(nameof(AidlLexicalInventoryContractCases.Fields), MemberType = typeof(AidlLexicalInventoryContractCases))]
    public void EachCountFlagAndIssueFieldRejectsAnIndependentWrongMock(string id, string property)
    {
        var test = AidlLexicalInventoryContractCases.Get(id);
        var provider = new ScriptedAidlLexicalInventory(_ => AidlLexicalInventoryContractCases.Wrong(test, property));
        Assert.NotNull(Record.Exception(() => AidlLexicalInventoryContractCases.AssertContract(test, provider)));
    }

    [Theory, MemberData(nameof(AidlLexicalInventoryContractCases.Cases), MemberType = typeof(AidlLexicalInventoryContractCases))]
    public void NullResultsAndProviderFailuresCannotMasqueradeAsEmptyInventory(string id)
    {
        var test = AidlLexicalInventoryContractCases.Get(id);
        Assert.NotNull(Record.Exception(() => AidlLexicalInventoryContractCases.AssertContract(test,
            new ScriptedAidlLexicalInventory(_ => null!))));
        Assert.Throws<IOException>(() => AidlLexicalInventoryContractCases.AssertContract(test,
            new ScriptedAidlLexicalInventory(_ => throw new IOException("scripted provider failure"))));
    }

    [Theory]
    [InlineData("LEX-UNBALANCED")]
    [InlineData("LEX-NESTED-UNBALANCED")]
    public void OriginalUnbalancedReasonCannotBeOmittedChangedOrDuplicated(string id)
    {
        var test = AidlLexicalInventoryContractCases.Get(id);
        foreach (var issues in new[] { Array.Empty<string>(), new[] { "wrong reason" }, test.Issues.Concat(test.Issues).ToArray() })
            Assert.NotNull(Record.Exception(() => AidlLexicalInventoryContractCases.AssertContract(test,
                new ScriptedAidlLexicalInventory(_ => test.Correct() with { Issues = issues }))));
    }

    [Fact]
    public void NullSourceIsAPreconditionFailureWhileEmptySourceHasOriginalOneRecordExpectation()
    {
        var provider = new ScriptedAidlLexicalInventory(text =>
        {
            ArgumentNullException.ThrowIfNull(text);
            return AidlLexicalInventoryContractCases.Get("LEX-EMPTY").Correct();
        });
        AidlLexicalInventoryContractCases.AssertNullPrecondition(provider);
        AidlLexicalInventoryContractCases.AssertContract(AidlLexicalInventoryContractCases.Get("LEX-EMPTY"), provider);
        Assert.Collection(provider.Received, text => Assert.Null(text), text => Assert.Equal("", text));
        Assert.NotNull(Record.Exception(() => AidlLexicalInventoryContractCases.AssertNullPrecondition(
            new ScriptedAidlLexicalInventory(_ => AidlLexicalInventoryContractCases.Get("LEX-EMPTY").Correct()))));
        Assert.NotNull(Record.Exception(() => AidlLexicalInventoryContractCases.AssertNullPrecondition(
            new ScriptedAidlLexicalInventory(_ => throw new FormatException("wrong caller error")))));
    }
}
