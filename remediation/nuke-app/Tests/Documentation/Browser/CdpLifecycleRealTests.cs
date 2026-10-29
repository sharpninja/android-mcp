using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation.Browser;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Browser;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-005")]
public sealed class CdpLifecycleRealTests
{
    private static ICdpClient Provider(ControlledCdpWire wire,ICdpLifecycleObserver? observer)
    {var store=new FileArtifactStore(ProcessSessionFixturePaths.NewRoot("cdp-lifecycle-tests"),bytes=>ContentHash.Sha256(bytes.Span));return new CdpClient(wire,store,new SystemClock(),observer);}
    [Fact]
    public Task SameAtomicAdmissionTerminalCase()=>CdpLifecycleCases.TerminalBetweenAdmissionAndRegistration(Provider);
    [Theory,InlineData(false),InlineData(true)]
    public Task SameCancellationAndLateReplyCases(bool wrongSession)=>CdpLifecycleCases.CancellationAndLateReply(Provider,wrongSession);
}
