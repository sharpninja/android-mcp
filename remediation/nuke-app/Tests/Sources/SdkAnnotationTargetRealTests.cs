using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using AndroidMcp.BuildTool.Sources.Java;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SdkAnnotationTargetRealTests
{
    [Theory, MemberData(nameof(SdkAnnotationTargetContractCases.Cases), MemberType = typeof(SdkAnnotationTargetContractCases))]
    public void SameTargetInputsAndAssertionsUseTheSingleNativeTypeFactsService(string id) =>
        SdkAnnotationTargetContractCases.AssertContract(SdkAnnotationTargetContractCases.Get(id), new SourceTypeFacts(new JvmSourceLinker(), new CanonicalJson()));
}
