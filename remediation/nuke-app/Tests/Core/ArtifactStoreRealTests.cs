using AndroidMcp.BuildTool.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;
[Trait("Gate","Real")]
public sealed class ArtifactStoreRealTests
{
    [Theory,MemberData(nameof(ArtifactStoreCases.Cases),MemberType=typeof(ArtifactStoreCases))]
    public async Task SameContractAgainstFilesystem(ArtifactCase test)
    {
        var path=Path.Combine(Path.GetTempPath(),"android-mcp-nuke-tests",Guid.NewGuid().ToString("N"));
        var store=new FileArtifactStore(path,bytes=>ContentHash.Sha256(bytes.Span));
        await ArtifactStoreCases.AssertContract(test,store);
    }
}
