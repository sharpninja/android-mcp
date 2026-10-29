using System.Text;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class NativeHashMainProbe : IHashMainProbe
{
    public async Task<HashMainObservation> RunAsync(HashMainFixture f)
    {
        var root = Path.Combine(Path.GetTempPath(), "hash-main-native", Guid.NewGuid().ToString("N"));
        var inputRoot = Path.Combine(root, "retained inputs"); var outputRoot = Path.Combine(root, "new output"); Directory.CreateDirectory(outputRoot);
        var source = new FileArtifactStore(inputRoot, bytes => ContentHash.Sha256(bytes.Span)); var output = new FileArtifactStore(outputRoot, bytes => ContentHash.Sha256(bytes.Span));
        foreach (var pair in f.Inputs) await source.WriteAsync(new(pair.Key, pair.Value, "application/octet-stream"));
        foreach (var pair in f.Existing) await output.WriteAsync(new(pair.Key, pair.Value, "application/octet-stream"));
        if (f.ExistingDirectory) Directory.CreateDirectory(Path.Combine(outputRoot, HashMainFixture.Generation));
        using var cancel = new CancellationTokenSource(); var reads = new List<string>(); var writes = new List<string>(); var parses = new List<string>(); var profiles = new List<string>(); var tokens = new List<CancellationToken>();
        var tracedInput = new Input(source, f, reads, tokens, cancel); var tracedOutput = new Output(output, f, writes, tokens, cancel);
        var runtime = new RuntimeFactory(f, parses, profiles);
        var workflow = new PublishedInterfaceHashWorkflow(tracedInput, tracedOutput, new Tree(new NativeArtifactWorkspace(outputRoot), tokens),
            new Leases(new FileArtifactLeaseProvider(Path.Combine(root, "leases")), tokens), runtime, new CanonicalJson(), new Clock());
        if (f.Case.Variant == "cancel-before") cancel.Cancel();
        PublishedInterfaceHashWorkflowResult? result = null;
        var error = await Record.ExceptionAsync(async () => result = await workflow.RunAsync(f.Request, cancel.Token));
        var files = Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories).ToDictionary(path => Path.GetRelativePath(outputRoot, path).Replace(Path.DirectorySeparatorChar, '/'), File.ReadAllBytes, StringComparer.Ordinal);
        var retained = f.Inputs.All(pair => File.ReadAllBytes(ArtifactPaths.Resolve(inputRoot, pair.Key)).SequenceEqual(pair.Value));
        var existing = f.Existing.All(pair => files.TryGetValue(pair.Key, out var bytes) && bytes.SequenceEqual(pair.Value));
        return new(result?.Report, result?.Summary, files, reads, parses, writes, Normalize(error), retained, existing, tokens.All(token => token == cancel.Token), profiles);
    }
    private static string? Normalize(Exception? error) => error switch
    {
        null => null, OperationCanceledException => "CANCEL",
        ArtifactGenerationException { Code: "PROJECTION_FAILED", InnerException: { } inner } => Normalize(inner),
        ArtifactGenerationException generation => generation.Code,
        SourceExtractException source => source.Code, PinnedArtifactException pin => pin.Code, CanonicalJsonException json => json.Code,
        DecoderFallbackException => "UTF8", FileNotFoundException or DirectoryNotFoundException => "MISSING",
        InvalidOperationException invalid when invalid.Message == "scripted runtime failure" => "RUNTIME",
        ArgumentException or InvalidOperationException or KeyNotFoundException => "ARGUMENT", IOException => "IO", _ => error.GetType().Name
    };
    private sealed class Clock : IClock { public DateTimeOffset Now => HashMainFixture.At; }
    private sealed class Input(IArtifactStore store, HashMainFixture f, List<string> reads, List<CancellationToken> tokens, CancellationTokenSource cancel) : IArtifactStore
    {
        private int rawReads;
        public async Task<ReadOnlyMemory<byte>> ReadAsync(string path, CancellationToken cancellationToken = default)
        {
            reads.Add(path); tokens.Add(cancellationToken);
            if (path == HashMainFixture.RawPath && f.Case.Variant == "cancel-read") { cancel.Cancel(); cancellationToken.ThrowIfCancellationRequested(); }
            var bytes = await store.ReadAsync(path, cancellationToken);
            if (path == HashMainFixture.RawPath)
            {
                rawReads++;
                if (f.Case.Variant == "change-before-generation" && rawReads == 2 || f.Case.Variant == "change-after-write" && rawReads == 3) return new byte[] { 0 };
            }
            return bytes;
        }
        public Task<StoredArtifact> WriteAsync(Artifact artifact, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Workflow must not write retained inputs");
    }
    private sealed class Output(IArtifactStore store, HashMainFixture f, List<string> writes, List<CancellationToken> tokens, CancellationTokenSource cancel) : IArtifactStore
    {
        public Task<ReadOnlyMemory<byte>> ReadAsync(string path, CancellationToken cancellationToken = default) { tokens.Add(cancellationToken); return store.ReadAsync(path, cancellationToken); }
        public async Task<StoredArtifact> WriteAsync(Artifact artifact, CancellationToken cancellationToken = default)
        {
            writes.Add(artifact.RelativePath); tokens.Add(cancellationToken);
            var variant = f.Case.Variant;
            if (variant == "write-index" && artifact.RelativePath.EndsWith("/index.json", StringComparison.Ordinal)) throw new IOException("scripted commit failure");
            if ((variant is "write-facts" or "cancel-write" && artifact.RelativePath.EndsWith("/facts.jsonl", StringComparison.Ordinal)) || (variant == "write-report" && artifact.RelativePath.EndsWith("/report.json", StringComparison.Ordinal)))
            {
                await store.WriteAsync(artifact with { Bytes = new byte[] { 1 } }, cancellationToken);
                if (variant == "cancel-write") { cancel.Cancel(); cancellationToken.ThrowIfCancellationRequested(); }
                throw new IOException("scripted partial write failure");
            }
            var pin = await store.WriteAsync(artifact, cancellationToken);
            return variant == "bad-receipt" && artifact.RelativePath.EndsWith("/report.json", StringComparison.Ordinal) ? pin with { Sha256 = new string('0', 64) } : pin;
        }
    }
    private sealed class Tree(IArtifactTreeReader tree, List<CancellationToken> tokens) : IArtifactTreeReader
    {
        public Task<IReadOnlyList<ArtifactTreeEntry>> EnumerateAsync(string? relativeDirectory = null, bool recursive = true, CancellationToken cancellationToken = default)
        { tokens.Add(cancellationToken); Assert.False(recursive); return tree.EnumerateAsync(relativeDirectory, recursive, cancellationToken); }
    }
    private sealed class Leases(IArtifactLeaseProvider leases, List<CancellationToken> tokens) : IArtifactLeaseProvider
    {
        public ValueTask<IAsyncDisposable> AcquireAsync(string scope, CancellationToken cancellationToken = default)
        { tokens.Add(cancellationToken); Assert.Equal("interface-hashes/" + HashMainFixture.Generation, scope); return leases.AcquireAsync(scope, cancellationToken); }
    }
    private sealed class RuntimeFactory(HashMainFixture f, List<string> parses, List<string> profiles) : ISourcesRuntimeFactory
    {
        public ISourcesRuntime Create(SourceExtractionProfile profile, SourceJavaAdmission? java = null)
        {
            Assert.Null(java); profiles.Add(profile.Id);
            if (f.Case.Variant == "runtime") throw new InvalidOperationException("scripted runtime failure");
            return new Runtime(new SourcesRuntimeFactory().Create(profile), f, parses);
        }
        private sealed class Runtime(ISourcesRuntime inner, HashMainFixture f, List<string> parses) : ISourcesRuntime
        {
            public ISourcesExtractionDispatcher Dispatcher => inner.Dispatcher;
            public SourcesRuntimeConcerns Concerns => inner.Concerns with { PublishedHashes = new Hashes(inner.Concerns.PublishedHashes, f, parses) };
            public void Dispose() => inner.Dispose();
        }
        private sealed class Hashes(IPublishedInterfaceHashSourceFacts native, HashMainFixture f, List<string> parses) : IPublishedInterfaceHashSourceFacts
        {
            public System.Text.Json.Nodes.JsonObject AidlHashes(string text) { parses.Add("aidl"); if (f.Case.Variant == "parser-before-identity") throw new IOException("scripted parser failure"); return native.AidlHashes(text); }
            public System.Text.Json.Nodes.JsonObject HidlHashes(string text) { parses.Add("hidl"); return native.HidlHashes(text); }
        }
    }
}
[Trait("Gate", "Real"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class PublishedHashMainRealTests
{
    [Theory, MemberData(nameof(PublishedHashMainContractCases.Cases), MemberType = typeof(PublishedHashMainContractCases))]
    public Task SameOriginalMainAssertionsUseNativeTempTreesAndSharedServices(string id) => PublishedHashMainContractCases.AssertCase(HashMainFixture.Create(PublishedHashMainContractCases.Get(id)), new NativeHashMainProbe());
}
[Trait("Gate", "Real"), Trait("Requirement", "TEST-BUILD-003")]
public sealed class PublishedHashMainPlannerRealTests
{
    [Theory, MemberData(nameof(PublishedHashMainPlannerCases.Cases), MemberType = typeof(PublishedHashMainPlannerCases))]
    public void SameTaskAndDependencyGraphUsesNativePlanner(PlannerCase test) => PlannerContractCases.AssertContract(test, new OperationPlanner(new OperationRegistry()));
}
