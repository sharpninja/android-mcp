using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

// SAME 30 mock-proven requests and ownership assertions, now through the
// production portable-intent adapter. The kernel remains controlled: this
// proves no actual Linux/Windows syscalls, ABI extraction or FIFO runtime.
[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-007")]
public sealed class NativeArtifactCallsRealTests
{
    [Theory,MemberData(nameof(NativeArtifactCallCases.Cases),MemberType=typeof(NativeArtifactCallCases))]
    public void SameKernelIntentAndOwnershipCasesUseProductionAdapter(NativeArtifactCallCase test)=>
        NativeArtifactCallCases.AssertCase(test,kernel=>{var calls=new NativeArtifactCalls(kernel);return(calls,calls);});
}
