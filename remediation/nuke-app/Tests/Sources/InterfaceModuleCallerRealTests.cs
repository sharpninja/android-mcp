using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class NativeModuleMainCliProbe : IModuleMainCliProbe
{
    public async Task<ModuleMainCliObservation> RunAsync()
    {
        var basis = Path.Combine(Path.GetTempPath(), "module-main-compiled", Guid.NewGuid().ToString("N"));
        var project = Path.Combine(basis, "retained project"); var outputRoot = Path.Combine(basis, "new evidence"); var runtime = Path.Combine(basis, "isolated runtime");
        Directory.CreateDirectory(outputRoot); Directory.CreateDirectory(Path.Combine(runtime, ".nuke"));
        var f = ModuleMainFixture.Create(InterfaceModuleMainContractCases.All.Single(test => test.Variant == "default"));
        var inputs = new FileArtifactStore(project, bytes => ContentHash.Sha256(bytes.Span)); var canonical = new CanonicalJson();
        foreach (var input in f.Inputs) await inputs.WriteAsync(new(input.Key, input.Value, "application/octet-stream"));
        var wire = new InterfaceModuleWorkflowFile(f.Request.Inventory, f.Request.DeclaredSources, outputRoot, ModuleMainFixture.Generation);
        var outer = await inputs.WriteAsync(new("workflows/modules.json", canonical.Serialize(JsonSerializer.SerializeToElement(wire)), "application/json"));
        var broken = JsonNode.Parse(f.Inputs[ModuleMainFixture.InventoryPath])!.AsObject(); broken["sources"]![0]!["sha256"] = new string('0', 64);
        var brokenInventory = await inputs.WriteAsync(new("outside-corpus-audit/broken-inventory.json", canonical.Serialize(JsonSerializer.SerializeToElement(broken)), "application/json"));
        const string brokenGeneration = "broken-interface-module-facts";
        var brokenOuter = await inputs.WriteAsync(new("workflows/broken.json", canonical.Serialize(JsonSerializer.SerializeToElement(wire with { Inventory = brokenInventory, GenerationRoot = brokenGeneration })), "application/json"));
        var protectedInputs = Directory.EnumerateFiles(project, "*", SearchOption.AllDirectories).ToDictionary(path => path, path => ContentHash.Sha256(File.ReadAllBytes(path)));
        var host = Environment.ProcessPath ?? throw new InvalidOperationException("Native host absent"); Assert.Equal("dotnet", Path.GetFileNameWithoutExtension(host));
        var tool = new ToolIdentity(host, ContentHash.Sha256(await File.ReadAllBytesAsync(host)), Environment.Version.ToString());
        var runner = new NativeProcessRunner(new SystemClock()); var arguments = new List<IReadOnlyList<string>>(); var exits = new List<int>();
        async Task<(string Out, string Error)> Invoke(StoredArtifact pin, string task = "InterfaceModules")
        {
            var args = InterfaceModuleCallerContractCases.Arguments(runtime, project, pin, task).ToList(); args[1] = typeof(Build).Assembly.Location;
            args[args.IndexOf("--dotnet-host") + 1] = host;
            args.InsertRange(1, ["--runtimeconfig", Path.Combine(AppContext.BaseDirectory, "AndroidMcp.Build.Tests.runtimeconfig.json"), "--depsfile", Path.Combine(AppContext.BaseDirectory, "AndroidMcp.Build.Tests.deps.json")]);
            arguments.Add(args.ToArray());
            var process = await runner.RunAsync(new(tool, args, runtime, new Dictionary<string, string>(), Path.Combine(basis, "process-" + arguments.Count)));
            exits.Add(process.ExitCode); var stdout = await File.ReadAllTextAsync(process.StandardOutputPath); var stderr = await File.ReadAllTextAsync(process.StandardErrorPath);
            if (arguments.Count == 1) Assert.True(process.ExitCode == 0, basis + Environment.NewLine + stdout + Environment.NewLine + stderr);
            return (stdout, stderr);
        }
        var success = await Invoke(outer);
        var line = Assert.Single(success.Out.Split('\n').Select(row => row.Trim()), row => row.StartsWith('{') && row.Contains("\"build_files_executed\"", StringComparison.Ordinal));
        var stdout = JsonNode.Parse(line)!.AsObject();
        var output = new FileArtifactStore(outputRoot, bytes => ContentHash.Sha256(bytes.Span)); var pins = new PinnedArtifactReader(output);
        var indexPath = ModuleMainFixture.Generation + "/index.json"; var indexBytes = await output.ReadAsync(indexPath); await pins.ReadAsync(HashMainFixture.Pin(indexPath, indexBytes.ToArray()));
        var index = JsonNode.Parse(indexBytes.Span)!.AsObject(); var products = index["Outputs"]!.Deserialize<StoredArtifact[]>()!;
        Assert.Equal(2, products.Length);
        var reportPin = products.Single(pin => pin.RelativePath.EndsWith("/report.json", StringComparison.Ordinal)); var factsPin = products.Single(pin => pin.RelativePath.EndsWith("/facts.jsonl", StringComparison.Ordinal));
        var report = JsonNode.Parse((await pins.ReadAsync(reportPin)).Span)!.AsObject(); var facts = (await pins.ReadAsync(factsPin)).ToArray();
        foreach (var pin in index["Inputs"]!.Deserialize<StoredArtifact[]>()!) await new PinnedArtifactReader(inputs).ReadAsync(pin);
        var originals = Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories).ToDictionary(path => path, path => ContentHash.Sha256(File.ReadAllBytes(path)));
        var originalDirectories = Directory.EnumerateDirectories(outputRoot, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
        var wrongPin = await Invoke(outer with { Sha256 = new string('0', 64) });
        var noWrongPinDirectory = originalDirectories.SequenceEqual(Directory.EnumerateDirectories(outputRoot, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)) && (wrongPin.Out + wrongPin.Error).Contains("ARTIFACT_PIN_HASH", StringComparison.Ordinal);
        await Invoke(outer);
        var failed = await Invoke(brokenOuter); var brokenPath = Path.Combine(outputRoot, brokenGeneration);
        var emptyRetained = Directory.Exists(brokenPath) && !Directory.EnumerateFileSystemEntries(brokenPath).Any() && (failed.Out + failed.Error).Contains("Selected source bytes differ", StringComparison.Ordinal);
        var retry = await Invoke(brokenOuter); var retryText = retry.Out + retry.Error;
        var retryBeforeRead = retryText.Contains("Artifact already exists and was retained", StringComparison.Ordinal) && !retryText.Contains("Selected source bytes differ", StringComparison.Ordinal) && !Directory.EnumerateFileSystemEntries(brokenPath).Any();
        await Invoke(outer, "Bogus");
        var observed = new SourceProducerIdentityBuilder().Create().Identity;
        return new(exits, report, stdout, facts, arguments,
            protectedInputs.All(pair => ContentHash.Sha256(File.ReadAllBytes(pair.Key)) == pair.Value),
            originals.All(pair => ContentHash.Sha256(File.ReadAllBytes(pair.Key)) == pair.Value) && Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories).Count() == originals.Count,
            noWrongPinDirectory, emptyRetained, retryBeforeRead, JsonNode.DeepEquals(JsonSerializer.SerializeToNode(observed), report["native_producer"]), true, runtime, project);
    }
}
[Trait("Gate", "Real"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class InterfaceModuleCallerRealTests
{
    [Fact] public Task SameCompiledMonolithContractPreservesPinsReportAndEarlyDirectoryEffects() => InterfaceModuleCallerContractCases.AssertCase(new NativeModuleMainCliProbe());
}
