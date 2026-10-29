using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class LosslessEvidenceRealTests
{
    [Theory,MemberData(nameof(LosslessContractCases.Cases),MemberType=typeof(LosslessContractCases))]
    public void SameFactorCasesAndAssertionsBindNativeService(LosslessCase test)=>LosslessContractCases.AssertContract(test,new LosslessEvidenceService(new NativeContentHash()));
    [Theory,MemberData(nameof(ReconstructionContractCases.Cases),MemberType=typeof(ReconstructionContractCases))]
    public void SameReconstructionCasesAndAssertionsBindNativeService(ReconstructionCase test)=>ReconstructionContractCases.AssertContract(test,new LosslessEvidenceService(new NativeContentHash()));
}
