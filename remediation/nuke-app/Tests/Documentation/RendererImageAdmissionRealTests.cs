using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class RendererImageAdmissionRealTests
{
    [Theory]
    [InlineData("SUCCESS")][InlineData("FILE-HASH-0")][InlineData("FILE-HASH-1")][InlineData("FILE-HASH-2")][InlineData("FILE-HASH-3")][InlineData("FILE-HASH-4")][InlineData("PROCESS-FAIL")][InlineData("PROCESS-CANCELLED")][InlineData("CAPTURE-MISSING")][InlineData("JAVA-MISMATCH")][InlineData("FONT-MISSING")][InlineData("CANCEL-PREFLIGHT")]
    public Task SameAdmissionCasesBindNativeService(string scenario)
    {
        var e=new RendererAdmissionEnvironment(scenario);
        return RendererAdmissionAssertions.AssertCorrect(e,new NativeRendererImageAdmission(new RecordingAdmissionRunner(e),new RecordingAdmissionCapture(e),new RecordingAdmissionTransport(e),new NativeContentHash()));
    }
}
