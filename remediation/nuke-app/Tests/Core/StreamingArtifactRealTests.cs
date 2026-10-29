using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate", "Real"), Trait("Requirement", "TEST-BUILD-007")]
public sealed class StreamingArtifactRealTests
{
    [Theory, MemberData(nameof(StreamingArtifactCases.Cases), MemberType = typeof(StreamingArtifactCases))]
    public Task SameContractAgainstFilesystem(StreamingArtifactCase c)
    {
        var root = Path.Combine(Path.GetTempPath(), "android-mcp-nuke-tests", Guid.NewGuid().ToString("N"));
        return StreamingArtifactCases.AssertContract(c, new FileArtifactStore(root, bytes => ContentHash.Sha256(bytes.Span)));
    }
}

[Trait("Gate", "Real"), Trait("Requirement", "TEST-BUILD-007")]
public sealed class StreamingArtifactFilesystemRealTests
{
    [Theory, MemberData(nameof(StreamingFilesystemCases.Cases), MemberType = typeof(StreamingFilesystemCases))]
    public Task SameContractAgainstFilesystem(string scenario) => StreamingFilesystemCases.AssertContract(scenario, new NativeStreamingFilesystemHarness());
    private sealed class NativeStreamingFilesystemHarness : IStreamingFilesystemHarness
    {
        private readonly string workspace = Path.Combine(Path.GetTempPath(), "android-mcp-nuke-tests", Guid.NewGuid().ToString("N"));
        private string Root => Path.Combine(workspace, "root");
        public IStreamingArtifactStore Store => new FileArtifactStore(Root, bytes => ContentHash.Sha256(bytes.Span));
        public IStreamingArtifactStore Peer => new FileArtifactStore(Root, bytes => ContentHash.Sha256(bytes.Span));
        public void PrepareLink(string kind)
        {
            Directory.CreateDirectory(workspace);
            var outside = Path.Combine(workspace, "retained-outside");
            Directory.CreateDirectory(outside);
            if (kind == "root-link") { Directory.CreateSymbolicLink(Root, outside); return; }
            Directory.CreateDirectory(Root);
            if (kind == "ancestor-link") { Directory.CreateSymbolicLink(Path.Combine(Root, "exports"), outside); return; }
            Directory.CreateDirectory(Path.Combine(Root, "exports"));
            var outsideFile = Path.Combine(outside, kind == "dangling-link" ? "absent.bin" : "original.bin");
            if (kind == "leaf-link") File.WriteAllBytes(outsideFile, StreamingArtifactCases.Abc);
            File.CreateSymbolicLink(Path.Combine(Root, "exports", "result.bin"), outsideFile);
        }
    }
}
