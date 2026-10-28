using System.Text;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-002")]
public sealed class ArtifactRootBoundaryRealTests
{
    [Theory, MemberData(nameof(ArtifactRootBoundaryCases.Data), MemberType = typeof(ArtifactRootBoundaryCases))]
    public async Task SameRootTopologyAssertionsBindNativeStoreAndLease(string id)
    {
        var test = ArtifactRootBoundaryCases.Get(id);
        using var fixture = new RootTopology(test.Id);
        var store = new FileArtifactStore(fixture.Root, bytes => ContentHash.Sha256(bytes.Span));
        var leases = new FileArtifactLeaseProvider(fixture.LeaseRoot);
        await ArtifactRootBoundaryCases.AssertContract(test, new(store, leases, fixture.OutsideSnapshot));
    }

    private sealed class RootTopology : IDisposable
    {
        private readonly string owned = Path.Combine(Path.GetTempPath(), "android-mcp-root-boundary-tests", Guid.NewGuid().ToString("N"));
        private readonly string outside;
        public string Root { get; }
        public string LeaseRoot { get; }
        public string MissingTarget { get; }
        public RootTopology(string topology)
        {
            var host = Path.Combine(owned, "host");
            outside = Path.Combine(owned, "outside");
            Directory.CreateDirectory(host);
            Directory.CreateDirectory(outside);
            File.WriteAllBytes(Path.Combine(outside, "sentinel.bin"), Encoding.UTF8.GetBytes("original sentinel"));
            MissingTarget = Path.Combine(outside, "uncreated-target");
            var alias = Path.Combine(host, "alias");
            switch (topology)
            {
                case "ordinary-existing-root":
                    Root = Path.Combine(host, "bound");
                    Directory.CreateDirectory(Root);
                    break;
                case "ordinary-new-root":
                    Root = Path.Combine(host, "new", "bound");
                    break;
                case "root-directory-link":
                    Directory.CreateSymbolicLink(alias, outside);
                    Root = alias;
                    break;
                case "ancestor-link-existing-bound-root":
                    Directory.CreateDirectory(Path.Combine(outside, "existing", "bound"));
                    Directory.CreateSymbolicLink(alias, outside);
                    Root = Path.Combine(alias, "existing", "bound");
                    break;
                case "ancestor-link-new-bound-root":
                    Directory.CreateSymbolicLink(alias, outside);
                    Root = Path.Combine(alias, "new", "bound");
                    break;
                case "dangling-root-link":
                    Directory.CreateSymbolicLink(alias, MissingTarget);
                    Root = alias;
                    break;
                case "artifact-child-link":
                    Root = Path.Combine(host, "bound");
                    Directory.CreateDirectory(Root);
                    Directory.CreateSymbolicLink(Path.Combine(Root, "alias"), outside);
                    break;
                case "multiple-ancestors-above-bound-root":
                    Directory.CreateDirectory(Path.Combine(outside, "one", "two", "three"));
                    Directory.CreateSymbolicLink(alias, outside);
                    Root = Path.Combine(alias, "one", "two", "three");
                    break;
                default: throw new ArgumentException("Unknown fixture topology", nameof(topology));
            }
            LeaseRoot = Path.Combine(Root, topology == "artifact-child-link" ? "alias" : "leases");
        }
        public string[] OutsideSnapshot() => Directory.EnumerateFileSystemEntries(outside, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).Select(path => Path.GetRelativePath(outside, path) + "|" +
                (Directory.Exists(path) ? "directory" : Convert.ToBase64String(File.ReadAllBytes(path)))).ToArray();
        public void Dispose() { if (Directory.Exists(owned)) Directory.Delete(owned, true); }
    }
}
