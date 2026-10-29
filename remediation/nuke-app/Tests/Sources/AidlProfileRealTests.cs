using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class AidlProfileRealTests
{
    private static AidlSourceExtractor Extractor() => new(bytes => ContentHash.Sha256(bytes.Span));
    [Theory, MemberData(nameof(AidlProfileContractCases.Cases), MemberType = typeof(AidlProfileContractCases))]
    public void SameExactProfileCasesBindOneNativeExtractorAndSoleDispatcher(string id)
    {
        var test = AidlProfileContractCases.Get(id); var extractor = Extractor();
        AidlProfileContractCases.AssertContract(test, extractor.Extract, false);
        AidlProfileContractCases.AssertContract(test, (source, profile) => new SourcesExtractionDispatcher([extractor], profile).Extract(source, "aidl"), true);
        using var runtime = new SourcesRuntimeFactory().Create(test.Profile);
        AidlProfileContractCases.AssertContract(test, (source, _) => runtime.Dispatcher.Extract(source, "aidl"), true);
    }
    [Theory, MemberData(nameof(AidlProfileContractCases.CurrentCases), MemberType = typeof(AidlProfileContractCases))]
    public void ExistingDefaultCurrentApiAndAllUnrelatedProfileOutputRemainUnchanged(string id)
    {
        var test = AidlProfileContractCases.Get(id); var extractor = Extractor();
        AidlProfileContractCases.AssertContract(test, (source, _) => extractor.Extract(source), false);
        AidlProfileContractCases.AssertOnlyProfileDelta(test, extractor.Extract);
        AidlProfileContractCases.AssertOnlyProfileDelta(test, (source, profile) => new SourcesExtractionDispatcher([extractor], profile).Extract(source, "aidl"));
    }
}
