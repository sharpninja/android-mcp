using System.Security.Cryptography;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SourceExtractionRealTests
{
    [Theory, MemberData(nameof(SourceExtractionContractCases.Cases), MemberType = typeof(SourceExtractionContractCases))]
    public void RealProviderPassesUnchangedMockGateAssertions(string id)
    {
        var test = SourceExtractionContractCases.Get(id);
        ISourceExtractor provider = test.Kind switch
        {
            "aidl" => new AidlSourceExtractor(Hash),
            "signature" => new SignatureSourceExtractor(Hash),
            _ => throw new InvalidOperationException("No real provider for " + test.Kind)
        };
        SourceExtractionContractCases.AssertContract(test, provider);
    }

    // Test binding only; production receives the parent's canonical content hash
    // service, avoiding a second hashing implementation in source modules.
    private static string Hash(ReadOnlyMemory<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes.Span));
}
