using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class ParserOriginalRealTests
{
    [Theory, MemberData(nameof(ParserOriginalContractCases.Cases), MemberType = typeof(ParserOriginalContractCases))]
    public void ExistingNativeConsumersUseTheSameOriginalFixtureAssertions(string id)
    {
        var test = ParserOriginalContractCases.Get(id);
        ISourceExtractor extractor = test.Source.Kind switch
        {
            "aidl" => new AidlSourceExtractor(bytes => ContentHash.Sha256(bytes.Span)),
            "signature" => new SignatureSourceExtractor(bytes => ContentHash.Sha256(bytes.Span)),
            _ => throw new InvalidOperationException("No native binding for this finite fixture kind")
        };
        ParserOriginalContractCases.AssertContract(test, extractor, new PermissionExpressionSourceFacts());
    }
}
