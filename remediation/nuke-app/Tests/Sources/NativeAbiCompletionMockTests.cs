using System.Text.Json.Nodes;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Mock"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class NativeAbiCompletionMockTests
{
    [Theory, MemberData(nameof(NativeAbiCompletionContractCases.Cases), MemberType = typeof(NativeAbiCompletionContractCases))]
    public void ExactGraphOpaqueFieldAndDiagnosticAssertionsRejectEveryWrongProperty(string id)
    {
        var test = NativeAbiCompletionContractCases.Get(id); var correct = new ScriptedNativeAbi(_ => test.CorrectResult());
        NativeAbiContractCases.AssertContract(test, correct); Assert.Equal(test.Input, Assert.Single(correct.Calls));
        foreach (var row in SourceMutationCases.For(test.MutationCase()))
        {
            var wrong = SourceMutationCases.Apply(test.MutationCase(), (SourceMutation)row[1]).Records[0];
            Assert.NotNull(Record.Exception(() => NativeAbiContractCases.AssertContract(test, new ScriptedNativeAbi(_ => wrong))));
        }
    }
    [Theory, MemberData(nameof(NativeAbiCompletionContractCases.ErrorCases), MemberType = typeof(NativeAbiCompletionContractCases))]
    public void OriginalReachedAppendFailureRejectsAcceptanceWrongExceptionAndWrongOperation(string id)
    {
        var test = NativeAbiCompletionContractCases.GetFailure(id);
        NativeAbiCompletionContractCases.AssertFailure(test, new ScriptedNativeAbi(_ => throw new ArgumentException("Expected ABI nonobject-record bucket array")));
        foreach (var error in new Exception?[] { null, new FormatException("Expected ABI nonobject-record bucket array"), new ArgumentException("wrong operation") })
            Assert.NotNull(Record.Exception(() => NativeAbiCompletionContractCases.AssertFailure(test, new ScriptedNativeAbi(_ => error is null ? new JsonObject() : throw error))));
    }
    [Theory, MemberData(nameof(NativeAbiOriginalContractCases.Cases), MemberType = typeof(NativeAbiOriginalContractCases))]
    public void ExactOriginalMethodAssertionsRejectTheirOwnIndependentNegativeControls(string id)
    {
        var test = NativeAbiOriginalContractCases.Get(id); var correct = new ScriptedNativeAbi(_ => test.Fixture.CorrectResult());
        NativeAbiOriginalContractCases.AssertMethod(test, correct); Assert.Equal(test.Fixture.Input, Assert.Single(correct.Calls));
        foreach (var change in test.NegativeControls)
        {
            var wrong = test.Fixture.CorrectResult(); change(wrong);
            Assert.NotNull(Record.Exception(() => NativeAbiOriginalContractCases.AssertMethod(test, new ScriptedNativeAbi(_ => wrong))));
        }
    }
}
