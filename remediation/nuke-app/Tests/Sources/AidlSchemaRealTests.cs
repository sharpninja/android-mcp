using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
public sealed class AidlSchemaRealTests
{
    [Theory, MemberData(nameof(AidlSchemaContractCases.Cases), MemberType = typeof(AidlSchemaContractCases))]
    public void SameRawSchemaAndLocatorCasesUseNativeExtractor(string id) => AidlSchemaContractCases.AssertContract(
        AidlSchemaContractCases.Get(id), new AidlSourceExtractor(bytes => ContentHash.Sha256(bytes.Span)));
}
