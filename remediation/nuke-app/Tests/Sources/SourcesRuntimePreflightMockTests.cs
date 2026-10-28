using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;
using Xunit.Sdk;

namespace AndroidMcp.BuildTool.Tests.Sources;

public static class SourcesRuntimePreflightContractCases
{
    public static void AssertMissingJavaLoader(ISourcesRuntimeFactory factory)
    {
        var failure = Assert.Throws<InvalidOperationException>(() => factory.Create(SourceExtractionProfile.RELEASE011,
            new SourceJavaAdmission("fixtures/explicit-native-source-lock.json")));
        Assert.Contains("Java admission loader", failure.Message, StringComparison.Ordinal);
    }
}

[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
public sealed class SourcesRuntimePreflightMockTests
{
    [Fact] public void CorrectMissingLoaderMockExercisesAssertion() => SourcesRuntimePreflightContractCases.AssertMissingJavaLoader(new ScriptedFactory("correct"));
    [Theory, InlineData("fallback"), InlineData("wrong-type"), InlineData("wrong-reason")]
    public void IndividuallyWrongMissingLoaderMocksAreRejected(string fault) => Assert.ThrowsAny<XunitException>(() =>
        SourcesRuntimePreflightContractCases.AssertMissingJavaLoader(new ScriptedFactory(fault)));
    private sealed class ScriptedFactory(string behavior) : ISourcesRuntimeFactory
    {
        public ISourcesRuntime Create(SourceExtractionProfile profile, SourceJavaAdmission? java = null) => behavior switch
        {
            "correct" => throw new InvalidOperationException("Java admission loader is required for a selected native manifest"),
            "wrong-type" => throw new FormatException("Java admission loader missing"),
            "wrong-reason" => throw new InvalidOperationException("unrelated failure"),
            _ => null! // Deliberately incorrect silent admission/fallback response.
        };
    }
}
