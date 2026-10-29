using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using AndroidMcp.BuildTool.Sources.Java;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
public sealed class SdkAnnotationArchiveRealTests
{
    private static ISdkAnnotationArchiveSourceFacts Provider() => new SdkAnnotationArchiveSourceFacts(new SourceTypeFacts(new JvmSourceLinker(), new CanonicalJson()), new SourceXmlFormatter());
    [Theory, MemberData(nameof(SdkAnnotationArchiveContractCases.Cases), MemberType = typeof(SdkAnnotationArchiveContractCases))]
    public void SameArchiveInputsAndAllEvidenceAssertionsUseNativeProvider(string id) => SdkAnnotationArchiveContractCases.AssertContract(SdkAnnotationArchiveContractCases.Get(id), Provider());
    [Theory, MemberData(nameof(SdkAnnotationArchiveContractCases.Failures), MemberType = typeof(SdkAnnotationArchiveContractCases))]
    public void SameMalformedArchivesUseNativeFailureAssertions(string id, byte[] input)
    { Assert.StartsWith("ANN-Z-invalid-", id); SdkAnnotationArchiveContractCases.AssertFailure(input, Provider()); }
}
