using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate", "Real"), Trait("Requirement", "TEST-BUILD-007")]
public sealed class CreateNewArtifactRealTests
{
    [Theory, MemberData(nameof(CreateNewArtifactCases.Cases), MemberType = typeof(CreateNewArtifactCases))]
    public Task SameContractAgainstFilesystem(CreateNewCase c) =>
        CreateNewArtifactCases.AssertContract(c, new NativeCreateNewHarness());

    private sealed class NativeCreateNewHarness : ICreateNewHarness
    {
        private readonly string workspace = Path.Combine(Path.GetTempPath(), "android-mcp-nuke-tests", Guid.NewGuid().ToString("N"));
        private string Root => Path.Combine(workspace, "root");
        private string Fixture => Path.Combine(workspace, "outside", "original.bin");
        private string mode = "";
        private CancellationTokenSource? cancellation;
        public ICreateNewArtifactStore Store => new FileArtifactStore(Root, Hash);
        public ICreateNewArtifactStore Peer => new FileArtifactStore(Root, Hash);
        public void Configure(string value, CancellationTokenSource tokenSource)
        { mode = value; cancellation = tokenSource; }
        private string Hash(ReadOnlyMemory<byte> bytes)
        {
            if (mode == "hash-failure") throw new InvalidOperationException("hash-fixture");
            if (mode == "cancel-hash") cancellation!.Cancel();
            return ContentHash.Sha256(bytes.Span);
        }
        public bool Exists(string path) => File.Exists(Path.Combine(Root, path)) || Directory.Exists(Path.Combine(Root, path));
        public byte[] ReadFixture() => File.ReadAllBytes(Fixture);
        public void PrepareFilesystem(string kind)
        {
            var outside = Path.GetDirectoryName(Fixture)!;
            Directory.CreateDirectory(outside);
            File.WriteAllBytes(Fixture, CreateNewArtifactCases.Abc);
            if (kind == "root-link") { Directory.CreateSymbolicLink(Root, outside); return; }
            Directory.CreateDirectory(Root);
            if (kind == "directory") { Directory.CreateDirectory(Path.Combine(Root, "challenge.bin")); return; }
            if (kind == "ancestor-link") { Directory.CreateSymbolicLink(Path.Combine(Root, "nested"), outside); return; }
            File.CreateSymbolicLink(Path.Combine(Root, "challenge.bin"),
                kind == "dangling-link" ? Path.Combine(outside, "absent.bin") : Fixture);
        }
    }
}
