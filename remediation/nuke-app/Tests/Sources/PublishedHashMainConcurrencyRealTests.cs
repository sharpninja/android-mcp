using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class NativeHashMainConcurrencyProbe : IHashMainConcurrencyProbe
{
    public async Task<HashMainConcurrencyObservation> RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "hash-main-concurrent", Guid.NewGuid().ToString("N")); var inputRoot = Path.Combine(root, "inputs"); var outputRoot = Path.Combine(root, "output"); Directory.CreateDirectory(outputRoot);
        var inputs = new FileArtifactStore(inputRoot, value => ContentHash.Sha256(value.Span)); var output = new FileArtifactStore(outputRoot, value => ContentHash.Sha256(value.Span));
        var f = HashMainFixture.Create(PublishedHashMainContractCases.All.Single(test => test.Variant == "aidl"));
        foreach (var pair in f.Inputs) await inputs.WriteAsync(new(pair.Key, pair.Value, "application/octet-stream"));
        var lease = new ObservedLeases(new FileArtifactLeaseProvider(Path.Combine(root, "leases")));
        PublishedInterfaceHashWorkflow Workflow() => new(inputs, output, new NativeArtifactWorkspace(outputRoot), lease, new SourcesRuntimeFactory(), new CanonicalJson(), new Clock());
        var successes = new List<PublishedInterfaceHashWorkflowResult>(); var errors = new List<Exception>(); var gate = new object();
        async Task Attempt()
        {
            try { var result = await Workflow().RunAsync(f.Request); lock (gate) successes.Add(result); }
            catch (Exception error) { lock (gate) errors.Add(error); }
        }
        await Task.WhenAll(Task.Run(Attempt), Task.Run(Attempt));
        var success = Assert.Single(successes); Assert.Single(errors);
        Assert.True(errors[0] is ArtifactGenerationException { Code: "OUTPUT_EXISTS" } or
            ArtifactGenerationException { Code: "PROJECTION_FAILED", InnerException: SourceExtractException { Code: "INTERFACE_HASH_OUTPUT_EXISTS" } });
        var read = new PinnedArtifactReader(output);
        var facts = await read.ReadAsync(success.Facts); await read.ReadAsync(success.ReportFile); await read.ReadAsync(success.Index);
        var names = Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories).ToArray();
        return new(successes.Count, errors.Count, lease.Acquired, lease.Released, names.Length == 3, true,
            facts.Span.SequenceEqual(f.ExpectedFactBytes), f.Inputs.All(pair => File.ReadAllBytes(ArtifactPaths.Resolve(inputRoot, pair.Key)).SequenceEqual(pair.Value)),
            lease.Scopes.All(value => value == "interface-hashes/" + HashMainFixture.Generation), names.All(File.Exists));
    }
    private sealed class Clock : IClock { public DateTimeOffset Now => HashMainFixture.At; }
    private sealed class ObservedLeases(IArtifactLeaseProvider inner) : IArtifactLeaseProvider
    {
        private int acquired, released; public int Acquired => acquired; public int Released => released;
        public System.Collections.Concurrent.ConcurrentBag<string> Scopes { get; } = [];
        public async ValueTask<IAsyncDisposable> AcquireAsync(string scope, CancellationToken cancellationToken = default)
        { Scopes.Add(scope); var lease = await inner.AcquireAsync(scope, cancellationToken); Interlocked.Increment(ref acquired); return new Observed(lease, this); }
        private sealed class Observed(IAsyncDisposable lease, ObservedLeases owner) : IAsyncDisposable
        { public async ValueTask DisposeAsync() { await lease.DisposeAsync(); Interlocked.Increment(ref owner.released); } }
    }
}
[Trait("Gate", "Real"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class PublishedHashMainConcurrencyRealTests
{
    [Fact] public Task SameCooperatingGenerationAssertionsUseNativeServices() => PublishedHashMainConcurrencyContract.AssertCase(new NativeHashMainConcurrencyProbe());
}
