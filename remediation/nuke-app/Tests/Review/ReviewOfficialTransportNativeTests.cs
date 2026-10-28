using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Review;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed class ReviewOfficialTransportNativeTests
{
    [Theory,MemberData(nameof(ReviewOfficialTransportCases.Data),MemberType=typeof(ReviewOfficialTransportCases))]
    public async Task SameTypedInputsAndFullProofAssertionsBindToNativeAdapter(string name)
    {
        var fixture=new OfficialTransportFixture(name);
        var actual=await new NativeOfficialTransportCases().ExecuteAsync(fixture);
        ReviewOfficialTransportCases.AssertContract(fixture,ReviewOfficialTransportCases.Json(actual));
    }
}

public sealed class NativeOfficialTransportCases : IOfficialTransportCaseSystem
{
    public async Task<OfficialTransportObservation> ExecuteAsync(OfficialTransportFixture fixture)
    {
        var purpose=new RecordingOfficialPurposeInvoker(fixture);
        var runtime=new RecordingRuntimeEvidenceSource(fixture);
        var store=new ReadOnlyOfficialProofStore(fixture);
        IReviewAuditTransport transport=new OfficialPluginReviewAuditTransport(purpose,runtime,store,
            new CanonicalJson(),OfficialTransportFixture.Workspace);
        var calls=new List<AuditAdapterCall>();
        ReviewAuditBootstrapEvidence? bootstrap=null;ReviewAuditCompletionEvidence? completion=null;string status;
        try
        {
            if(fixture.IsBootstrap)
            {
                calls.Add(new("ObserveBootstrap",fixture.Snapshot,null,[]));
                bootstrap=await transport.ObserveBootstrapAsync(fixture.Snapshot);status="ready-to-launch";
            }
            else
            {
                calls.Add(new("ReadReviewerOwnTurn",fixture.Snapshot,OfficialTransportFixture.FullReport,fixture.Receipts.ToArray()));
                completion=await transport.ReadReviewerOwnTurnAsync(fixture.Snapshot,OfficialTransportFixture.FullReport,fixture.Receipts);status="accepted";
            }
        }
        catch(ReviewException error)
        {
            status=error.Code=="REVIEWER_AUDIT_UNAVAILABLE" ? "audit-unverified" : error.Code;
        }
        return new(status,calls,purpose.InstallationCalls,purpose.PageCalls,runtime.Calls,purpose.ReturnedInstallation,
            purpose.ReturnedPages,runtime.Returned,bootstrap,completion,fixture.Proofs,
            purpose.PrivilegedMutationCalls.Concat(store.WriteAttempts).ToArray(),[]);
    }
}
public sealed class ReadOnlyOfficialProofStore(OfficialTransportFixture fixture) : IArtifactStore
{
    public List<string> WriteAttempts { get; }=[];
    public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default)
    {
        WriteAttempts.Add(artifact.RelativePath);
        throw new InvalidOperationException("Official audit transport must not author or mutate reviewer evidence.");
    }
    public Task<ReadOnlyMemory<byte>> ReadAsync(string relativePath,CancellationToken cancellationToken=default)
        =>Task.FromResult<ReadOnlyMemory<byte>>(fixture.Proofs[relativePath]);
}
