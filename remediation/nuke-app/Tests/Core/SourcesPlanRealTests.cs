using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Execution;
using AndroidMcp.BuildTool.Publication;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;

public sealed class NativeSourcesPlanProbe:ISourcesPlanProbe
{
    private readonly FetchPlannerProbe planner;
    private readonly ObservedPlanStore store;
    public NativeSourcesPlanProbe(SourcesPlanCase test){planner=new(test.Planned);store=new(test.Mode=="write-failure");}
    public IReadOnlyList<RehydrationPlanRequest> PlannerRequests=>planner.Requests;
    public IReadOnlyList<(Artifact Artifact,CancellationToken Token)> Writes=>store.Writes;
    public int NetworkCalls=>0;
    public int ProcessCalls=>0;
    public Task<SourcesPlanResult> RunAsync(RehydrationPlanRequest request,CancellationToken token)=>new SourcesPlanOperation(planner,store).RunAsync(request,token);
    private sealed class ObservedPlanStore(bool fail):IArtifactStore
    {
        public List<(Artifact Artifact,CancellationToken Token)> Writes{get;}=[];
        public Task<ReadOnlyMemory<byte>> ReadAsync(string relativePath,CancellationToken cancellationToken=default)=>throw new InvalidOperationException("Pure plan facade must not read source or bundle bytes.");
        public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default)
        {
            Writes.Add((artifact,cancellationToken));
            if(fail)throw new IOException("Pinned output failure fixture.");
            return Task.FromResult(new StoredArtifact(artifact.RelativePath,ContentHash.Sha256(artifact.Bytes.Span),artifact.Bytes.Length,artifact.MediaType));
        }
    }
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
public sealed class SourcesPlanRealTests
{
    [Theory,MemberData(nameof(SourcesPlanCase.Cases),MemberType=typeof(SourcesPlanCase))]
    public Task SameInvocationAndTruthfulMetadataCases(SourcesPlanCase test)=>test.AssertCase(new NativeSourcesPlanProbe(test));
}
