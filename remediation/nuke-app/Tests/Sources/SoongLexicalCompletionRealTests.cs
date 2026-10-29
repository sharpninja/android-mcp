using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SoongLexicalCompletionRealTests
{
    [Theory, MemberData(nameof(SoongLexicalCompletionContractCases.Cases), MemberType = typeof(SoongLexicalCompletionContractCases))]
    public void SameScalarCasesUseNativeServiceAndBothExistingConcerns(string id)
    {
        var test = SoongLexicalCompletionContractCases.Get(id); SoongContractCases.AssertContract(test, new SoongSourceFacts(new GlobMatcher()));
        foreach (var profile in new[] { SourceExtractionProfile.CurrentCandidate, SourceExtractionProfile.RELEASE011 })
        { using var runtime = new SourcesRuntimeFactory().Create(profile); SoongContractCases.AssertContract(test, runtime.Concerns.Soong); }
    }
}
