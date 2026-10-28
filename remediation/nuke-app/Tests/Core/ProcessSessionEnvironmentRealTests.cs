using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Collection("OwnedProcessEnvironment"),Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-003")]
public sealed class ProcessSessionEnvironmentRealTests
{
    [Theory,InlineData(false),InlineData(true)]
    public Task SameInheritanceClearingAndPublicOverrideCasesBindToOneNativeEngine(bool clear)
    {
        var root=ProcessSessionFixturePaths.NewRoot("process-environment-tests");var request=ProcessRunnerRealTests.Request(root,0,"environment",[ProcessSessionEnvironmentCases.Name]);
        return ProcessSessionEnvironmentCases.AssertContract(clear,new NativeProcessRunner(new SystemClock()),request);
    }
}
