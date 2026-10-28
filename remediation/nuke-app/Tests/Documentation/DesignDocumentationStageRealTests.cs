using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DesignDocumentationStageRealTests
{
    [Theory,MemberData(nameof(DesignDocumentationStageCases.Cases),MemberType=typeof(DesignDocumentationStageCases))]
    public Task SamePublishedUnionAndStageCasesBindNativeFactory(string scenario)
    {
        var e=new DesignDocumentationStageEnvironment(scenario);var services=new RecordingDesignDocumentationStageServices(e);
        return DesignDocumentationStageCases.AssertContract(e,new DesignCompositionRecipeFactory(services,new ModuleGraphValidator(),services,services,services,
            services,services,services,services,services,new ArtifactReceiptVerifier(new NativeContentHash())));
    }
}
