using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationBootstrapReportRealTests
{
    [Theory,MemberData(nameof(BootstrapReportCases.Cases),MemberType=typeof(BootstrapReportCases))]
    public void SameOriginalRowsUtcRawLockAndSummaryCasesBindNativeProjection(string scenario)
    {var e=new BootstrapReportEnvironment(scenario);BootstrapReportCases.AssertContract(e,new DocumentationBootstrapReportProjector(e,new ArtifactReceiptVerifier(new NativeContentHash())));}
}
