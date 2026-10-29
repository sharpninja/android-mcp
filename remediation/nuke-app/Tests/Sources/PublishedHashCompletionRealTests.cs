using AndroidMcp.BuildTool.Sources;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class PublishedHashCompletionRealTests
{
    [Theory, MemberData(nameof(PublishedHashCompletionContractCases.Cases), MemberType = typeof(PublishedHashCompletionContractCases))]
    public void SameFullOutputsBindOneNativeService(string id)
    {
        var test = PublishedHashCompletionContractCases.Get(id);
        PublishedHashCompletionContractCases.AssertCase(test, new PublishedInterfaceHashSourceFacts());
        foreach (var profile in new[] { SourceExtractionProfile.CurrentCandidate, SourceExtractionProfile.RELEASE011 })
        {
            using var runtime = new SourcesRuntimeFactory().Create(profile);
            PublishedHashCompletionContractCases.AssertCase(test, runtime.Concerns.PublishedHashes);
        }
    }
    [Theory, MemberData(nameof(PublishedHashCompletionContractCases.OriginalMethods), MemberType = typeof(PublishedHashCompletionContractCases))]
    public void SameExactOriginalFourMethodsBindNative(int method) => PublishedHashCompletionContractCases.AssertOriginal(method, new PublishedInterfaceHashSourceFacts());
}
