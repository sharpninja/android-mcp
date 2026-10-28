using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class ParserRemainingRealTests
{
    [Theory, MemberData(nameof(ParserRemainingContractCases.Cases), MemberType = typeof(ParserRemainingContractCases))]
    public void SameThreeOriginalInputsUseTheExistingAidlExtractor(string id) =>
        ParserRemainingContractCases.AssertContract(ParserRemainingContractCases.Get(id),
            new AidlSourceExtractor(bytes => ContentHash.Sha256(bytes.Span)));
}
