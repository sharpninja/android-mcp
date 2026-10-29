using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate", "Real"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class ArtifactDirectoryRealTests
{
    [Theory, MemberData(nameof(ArtifactDirectoryContractCases.Cases), MemberType = typeof(ArtifactDirectoryContractCases))]
    public async Task SameDirectoryCasesUseExistingNativeStore(string id)
    {
        var test = ArtifactDirectoryContractCases.Get(id);
        var basis = Path.Combine(Path.GetTempPath(), "android-mcp-artifact-directory", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(basis); var root = Path.Combine(basis, "root");
        if (test.Before.Links.Contains("$root"))
        {
            var target = Path.Combine(basis, "target"); Directory.CreateDirectory(target);
            Directory.CreateSymbolicLink(root, target);
        }
        else if (test.Before.Links.Contains("$ancestor"))
        {
            var target = Path.Combine(basis, "target"); Directory.CreateDirectory(Path.Combine(target, "root"));
            var ancestor = Path.Combine(basis, "link"); Directory.CreateSymbolicLink(ancestor, target);
            root = Path.Combine(ancestor, "root");
        }
        else if (id != "DIR-01") Directory.CreateDirectory(root);
        foreach (var directory in test.Before.Directories) Directory.CreateDirectory(Path.Combine(root, directory));
        foreach (var file in test.Before.Files)
        {
            var path = Path.Combine(root, file.Key); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, file.Value);
        }
        foreach (var link in test.Before.Links.Where(x => !x.StartsWith('$')))
        {
            var target = Path.Combine(basis, "target"); Directory.CreateDirectory(target);
            Directory.CreateSymbolicLink(Path.Combine(root, link), target);
        }
        var hashes = 0;
        var store = new FileArtifactStore(root, _ => { hashes++; throw new InvalidOperationException("Directory operation must not hash bytes"); });
        await using var lease = await new FileArtifactLeaseProvider(Path.Combine(basis, "leases")).AcquireAsync("directory-fixture");
        await ArtifactDirectoryContractCases.AssertContract(test, new(store, Observe, () => hashes));

        ArtifactDirectoryState Observe()
        {
            var directories = new List<string>(); var files = new Dictionary<string, string>(); var links = new List<string>();
            if (test.Before.Links.Contains("$root") && (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) links.Add("$root");
            if (test.Before.Links.Contains("$ancestor") && (File.GetAttributes(Path.GetDirectoryName(root)!) & FileAttributes.ReparsePoint) != 0) links.Add("$ancestor");
            if (Directory.Exists(root)) Walk(root);
            return new(directories.ToArray(), files, links.ToArray());
            void Walk(string directory)
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    var relative = Path.GetRelativePath(root, entry).Replace(Path.DirectorySeparatorChar, '/');
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) { links.Add(relative); continue; }
                    if ((attributes & FileAttributes.Directory) != 0) { directories.Add(relative); Walk(entry); }
                    else files.Add(relative, File.ReadAllText(entry));
                }
            }
        }
    }
}
