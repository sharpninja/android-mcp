using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class NativeHashMainCliProbe : IHashMainCliProbe
{
    public async Task<HashMainCliObservation> RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "hash-main-compiled", Guid.NewGuid().ToString("N"));
        var project = Path.Combine(root, "retained project"); var outputRoot = Path.Combine(root, "new evidence"); var runtime = Path.Combine(root, "isolated runtime");
        Directory.CreateDirectory(outputRoot); Directory.CreateDirectory(Path.Combine(runtime, ".nuke"));
        var f = HashMainFixture.Create(PublishedHashMainContractCases.All.Single(test => test.Variant == "aidl"));
        var inputs = new FileArtifactStore(project, bytes => ContentHash.Sha256(bytes.Span));
        foreach (var pair in f.Inputs) await inputs.WriteAsync(new(pair.Key, pair.Value, "application/octet-stream"));
        var wire = new PublishedInterfaceHashWorkflowFile(f.Request.Inventory, outputRoot, HashMainFixture.Generation);
        var canonical = new CanonicalJson(); var outer = await inputs.WriteAsync(new("workflows/hash.json", canonical.Serialize(JsonSerializer.SerializeToElement(wire)), "application/json"));
        var protectedInputs = Directory.EnumerateFiles(project, "*", SearchOption.AllDirectories).ToDictionary(path => path, path => ContentHash.Sha256(File.ReadAllBytes(path)));
        var host = Environment.ProcessPath ?? throw new InvalidOperationException("Native host absent"); Assert.Equal("dotnet", Path.GetFileNameWithoutExtension(host));
        var tool = new ToolIdentity(host, ContentHash.Sha256(await File.ReadAllBytesAsync(host)), Environment.Version.ToString());
        var runner = new NativeProcessRunner(new SystemClock()); var arguments = new List<IReadOnlyList<string>>(); var exits = new List<int>();
        async Task<string> Invoke(StoredArtifact pin, string task = "InterfaceHashes")
        {
            var args = PublishedHashMainCliContractCases.Arguments(runtime, project, pin, task).ToList(); args[1] = typeof(Build).Assembly.Location;
            args[args.IndexOf("--dotnet-host") + 1] = host;
            args.InsertRange(1, ["--runtimeconfig", Path.Combine(AppContext.BaseDirectory, "AndroidMcp.Build.Tests.runtimeconfig.json"), "--depsfile", Path.Combine(AppContext.BaseDirectory, "AndroidMcp.Build.Tests.deps.json")]);
            arguments.Add(args.ToArray());
            var process = await runner.RunAsync(new(tool, args, runtime, new Dictionary<string, string>(), Path.Combine(root, "process-" + arguments.Count)));
            exits.Add(process.ExitCode); var stdout = await File.ReadAllTextAsync(process.StandardOutputPath); var stderr = await File.ReadAllTextAsync(process.StandardErrorPath);
            if (arguments.Count == 1) Assert.True(process.ExitCode == 0, root + Environment.NewLine + stdout + Environment.NewLine + stderr);
            return stdout;
        }
        var success = await Invoke(outer);
        var summaryLine = Assert.Single(success.Split('\n').Select(line => line.Trim()), line => line.StartsWith('{') && line.Contains("\"native_generation\"", StringComparison.Ordinal));
        var summary = JsonNode.Parse(summaryLine)!.AsObject();
        var generations = new FileArtifactStore(outputRoot, bytes => ContentHash.Sha256(bytes.Span)); var pins = new PinnedArtifactReader(generations);
        var reportPin = summary["native_generation"]!["Report"]!.Deserialize<StoredArtifact>()!; var factsPin = summary["native_generation"]!["Facts"]!.Deserialize<StoredArtifact>()!; var indexPin = summary["native_generation"]!["Index"]!.Deserialize<StoredArtifact>()!;
        var report = JsonNode.Parse((await pins.ReadAsync(reportPin)).Span)!.AsObject(); var facts = (await pins.ReadAsync(factsPin)).ToArray(); await pins.ReadAsync(indexPin);
        var originals = Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories).ToDictionary(path => path, path => ContentHash.Sha256(File.ReadAllBytes(path)));
        await Invoke(outer with { Sha256 = new string('0', 64) }); await Invoke(outer); await Invoke(outer, "Bogus");
        var observed = new SourceProducerIdentityBuilder().Create().Identity;
        return new(exits, report, summary, facts, arguments,
            protectedInputs.All(pair => ContentHash.Sha256(File.ReadAllBytes(pair.Key)) == pair.Value),
            originals.All(pair => ContentHash.Sha256(File.ReadAllBytes(pair.Key)) == pair.Value) && Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories).Count() == originals.Count,
            originals.Count == 3, JsonNode.DeepEquals(JsonSerializer.SerializeToNode(observed), report["native_producer"]), true, runtime, project);
    }
}
[Trait("Gate", "Real"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class PublishedHashMainCliRealTests
{
    [Fact] public Task SameCompiledCallerArgvOutputPinAndRetentionAssertions() => PublishedHashMainCliContractCases.AssertCase(new NativeHashMainCliProbe());
}
