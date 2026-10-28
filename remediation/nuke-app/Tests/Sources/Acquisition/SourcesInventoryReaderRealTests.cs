using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed class NativeSourcesInventoryReaderProbe:ISourcesInventoryReaderProbe
{
    private readonly SourcesInventoryReaderCase fixture;
    private readonly byte[] original;
    private readonly List<(StoredArtifact Pin,CancellationToken Token)> pins=[];
    private readonly List<(SourceBatchResumeRequest Request,CancellationToken Token)> resumes=[];
    public NativeSourcesInventoryReaderProbe(SourcesInventoryReaderCase test){fixture=test;original=test.Bytes.ToArray();}
    public IReadOnlyList<(StoredArtifact Pin,CancellationToken Token)> PinReads=>pins;
    public IReadOnlyList<(SourceBatchResumeRequest Request,CancellationToken Token)> ResumeReads=>resumes;
    public int Writes{get;private set;}
    public int Http=>0;
    public bool InputRetained=>original.AsSpan().SequenceEqual(fixture.Bytes);
    public Task<SourcesInventoryResult> ReadAsync(SourcesInventoryReadRequest request,CancellationToken token)
        =>new SourcesInventoryReader(new ObservedPinnedReader(this),new FixtureBatchReader(this),SourceBatchCases.Canonical).ReadAsync(request,token);
    private sealed class ObservedPinnedReader(NativeSourcesInventoryReaderProbe owner):IPinnedArtifactReader
    {
        public Task<ReadOnlyMemory<byte>> ReadAsync(StoredArtifact callerPin,CancellationToken cancellationToken=default)
        {
            owner.pins.Add((callerPin,cancellationToken));
            return new PinnedArtifactReader(new FixtureStore(owner)).ReadAsync(callerPin,cancellationToken);
        }
    }
    private sealed class FixtureStore(NativeSourcesInventoryReaderProbe owner):IArtifactStore
    {
        public Task<ReadOnlyMemory<byte>> ReadAsync(string relativePath,CancellationToken cancellationToken=default)
        {
            if(owner.fixture.Mode=="missing")throw new FileNotFoundException(relativePath);
            if(owner.fixture.Mode=="cancel-during-pin")owner.fixture.Cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            if(owner.fixture.Mode=="pin-length")return Task.FromResult<ReadOnlyMemory<byte>>(new byte[owner.fixture.Bytes.Length+1]);
            if(owner.fixture.Mode=="pin-hash"){var changed=owner.fixture.Bytes.ToArray();changed[0]^=1;return Task.FromResult<ReadOnlyMemory<byte>>(changed);}
            return Task.FromResult<ReadOnlyMemory<byte>>(owner.fixture.Bytes);
        }
        public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default)
        {owner.Writes++;throw new InvalidOperationException("Inventory reader attempted a write");}
    }
    private sealed class FixtureBatchReader(NativeSourcesInventoryReaderProbe owner):ISourceBatchReader
    {
        public Task<SourceBatchResult> ResumeOnlyAsync(SourceBatchResumeRequest request,CancellationToken cancellationToken=default)
        {
            owner.resumes.Add((request,cancellationToken));cancellationToken.ThrowIfCancellationRequested();
            if(owner.fixture.Mode.StartsWith("batch-",StringComparison.Ordinal))throw new TransportException(owner.fixture.ErrorCode,"Sealed acquisition fixture rejection");
            var result=owner.fixture.Expected.Acquisition;
            if(owner.fixture.Mode=="not-reused")result=result with{Reused=false};
            if(owner.fixture.Mode=="result-pin")result=result with{Manifest=result.Manifest with{Sha256=new string('0',64)}};
            return Task.FromResult(result);
        }
    }
}
[Trait("Gate","Real")]
public sealed class SourcesInventoryReaderRealTests
{
    [Theory,MemberData(nameof(SourcesInventoryReaderCase.All),MemberType=typeof(SourcesInventoryReaderCase))]
    public Task SamePinnedScopeResumeAndMetadataCases(SourcesInventoryReaderCase test)=>test.AssertCase(new NativeSourcesInventoryReaderProbe(test));
}
