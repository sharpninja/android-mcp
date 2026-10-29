using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed class NativeSourcesInventoryProbe:ISourcesInventoryProbe
{
    private readonly SourcesInventoryCase fixture;
    private readonly List<RehydrationPlanRequest> plans=[];
    private readonly List<(string Path,CancellationToken Token)> reads=[];
    private readonly List<(SourceBatchResumeRequest Request,CancellationToken Token)> resumes=[];
    private readonly List<(Artifact Artifact,CancellationToken Token)> writes=[];
    private readonly byte[] original;
    public NativeSourcesInventoryProbe(SourcesInventoryCase test){fixture=test;original=test.AdmissionBytes.ToArray();}
    public IReadOnlyList<RehydrationPlanRequest> PlannerRequests=>plans;
    public IReadOnlyList<(string Path,CancellationToken Token)> Reads=>reads;
    public IReadOnlyList<(SourceBatchResumeRequest Request,CancellationToken Token)> ResumeRequests=>resumes;
    public IReadOnlyList<(Artifact Artifact,CancellationToken Token)> Writes=>writes;
    public bool InputRetained=>original.AsSpan().SequenceEqual(fixture.AdmissionBytes);
    public int NetworkCalls=>0;
    public int ProcessCalls=>0;
    public Task<SourcesInventoryResult> RunAsync(SourcesInventoryRequest request,CancellationToken token)
        =>new SourcesInventoryOperation(new FixturePlanner(this),new FixtureReader(this),new InputStore(this),new OutputStore(this),SourceBatchCases.Canonical).RunAsync(request,token);
    private sealed class FixturePlanner(NativeSourcesInventoryProbe owner):IRehydrationPlanner
    {
        public RehydrationPlanResult Plan(RehydrationPlanRequest request){owner.plans.Add(request);return owner.fixture.Planned;}
    }
    private sealed class FixtureReader(NativeSourcesInventoryProbe owner):ISourceBatchReader
    {
        public Task<SourceBatchResult> ResumeOnlyAsync(SourceBatchResumeRequest request,CancellationToken cancellationToken=default)
        {
            owner.resumes.Add((request,cancellationToken));cancellationToken.ThrowIfCancellationRequested();
            if(owner.fixture.Mode is "reader-missing" or "reader-replaced" or "reader-output-missing")throw new TransportException(owner.fixture.ErrorCode,"Native facade fixture read-only boundary rejection");
            var result=owner.fixture.Acquired;
            if(owner.fixture.Mode=="reader-not-reused")result=result with{Reused=false};
            if(owner.fixture.Mode=="reader-pin")result=result with{Manifest=result.Manifest with{Sha256=new string('0',64)}};
            return Task.FromResult(result);
        }
    }
    private sealed class InputStore(NativeSourcesInventoryProbe owner):IArtifactStore
    {
        public Task<ReadOnlyMemory<byte>> ReadAsync(string relativePath,CancellationToken cancellationToken=default)
        {
            owner.reads.Add((relativePath,cancellationToken));cancellationToken.ThrowIfCancellationRequested();
            if(owner.fixture.Mode=="manifest-missing")throw new FileNotFoundException(relativePath);
            return Task.FromResult<ReadOnlyMemory<byte>>(owner.fixture.AdmissionBytes);
        }
        public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default)=>throw new InvalidOperationException("Inventory attempted to mutate acquisition input");
    }
    private sealed class OutputStore(NativeSourcesInventoryProbe owner):IArtifactStore
    {
        public Task<ReadOnlyMemory<byte>> ReadAsync(string relativePath,CancellationToken cancellationToken=default)=>throw new InvalidOperationException("Inventory unexpectedly read output");
        public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default)
        {
            owner.writes.Add((artifact,cancellationToken));cancellationToken.ThrowIfCancellationRequested();
            if(owner.fixture.Mode=="output-failure")throw new IOException("Retain original output");
            return Task.FromResult(new StoredArtifact(artifact.RelativePath,ContentHash.Sha256(artifact.Bytes.Span),artifact.Bytes.Length,artifact.MediaType));
        }
    }
}
[Trait("Gate","Real")]
public sealed class SourcesInventoryRealTests
{
    [Theory,MemberData(nameof(SourcesInventoryCase.Cases),MemberType=typeof(SourcesInventoryCase))]
    public Task SameRecipeBoundSourceStatusAndProvenance(SourcesInventoryCase test)=>test.AssertCase(new NativeSourcesInventoryProbe(test));
}
