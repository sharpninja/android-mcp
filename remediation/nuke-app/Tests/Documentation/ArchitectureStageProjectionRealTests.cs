using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class ArchitectureStageProjectionRealTests
{
    [Theory,MemberData(nameof(ArchitectureStageProjectionCases.Cases),MemberType=typeof(ArchitectureStageProjectionCases))]
    public Task NativeCommonEngineBindsTheSameSubsetAndUnionCases(ArchitectureSubsetCase test)
    {
        var e=new ArchitectureSubsetEnvironment(test);
        var provider=new ArchitectureProjectionComposer(e.Runtime,new RecordingPipelineStore(e.Inner),e.Inner.Receipts,
            new RecordingPipelinePreparation(e.Inner),new RecordingPipelineTransport(e.Inner),new RecordingPipelineAdmission(e.Inner),ArchitecturePipelineEnvironment.WorkGeneration);
        return ArchitectureStageProjectionCases.AssertSubset(e,provider);
    }
}
