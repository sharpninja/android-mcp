using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
public sealed class SourceExtractRoundTripRealTests
{
    [Theory, MemberData(nameof(SourceExtractRoundTripContractCases.Cases), MemberType = typeof(SourceExtractRoundTripContractCases))]
    public Task NativeWriterCacheAndReaderUseSameCommittedIndexAndRawUnavailableReadbackAssertions(string id) =>
        SourceExtractRoundTripContractCases.AssertContract(new(id), new NativeProbe());

    private sealed class NativeProbe : ISourceExtractRoundTripProbe
    {
        public Task<SourceExtractRoundTripObservation> RunAsync(SourceExtractRoundTripCase test)
        {
            var fixture = SourceExtractWriteFixture.Create(SourceExtractWriteContractCases.Get(test.Id));
            var rig = new SourceExtractWriteDependencies(fixture, CancellationToken.None); var state = new SourceExtractRoundTripState();
            var raw = new GuardedRaw(rig.Raw, state); var canonical = new CanonicalJson();
            var cache = new CountedCache(new SourceCache(rig.Generations, rig.Leases, canonical), state);
            var service = new SourcesExtractService(raw, rig.Generations, rig.Runtimes, cache, canonical, rig.Leases);
            var reader = new TrackedReader(service, state, rig); var before = 0;
            return SourceExtractRoundTripHarness.RunAsync(fixture, service, reader, state,
                () => { raw.Blocked = true; cache.Committed = true; before = rig.Dispatches; reader.ExtractionsBefore = before; },
                () => rig.OriginalsRetained, pin => rig.GenerationFiles.TryGetValue(pin.RelativePath, out var artifact) && SourceExtractWriteFixture.Pin(artifact) == pin);
        }
    }
    private sealed class GuardedRaw(IArtifactStore source, SourceExtractRoundTripState state) : IArtifactStore
    {
        internal bool Blocked;
        public Task<ReadOnlyMemory<byte>> ReadAsync(string path, CancellationToken cancellationToken = default)
        {
            if (Blocked) { state.RawReads++; throw new FileNotFoundException("Consumer raw access intentionally unavailable; producer originals retained"); }
            return source.ReadAsync(path, cancellationToken);
        }
        public Task<StoredArtifact> WriteAsync(Artifact artifact, CancellationToken cancellationToken = default) => throw new IOException("Raw producer originals are read-only");
    }
    private sealed class CountedCache(ISealedSourceCache cache, SourceExtractRoundTripState state) : ISealedSourceCache
    {
        internal bool Committed;
        public Task<SourceCacheLookup> LookupAsync(SourceCacheRequest request, SourceCacheSeal seal, CancellationToken cancellationToken = default)
        { if (Committed) state.Lookups++; return cache.LookupAsync(request, seal, cancellationToken); }
        public Task<SourceCachePublication> PublishAsync(SourceCacheRequest request, CancellationToken cancellationToken = default) => cache.PublishAsync(request, cancellationToken);
        public Task<SourceCacheReceipt> WriteAsync(SourceCacheRequest request, CancellationToken cancellationToken = default) => cache.WriteAsync(request, cancellationToken);
        public Task<ReadOnlyMemory<byte>> ReadAsync(SourceCacheRequest request, CancellationToken cancellationToken = default) => cache.ReadAsync(request, cancellationToken);
    }
    private sealed class TrackedReader(ISourceExtractGenerationReader reader, SourceExtractRoundTripState state, SourceExtractWriteDependencies rig) : ISourceExtractGenerationReader
    {
        internal int ExtractionsBefore;
        public async Task<ISourceExtractGeneration> OpenAsync(SourceExtractGenerationRequest request, CancellationToken cancellationToken = default)
        {
            state.SubmittedIndex = request.TrustedIndex!;
            return new TrackedGeneration(await reader.OpenAsync(request, cancellationToken), state, () => rig.Dispatches - ExtractionsBefore);
        }
    }
    private sealed class TrackedGeneration(ISourceExtractGeneration generation, SourceExtractRoundTripState state, Func<int> extractionDelta) : ISourceExtractGeneration
    {
        public async Task<SourceExtractRow> ReadAsync(RehydrationSourceRow row, CancellationToken cancellationToken = default)
        { var result = await generation.ReadAsync(row, cancellationToken); state.Extractions = extractionDelta(); return result; }
        public void Dispose() { generation.Dispose(); state.ReaderDisposed = true; }
    }
}
