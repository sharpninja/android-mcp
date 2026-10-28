using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationAssetRealTests
{
    [Theory,MemberData(nameof(DocumentationAssetCases.Cases),MemberType=typeof(DocumentationAssetCases))]
    public async Task SameCasesBindNativeOrchestrationWithRecordingIo(AssetCase test)
    {
        var environment=new AssetTestEnvironment(test);
        var destination=test.Scenario==AssetScenario.SameStore?environment.Source:environment.Target;
        var provider=new DocumentationAssetOperation(environment.Source,destination,environment.Leases,environment.Projector,environment.Hashes,AssetTestEnvironment.RootIdentity);
        await DocumentationAssetCases.AssertContract(test,environment,provider);
    }
}
