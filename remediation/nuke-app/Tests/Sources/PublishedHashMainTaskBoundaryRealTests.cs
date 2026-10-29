using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class NativeHashTaskBoundaryProbe : IHashTaskBoundaryProbe
{
    public async Task<HashTaskBoundaryObservation> RunAsync(string task)
    {
        var root = Path.Combine(Path.GetTempPath(), "hash-task-boundary", Guid.NewGuid().ToString("N")); var runtime = Path.Combine(root, "runtime"); var project = Path.Combine(root, "inputs");
        Directory.CreateDirectory(Path.Combine(runtime, ".nuke")); Directory.CreateDirectory(project);
        var original = Path.Combine(project, "retained.txt"); await File.WriteAllTextAsync(original, "retained original");
        var host = Environment.ProcessPath!; var pin = new StoredArtifact("workflows/never-read.json", new string('0', 64), 1, "application/json");
        var args = PublishedHashMainCliContractCases.Arguments(runtime, project, pin, task).ToList(); args[1] = typeof(Build).Assembly.Location; args[args.IndexOf("--dotnet-host") + 1] = host;
        args.InsertRange(1, ["--runtimeconfig", Path.Combine(AppContext.BaseDirectory, "AndroidMcp.Build.Tests.runtimeconfig.json"), "--depsfile", Path.Combine(AppContext.BaseDirectory, "AndroidMcp.Build.Tests.deps.json")]);
        var tool = new ToolIdentity(host, ContentHash.Sha256(await File.ReadAllBytesAsync(host)), Environment.Version.ToString());
        var result = await new NativeProcessRunner(new SystemClock()).RunAsync(new(tool, args, runtime, new Dictionary<string, string>(), Path.Combine(root, "process")));
        var text = await File.ReadAllTextAsync(result.StandardOutputPath) + await File.ReadAllTextAsync(result.StandardErrorPath);
        return new(result.ExitCode, text.Contains("Unknown source extraction task", StringComparison.Ordinal) || text.Contains("SourceExtractTask", StringComparison.Ordinal),
            !text.Contains("never-read.json", StringComparison.Ordinal), Directory.EnumerateFiles(project, "*", SearchOption.AllDirectories).Count() == 1,
            await File.ReadAllTextAsync(original) == "retained original");
    }
}
[Trait("Gate", "Real"), Trait("Requirement", "TEST-BUILD-004")]
public sealed class PublishedHashMainTaskBoundaryRealTests
{
    [Theory, MemberData(nameof(PublishedHashMainTaskBoundaryContract.Cases), MemberType = typeof(PublishedHashMainTaskBoundaryContract))]
    public Task SameNumericTaskRejectionBeforeWorkflowReadUsesCompiledCli(string task) => PublishedHashMainTaskBoundaryContract.AssertCase(task, new NativeHashTaskBoundaryProbe());
}
