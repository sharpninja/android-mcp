using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class ArchitecturePipelineRealTests
{
    [Theory]
    [InlineData("SUCCESS")][InlineData("SOURCE-WRITE-FAIL")][InlineData("ADMISSION-FAIL")][InlineData("FAIL-RenderCode")][InlineData("MISSING-CODE-SVG")][InlineData("FAIL-ValidateWorkspace")][InlineData("FAIL-ExportWorkspace")][InlineData("MISSING-EXPORT")][InlineData("FAIL-RenderNative")][InlineData("MISSING-NATIVE-SVG")][InlineData("WORK-COLLISION")][InlineData("WORK-COMMIT-FAIL")]
    public Task SamePipelineCasesBindNativeComposer(string scenario)
    {
        var e=new ArchitecturePipelineEnvironment(scenario);
        var provider=new ArchitectureProjectionComposer(new RecordingPipelineRuntime(e),new RecordingPipelineStore(e),e.Receipts,new RecordingPipelinePreparation(e),new RecordingPipelineTransport(e),new RecordingPipelineAdmission(e),ArchitecturePipelineEnvironment.WorkGeneration);
        return ArchitecturePipelineAssertions.AssertCorrect(e,provider);
    }
}
