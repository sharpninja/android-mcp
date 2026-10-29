using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class NativeAbiCompletionRealTests
{
    [Theory, MemberData(nameof(NativeAbiCompletionContractCases.Cases), MemberType = typeof(NativeAbiCompletionContractCases))]
    public void SameCompleteGraphOpaqueAndDiagnosticCasesUseExistingNativeProducer(string id) =>
        NativeAbiContractCases.AssertContract(NativeAbiCompletionContractCases.Get(id), new NativeAbiSourceFacts());
    [Theory, MemberData(nameof(NativeAbiCompletionContractCases.ErrorCases), MemberType = typeof(NativeAbiCompletionContractCases))]
    public void SameOriginalReachedAppendErrorsUseExistingNativeProducer(string id) =>
        NativeAbiCompletionContractCases.AssertFailure(NativeAbiCompletionContractCases.GetFailure(id), new NativeAbiSourceFacts());
    [Theory, MemberData(nameof(NativeAbiOriginalContractCases.Cases), MemberType = typeof(NativeAbiOriginalContractCases))]
    public void SameExactOriginalMethodBodiesBindNativeAbiParser(string id) =>
        NativeAbiOriginalContractCases.AssertMethod(NativeAbiOriginalContractCases.Get(id), new NativeAbiSourceFacts());
    [Theory, InlineData("release-011"), InlineData("current-candidate")]
    public void SameBoundariesAndOriginalMethodsUseExistingProfileConcern(string profile)
    {
        using var runtime = new SourcesRuntimeFactory().Create(SourceExtractionProfile.Resolve(profile)); var abi = runtime.Concerns.NativeAbi;
        foreach (var test in NativeAbiCompletionContractCases.All) NativeAbiContractCases.AssertContract(test, abi);
        foreach (var test in NativeAbiCompletionContractCases.Failures) NativeAbiCompletionContractCases.AssertFailure(test, abi);
        foreach (var test in NativeAbiOriginalContractCases.All) NativeAbiOriginalContractCases.AssertMethod(test, abi);
    }
}
