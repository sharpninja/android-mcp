using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DesignLegacyReceiptStageRealTests
{
    [Theory,MemberData(nameof(DesignLegacyReceiptStageMockTests.Cases),MemberType=typeof(DesignLegacyReceiptStageMockTests))]
    public Task SameReceiptTimingAndPackageAssertionsBindTheExistingFactory(string scenario)
    {
        var environment=new DesignDocumentationStageEnvironment(scenario,true);
        var services=new RecordingDesignDocumentationStageServices(environment);
        var factory=new DesignCompositionRecipeFactory(services,new ModuleGraphValidator(),services,services,services,
            services,services,services,services,services,new ArtifactReceiptVerifier(new NativeContentHash()),legacyReceipts:environment.LegacyReceipts);
        return DesignDocumentationStageCases.AssertContract(environment,factory);
    }
}
