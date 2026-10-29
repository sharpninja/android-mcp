using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

// Production admission/ownership adapter, SAME 20 mock-proven case assertions.
// Calls remain controlled fixtures: no actual open/statx/Windows runtime claim.
[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-007")]
public sealed class ArtifactReadOpenerRealTests
{
    [Theory,MemberData(nameof(ArtifactReadOpenerCases.Cases),MemberType=typeof(ArtifactReadOpenerCases))]
    public Task SameAdmissionAndOwnershipCasesUseProductionAdapter(ReadOpenCase test)=>
        ArtifactReadOpenerCases.AssertCase(test,(calls,identity)=>new ArtifactReadOpener(calls,identity));
}
