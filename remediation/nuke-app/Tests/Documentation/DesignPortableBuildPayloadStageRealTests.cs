using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DesignPortableBuildPayloadStageRealTests
{
    [Theory,MemberData(nameof(DesignPortableBuildPayloadStageMockTests.Cases),MemberType=typeof(DesignPortableBuildPayloadStageMockTests))]
    public Task SamePackageCoverageAndFinalSourceHookAssertionsBindNativeFactory(string scenario)
    {
        var e=new DesignDocumentationStageEnvironment(scenario,true,true);var services=new RecordingDesignDocumentationStageServices(e);
        var factory=new DesignCompositionRecipeFactory(services,new ModuleGraphValidator(),services,services,services,
            services,services,services,services,services,new ArtifactReceiptVerifier(new NativeContentHash()),legacyReceipts:e.LegacyReceipts,buildPayload:e.BuildPayload);
        return DesignDocumentationStageCases.AssertContract(e,factory);
    }
}
