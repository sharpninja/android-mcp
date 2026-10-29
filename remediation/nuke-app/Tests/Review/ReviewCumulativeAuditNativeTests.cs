using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Review;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed class ReviewCumulativeAuditNativeTests
{
    [Theory,MemberData(nameof(ReviewCumulativeAuditCases.Data),MemberType=typeof(ReviewCumulativeAuditCases))]
    public async Task SameCumulativeInputsAndFullProofAssertionsBindToNativeAdapter(string name)
    {
        var fixture=new CumulativeAuditFixture(name);
        var actual=await new NativeCumulativeAuditCases().ExecuteAsync(fixture);
        ReviewCumulativeAuditCases.AssertContract(fixture,ReviewCumulativeAuditCases.Json(actual));
    }
}
public sealed class NativeCumulativeAuditCases:IReviewCumulativeAuditCaseSystem
{
    public async Task<CumulativeAuditObservation> ExecuteAsync(CumulativeAuditFixture fixture)
    {
        var runtime=new CumulativeRuntimeSource(fixture);var cumulative=new CumulativeVerdictSource(fixture);
        var purpose=new CumulativePurposeSource(fixture);var store=new ReadOnlyCumulativeProofStore(fixture);
        IReviewAuditTransport adapter=new OfficialPluginReviewAuditTransport(purpose,runtime,store,new CanonicalJson(),
            OfficialTransportFixture.Workspace,cumulative);
        var calls=new List<AuditAdapterCall>();ReviewAuditCompletionEvidence? completion=null;string status;
        try
        {
            calls.Add(new("ReadReviewerOwnTurn",fixture.Snapshot,fixture.FullReport,fixture.Receipts.ToArray()));
            completion=await adapter.ReadReviewerOwnTurnAsync(fixture.Snapshot,fixture.FullReport,fixture.Receipts);status="accepted";
        }
        catch(ReviewException error){status=error.Code=="REVIEWER_AUDIT_UNAVAILABLE" ? "audit-unverified" : error.Code;}
        return new(status,calls,runtime.Calls,cumulative.Calls,purpose.Calls,fixture.Runtime,fixture.Cumulative,purpose.Returned,
            completion,fixture.Proofs,store.WriteAttempts,[]);
    }
}
public sealed class ReadOnlyCumulativeProofStore(CumulativeAuditFixture fixture):IArtifactStore
{
    public List<string> WriteAttempts { get; }=[];
    public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default)
    {WriteAttempts.Add(artifact.RelativePath);throw new InvalidOperationException("Cumulative audit must not author reviewer evidence.");}
    public Task<ReadOnlyMemory<byte>> ReadAsync(string relativePath,CancellationToken cancellationToken=default)
        =>Task.FromResult<ReadOnlyMemory<byte>>(fixture.Proofs[relativePath]);
}
