using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class ArchitectureLegacyReceiptRealTests
{
    [Theory,MemberData(nameof(ArchitectureLegacyReceiptCases.Cases),MemberType=typeof(ArchitectureLegacyReceiptCases))]
    public Task SamePhaseTimingAndPreservationCasesBindTheExistingComposer(ArchitectureLegacyReceiptCase test)
    {
        var e=new ArchitectureLegacyReceiptEnvironment(test);
        var formatter=test.Enabled?new DocumentationLegacyReceipts(new CanonicalJson(),new StrictJsonInspector(),new RecordingLegacyReceiptHash(e)):null;
        var native=new ArchitectureProjectionComposer(e.Subset.Runtime,new RecordingPipelineStore(e.Pipeline),e.Pipeline.Receipts,
            new RecordingPipelinePreparation(e.Pipeline),new RecordingLegacyValidationTransport(e),new RecordingPipelineAdmission(e.Pipeline),
            ArchitecturePipelineEnvironment.WorkGeneration,formatter);
        return ArchitectureLegacyReceiptCases.AssertContract(e,native);
    }
}
