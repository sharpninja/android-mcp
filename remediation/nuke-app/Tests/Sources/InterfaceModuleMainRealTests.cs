using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class NativeModuleMainProbe : IModuleMainProbe
{
    public async Task<ModuleMainObservation> RunAsync(ModuleMainFixture f)
    {
        var basis = Path.Combine(Path.GetTempPath(), "interface-module-main-native", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(basis, "retained inputs"); var outputRoot = Path.Combine(basis, "new output"); Directory.CreateDirectory(outputRoot);
        var source = new FileArtifactStore(sourceRoot, bytes => ContentHash.Sha256(bytes.Span)); var output = new FileArtifactStore(outputRoot, bytes => ContentHash.Sha256(bytes.Span));
        foreach (var file in f.Inputs) await source.WriteAsync(new(file.Key, file.Value, "application/octet-stream"));
        foreach (var file in f.Existing) await output.WriteAsync(new(file.Key, file.Value, "application/octet-stream"));
        if (f.ExistingDirectory) Directory.CreateDirectory(Path.Combine(outputRoot, ModuleMainFixture.Generation));
        using var cancellation = new CancellationTokenSource(); var reads = new List<string>(); var parses = new List<string>(); var writes = new List<string>(); var directoryCalls = new List<string>(); var profiles = new List<string>(); var tokens = new List<CancellationToken>();
        var leases = new Leases(new FileArtifactLeaseProvider(Path.Combine(basis, "leases")), tokens);
        var workflow = new InterfaceModuleWorkflow(new Input(source, f, reads, tokens, cancellation), new Output(output, f, writes, tokens, cancellation),
            new Directories(output, leases, directoryCalls, tokens), leases, new RuntimeFactory(f, parses, profiles), new CanonicalJson(), new LegacyJsonValueEquality(), new Clock());
        if (f.Case.Variant == "cancel-before") cancellation.Cancel();
        InterfaceModuleWorkflowResult? result = null;
        var error = await Record.ExceptionAsync(async () => result = await workflow.RunAsync(f.Request, cancellation.Token));
        Assert.Equal(0, leases.Held);
        var observedReads = reads.ToArray(); var observedParses = parses.ToArray(); var observedWrites = writes.ToArray(); var observedDirectories = directoryCalls.ToArray(); var observedProfiles = profiles.ToArray(); var forwarded = tokens.All(token => token == cancellation.Token);
        var directoryExists = Directory.Exists(Path.Combine(outputRoot, ModuleMainFixture.Generation)); var retryRefused = false;
        if (f.DirectoryCreated && error is not null)
        {
            var retry = await Record.ExceptionAsync(() => workflow.RunAsync(f.Request)); retryRefused = retry is IOException;
            Assert.Equal(observedParses.Length, parses.Count); Assert.Equal(observedWrites.Length, writes.Count); Assert.Equal(0, leases.Held);
        }
        var files = Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories).ToDictionary(path => Path.GetRelativePath(outputRoot, path).Replace(Path.DirectorySeparatorChar, '/'), File.ReadAllBytes, StringComparer.Ordinal);
        var retained = f.Inputs.All(pair => File.ReadAllBytes(ArtifactPaths.Resolve(sourceRoot, pair.Key)).SequenceEqual(pair.Value));
        var existing = f.Existing.All(pair => files.TryGetValue(pair.Key, out var bytes) && bytes.SequenceEqual(pair.Value));
        return new(result?.Report, files, observedReads, observedParses, observedWrites, observedDirectories, directoryExists, retryRefused, Normalize(error), retained, existing, forwarded, observedProfiles);
    }
    private static string? Normalize(Exception? error) => error switch
    {
        null => null, OperationCanceledException => "CANCEL",
        ArtifactGenerationException { Code: "PROJECTION_FAILED", InnerException: { } inner } => Normalize(inner),
        ArtifactGenerationException generation => generation.Code, SourceExtractException source => source.Code,
        PinnedArtifactException pin => pin.Code, CanonicalJsonException json => json.Code,
        DecoderFallbackException => "UTF8", FileNotFoundException or DirectoryNotFoundException => "MISSING",
        InvalidOperationException invalid when invalid.Message == "scripted runtime failure" => "RUNTIME",
        ArgumentException or InvalidOperationException or KeyNotFoundException => "ARGUMENT", IOException => "IO", _ => error.GetType().Name
    };
    private sealed class Clock : IClock { public DateTimeOffset Now => HashMainFixture.At; }
    private sealed class Input(IArtifactStore store, ModuleMainFixture f, List<string> reads, List<CancellationToken> tokens, CancellationTokenSource cancellation) : IArtifactStore
    {
        private int inventoryReads, rawReads;
        public async Task<ReadOnlyMemory<byte>> ReadAsync(string path, CancellationToken token = default)
        {
            reads.Add(path); tokens.Add(token);
            if (path == ModuleMainFixture.RawPath && f.Case.Variant == "cancel-read") { cancellation.Cancel(); token.ThrowIfCancellationRequested(); }
            var bytes = await store.ReadAsync(path, token);
            if (path == ModuleMainFixture.InventoryPath)
            {
                inventoryReads++;
                if (f.Case.Variant == "inventory-change-before" && inventoryReads == 2 || f.Case.Variant == "inventory-change-after" && inventoryReads == 3) return new byte[] { 0 };
            }
            if (path == ModuleMainFixture.RawPath && ++rawReads == 2 && f.Case.Variant == "raw-change-after") return new byte[] { 0 };
            return bytes;
        }
        public Task<StoredArtifact> WriteAsync(Artifact artifact, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Retained input writes forbidden");
    }
    private sealed class Output(IArtifactStore store, ModuleMainFixture f, List<string> writes, List<CancellationToken> tokens, CancellationTokenSource cancellation) : IArtifactStore
    {
        public Task<ReadOnlyMemory<byte>> ReadAsync(string path, CancellationToken token = default) { tokens.Add(token); return store.ReadAsync(path, token); }
        public async Task<StoredArtifact> WriteAsync(Artifact artifact, CancellationToken token = default)
        {
            writes.Add(artifact.RelativePath); tokens.Add(token); var variant = f.Case.Variant;
            if (variant == "write-index" && artifact.RelativePath.EndsWith("/index.json", StringComparison.Ordinal)) throw new IOException("scripted commit failure");
            if ((variant is "write-facts" or "cancel-write" && artifact.RelativePath.EndsWith("/facts.jsonl", StringComparison.Ordinal)) || variant == "write-report" && artifact.RelativePath.EndsWith("/report.json", StringComparison.Ordinal))
            {
                await store.WriteAsync(artifact with { Bytes = new byte[] { 1 } }, token);
                if (variant == "cancel-write") { cancellation.Cancel(); token.ThrowIfCancellationRequested(); }
                throw new IOException("scripted partial write failure");
            }
            var pin = await store.WriteAsync(artifact, token);
            return variant == "bad-receipt" && artifact.RelativePath.EndsWith("/report.json", StringComparison.Ordinal) ? pin with { Sha256 = new string('0', 64) } : pin;
        }
    }
    private sealed class Leases(IArtifactLeaseProvider inner, List<CancellationToken> tokens) : IArtifactLeaseProvider
    {
        public int Held { get; private set; }
        public async ValueTask<IAsyncDisposable> AcquireAsync(string scope, CancellationToken token = default)
        {
            Assert.Equal("interface-modules/" + ModuleMainFixture.Generation, scope); tokens.Add(token); var lease = await inner.AcquireAsync(scope, token); Held++;
            return new Lease(this, lease);
        }
        private sealed class Lease(Leases owner, IAsyncDisposable lease) : IAsyncDisposable
        { public async ValueTask DisposeAsync() { await lease.DisposeAsync(); owner.Held--; } }
    }
    private sealed class Directories(ICreateNewArtifactDirectory inner, Leases leases, List<string> calls, List<CancellationToken> tokens) : ICreateNewArtifactDirectory
    {
        public Task CreateNewDirectoryAsync(string path, CancellationToken token = default)
        { Assert.Equal(1, leases.Held); calls.Add(path); tokens.Add(token); return inner.CreateNewDirectoryAsync(path, token); }
    }
    private sealed class RuntimeFactory(ModuleMainFixture f, List<string> parses, List<string> profiles) : ISourcesRuntimeFactory
    {
        public ISourcesRuntime Create(SourceExtractionProfile profile, SourceJavaAdmission? java = null)
        {
            Assert.Null(java); profiles.Add(profile.Id); if (f.Case.Variant == "runtime") throw new InvalidOperationException("scripted runtime failure");
            return new Runtime(new SourcesRuntimeFactory().Create(profile), f, parses);
        }
        private sealed class Runtime(ISourcesRuntime inner, ModuleMainFixture f, List<string> parses) : ISourcesRuntime
        {
            public ISourcesExtractionDispatcher Dispatcher => inner.Dispatcher;
            public SourcesRuntimeConcerns Concerns => inner.Concerns with { Soong = new Soong(inner.Concerns.Soong, f, parses) };
            public void Dispose() => inner.Dispose();
        }
        private sealed class Soong(ISoongSourceFacts inner, ModuleMainFixture f, List<string> parses) : ISoongSourceFacts
        {
            public JsonArray Modules(string text, SoongModuleFamily family)
            { Assert.Equal(SoongModuleFamily.Interfaces, family); parses.Add(text); if (f.Case.Variant == "parser") throw new IOException("scripted parser failure"); return inner.Modules(text, family); }
            public JsonNode? Literal(JsonObject node, string? property = null) => inner.Literal(node, property);
            public IReadOnlyList<string> MatchSources(string build, IReadOnlyList<string> patterns, IReadOnlyList<string> excluded, IReadOnlyList<string> paths) => inner.MatchSources(build, patterns, excluded, paths);
            public string? ExplicitManifestPath(string build, JsonObject module) => throw new InvalidOperationException("Interface module caller must not infer APEX links");
        }
    }
}
[Trait("Gate", "Real"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class InterfaceModuleMainRealTests
{
    [Theory, MemberData(nameof(InterfaceModuleMainContractCases.Cases), MemberType = typeof(InterfaceModuleMainContractCases))]
    public Task SameMainAssertionsUseNativeTempTreesAndExistingServices(string id) => InterfaceModuleMainContractCases.AssertCase(ModuleMainFixture.Create(InterfaceModuleMainContractCases.Get(id)), new NativeModuleMainProbe());
}
[Trait("Gate", "Real"), Trait("Requirement", "TEST-BUILD-003")]
public sealed class InterfaceModulePlannerRealTests
{
    [Theory, MemberData(nameof(InterfaceModulePlannerCases.Cases), MemberType = typeof(InterfaceModulePlannerCases))]
    public void SameExistingTaskStepsUseNativePlanner(PlannerCase test) => PlannerContractCases.AssertContract(test, new OperationPlanner(new OperationRegistry()));
}
