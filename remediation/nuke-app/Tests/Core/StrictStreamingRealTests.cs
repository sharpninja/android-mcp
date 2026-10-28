using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-007")]
public sealed class StrictStreamingRealTests
{
    [Theory,MemberData(nameof(StrictStreamingCases.Cases),MemberType=typeof(StrictStreamingCases))]
    public Task SameNineCasesBindSharedFilesystemEngine(StrictStreamingCase c)=>
        StrictStreamingCases.AssertCase(c,new NativeStrictStreamingHarness());
    private sealed class NativeStrictStreamingHarness : IStrictStreamingHarness
    {
        private readonly string root=Path.Combine(Path.GetTempPath(),"android-mcp-nuke-tests",Guid.NewGuid().ToString("N"));
        public ICreateNewStreamingArtifactStore Store=>new FileArtifactStore(root,bytes=>ContentHash.Sha256(bytes.Span));
        public ICreateNewStreamingArtifactStore Peer=>new FileArtifactStore(root,bytes=>ContentHash.Sha256(bytes.Span));
        public bool Exists(string path)=>File.Exists(Path.Combine(root,path));
    }
}
