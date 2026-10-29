using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate","Real"), Trait("Requirement","TEST-BUILD-002")]
public sealed class NukeExitCodeRealTests
{
    [Theory, MemberData(nameof(NukeExitCodeCases.Cases), MemberType=typeof(NukeExitCodeCases))]
    public void SamePublicExitCasesBindNativePolicy(int engineCode,int expected) =>
        NukeExitCodeCases.AssertCase(engineCode,expected,NukeExitCode.Normalize);
}
