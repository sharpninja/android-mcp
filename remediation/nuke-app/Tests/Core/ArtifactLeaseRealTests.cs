using AndroidMcp.BuildTool.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;
[Trait("Gate","Real")]
public sealed class ArtifactLeaseRealTests
{
    [Fact] public Task SameContractAgainstNativeLocks()=>ArtifactLeaseCases.AssertContract(
        new FileArtifactLeaseProvider(Path.Combine(Path.GetTempPath(),"android-mcp-nuke-leases",Guid.NewGuid().ToString("N"))));
}
