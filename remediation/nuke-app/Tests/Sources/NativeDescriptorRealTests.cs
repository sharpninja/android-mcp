using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class NativeDescriptorRealTests
{
    [Theory, MemberData(nameof(NativeDescriptorContractCases.Cases), MemberType = typeof(NativeDescriptorContractCases))]
    public void SameOriginalAndBoundaryCasesUseNativeProducer(string id) =>
        NativeDescriptorContractCases.AssertContract(NativeDescriptorContractCases.Get(id), new NativeDescriptorSourceFacts());

    [Theory, InlineData(""), InlineData("c++"), InlineData("CPP"), InlineData("java")]
    public void SameLanguageFailuresUseNativeProducer(string language) =>
        NativeDescriptorContractCases.AssertInvalid(language, new NativeDescriptorSourceFacts());

    [Theory, InlineData("release011"), InlineData("current-candidate")]
    public void SameCapabilityCaseUsesSharedRuntimeComposition(string profile)
    {
        var selected = profile == "release011" ? SourceExtractionProfile.RELEASE011 : SourceExtractionProfile.CurrentCandidate;
        using var runtime = new SourcesRuntimeFactory().Create(selected);
        NativeDescriptorContractCases.AssertCapability(() => runtime.Concerns.NativeDescriptors);
    }
}
