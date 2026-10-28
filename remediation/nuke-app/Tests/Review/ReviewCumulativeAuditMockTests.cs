using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Review;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed record CumulativeEvidenceCall(ReviewSnapshot Snapshot,IReadOnlyList<StoredArtifact> Receipts);
public sealed record CumulativeAuditObservation(string Status,IReadOnlyList<AuditAdapterCall> AdapterCalls,
    IReadOnlyList<RuntimePurposeCall> RuntimeCalls,IReadOnlyList<CumulativeEvidenceCall> CumulativeCalls,
    IReadOnlyList<ReviewOfficialTurnPageRequest> PageCalls,ReviewProviderRuntimeObservation Runtime,
    ReviewObservedCumulativeVerdict? Cumulative,IReadOnlyList<ReviewOfficialTurnPage> Pages,
    ReviewAuditCompletionEvidence? Completion,IReadOnlyDictionary<string,byte[]> FullProofBytes,
    IReadOnlyList<string> PrivilegedMutationCalls,IReadOnlyList<string> AddedProviderCalls);
public interface IReviewCumulativeAuditCaseSystem
{
    Task<CumulativeAuditObservation> ExecuteAsync(CumulativeAuditFixture fixture);
}

public sealed class CumulativeAuditFixture
{
    public string Name { get; }
    public bool RequiresCumulative { get; }
    public string ExpectedStatus { get; }
    public bool DeniedRead { get; }
    public ReviewSnapshot Snapshot { get; }=new(OfficialTransportFixture.Run,OfficialTransportFixture.ParentSession,
        OfficialTransportFixture.ParentRequest,OfficialTransportFixture.Head,"https://github.com/example/project/pull/1",[],[],[],[]);
    public Dictionary<string,byte[]> Proofs { get; }=new(StringComparer.Ordinal);
    public List<StoredArtifact> Receipts { get; }=[];
    public ReviewProviderRuntimeObservation Runtime { get; }
    public ReviewObservedCumulativeVerdict? Cumulative { get; }
    public ReviewOfficialTurnPage Page { get; }
    public string FullReport { get; }
    public ReviewAuditCompletionEvidence Completion { get; }
    private StoredArtifact Proof(string path,object value)
    {
        var bytes=new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(value));Proofs.Add(path,bytes);
        return new(path,ContentHash.Sha256(bytes),bytes.Length,"application/json");
    }
    public CumulativeAuditFixture(string name)
    {
        Name=name;RequiresCumulative=name!="single-call-existing-contract";DeniedRead=name=="denied-read-no-retry";
        ExpectedStatus=name is "single-call-existing-contract" or "two-call-cumulative" or "repeated-boundaries-cumulative" or "recovery-read-only"
            ? "accepted" : DeniedRead ? "audit-denied" : "audit-unverified";
        var count=RequiresCumulative ? name=="repeated-boundaries-cumulative" ? 4 : 2 : 1;
        var reports=Enumerable.Range(1,count).Select(i=>i==count ?
            Encoding.UTF8.GetString(new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(new { report_complete=true,findings=new[]{"Final visible finding café"} }))) :
            $"Material partial finding {i}: Δ.\nPreserve exact whitespace.\r\n").ToArray();
        FullReport=string.Join("\n\n",reports); // Exact existing Launch accumulator contract, including embedded CRLF.
        var runtime=new List<ReviewObservedProviderRuntime>();var links=new List<ReviewObservedReviewerTurnLink>();
        var turns=new List<ReviewOfficialPersistedTurn>();
        for(var i=1;i<=count;i++)
        {
            var call=i.ToString("D8");var session=$"ClaudeCode-20261028T00000{i}Z-call";var request=$"req-20261028T00001{i}Z-call";
            var native=$"provider-native-{i}";var report=reports[i-1];
            var ask=Proof($"reviews/{Snapshot.RunId}/calls/{call}/request.jsonl",new { Snapshot.RunId,Snapshot.Head,Snapshot.ParentSessionId,Snapshot.ParentRequestId,callId=call,assistantCaps=(object?)null });
            var response=Proof($"reviews/{Snapshot.RunId}/calls/{call}/response.visible.jsonl",new { model=OfficialTransportFixture.Model,effort=OfficialTransportFixture.Effort,session_id=native,fullVisibleReport=report });
            var tool=Proof($"reviews/{Snapshot.RunId}/calls/{call}/call-turn-tool-result.json",new { sourceType=OfficialTransportFixture.Source,sessionId=session,requestId=request,providerCallId=call });
            Receipts.Add(ask);Receipts.Add(response);runtime.Add(new(call,native,OfficialTransportFixture.Model,OfficialTransportFixture.Effort,report,response));
            links.Add(new(call,OfficialTransportFixture.Source,session,request,tool));
            turns.Add(new(OfficialTransportFixture.Workspace,OfficialTransportFixture.Source,session,request,"completed",Snapshot.RunId,Snapshot.Head,
                Snapshot.ParentSessionId,Snapshot.ParentRequestId,native,call,report,[ask,response,tool]));
        }
        Runtime=new(runtime,links);
        var final=turns[^1];
        if(RequiresCumulative)
        {
            var finalTool=Proof($"reviews/{Snapshot.RunId}/cumulative-turn-tool-result.json",new { role="cumulative-final-verdict",Snapshot.RunId,Snapshot.Head,Snapshot.ParentSessionId,Snapshot.ParentRequestId,
                sourceType=OfficialTransportFixture.Source,sessionId=links[^1].SessionId,requestId=links[^1].RequestId,
                providerCallId=runtime[^1].ProviderCallId,relatedProviderCallIds=runtime.Select(x=>x.ProviderCallId).ToArray(),fullVisibleVerdict=FullReport });
            var finalLink=new ReviewObservedReviewerTurnLink(runtime[^1].ProviderCallId,OfficialTransportFixture.Source,
                links[^1].SessionId,links[^1].RequestId,finalTool);
            var callReportEntry=Proof($"reviews/{Snapshot.RunId}/cumulative-call-report-entry.json",new { role="provider-call-report",Snapshot.RunId,Snapshot.Head,Snapshot.ParentSessionId,Snapshot.ParentRequestId,
                sourceType=OfficialTransportFixture.Source,sessionId=finalLink.SessionId,requestId=finalLink.RequestId,providerCallId=runtime[^1].ProviderCallId,fullVisibleReport=reports[^1] });
            Cumulative=new(finalLink,runtime.Select(x=>x.ProviderCallId).ToArray(),FullReport,reports[^1],callReportEntry);
            final=final with { FullVisibleVerdict=FullReport,Evidence=[..final.Evidence,finalTool,callReportEntry] };
            switch(name)
            {
                case "missing-cumulative-turn": Cumulative=null;break;
                case "call-turn-missing-cumulative-section": Cumulative=Cumulative with { Turn=links[^1] };final=turns[^1];break;
                case "cumulative-report-omission": Cumulative=Cumulative with { FullVisibleVerdict=reports[^1] };final=final with { FullVisibleVerdict=reports[^1] };break;
                case "cumulative-coverage-omission": Cumulative=Cumulative with { RelatedProviderCallIds=runtime.Skip(1).Select(x=>x.ProviderCallId).ToArray() };break;
                case "final-call-report-omission": Cumulative=Cumulative with { FinalCallVisibleReport="" };break;
            }
            turns[^1]=final;
        }
        Page=new(0,100,turns.Count,turns.Count,turns.ToArray(),Proof("official/cumulative-readback-page.json",new { workspace=OfficialTransportFixture.Workspace,
            sourceType=OfficialTransportFixture.Source,offset=0,limit=100,totalCount=turns.Count,turns=turns.ToArray() }));
        Completion=new(Snapshot.Head,Snapshot.RunId,OfficialTransportFixture.Source,OfficialTransportFixture.Model,OfficialTransportFixture.Effort,
            final.SessionId,final.RequestId,Snapshot.ParentSessionId,Snapshot.ParentRequestId,FullReport,runtime.Select(x=>x.ProviderCallId).ToArray(),true);
    }
    public CumulativeAuditObservation Expected()=>new(ExpectedStatus,[new("ReadReviewerOwnTurn",Snapshot,FullReport,Receipts.ToArray())],
        [new(Snapshot,Receipts.ToArray())],RequiresCumulative ? [new(Snapshot,Receipts.ToArray())] : [],
        [new(OfficialTransportFixture.Workspace,Snapshot.RunId,OfficialTransportFixture.Source,0,100)],Runtime,Cumulative,
        DeniedRead ? [] : [Page],ExpectedStatus=="accepted" ? Completion : null,Proofs,[],[]);
}

public sealed class CumulativeRuntimeSource(CumulativeAuditFixture fixture):IReviewProviderRuntimeEvidenceSource
{
    public List<RuntimePurposeCall> Calls { get; }=[];
    public Task<ReviewProviderRuntimeObservation> ObserveAsync(ReviewSnapshot snapshot,IReadOnlyList<StoredArtifact> providerReceipts,CancellationToken cancellationToken=default)
    {Calls.Add(new(snapshot,providerReceipts.ToArray()));return Task.FromResult(fixture.Runtime);}
}
public sealed class CumulativeVerdictSource(CumulativeAuditFixture fixture):IReviewCumulativeVerdictEvidenceSource
{
    public List<CumulativeEvidenceCall> Calls { get; }=[];
    public Task<ReviewObservedCumulativeVerdict?> ObserveAsync(ReviewSnapshot snapshot,IReadOnlyList<StoredArtifact> providerReceipts,CancellationToken cancellationToken=default)
    {Calls.Add(new(snapshot,providerReceipts.ToArray()));return Task.FromResult(fixture.Cumulative);}
}
public sealed class CumulativePurposeSource(CumulativeAuditFixture fixture):IReviewOfficialPurposeInvoker
{
    public List<ReviewOfficialTurnPageRequest> Calls { get; }=[];public List<ReviewOfficialTurnPage> Returned { get; }=[];
    public Task<ReviewOfficialInstallationObservation> ObserveInstallationAsync(ReviewOfficialInstallationRequest request,CancellationToken cancellationToken=default)
        =>throw new InvalidOperationException("Cumulative read-back must not bootstrap or launch.");
    public Task<ReviewOfficialTurnPage> ReadReviewerTurnsPageAsync(ReviewOfficialTurnPageRequest request,CancellationToken cancellationToken=default)
    {Calls.Add(request);if(fixture.DeniedRead)throw new ReviewException("audit-denied","Scripted denied read, never retried.");Returned.Add(fixture.Page);return Task.FromResult(fixture.Page);}
}
// MOCK adapter, with a scripted terminal outcome. It invokes the intended typed dependencies
// and validates no production policy. Native binding must derive the SAME results from these inputs.
public sealed class ScriptedCumulativeAuditAdapter(CumulativeAuditFixture fixture,IReviewProviderRuntimeEvidenceSource runtime,
    IReviewCumulativeVerdictEvidenceSource cumulative,IReviewOfficialPurposeInvoker purpose):IReviewAuditTransport
{
    public List<AuditAdapterCall> Calls { get; }=[];
    public Task<ReviewAuditBootstrapEvidence> ObserveBootstrapAsync(ReviewSnapshot snapshot,CancellationToken cancellationToken=default)
        =>throw new InvalidOperationException("Cumulative fixture must not bootstrap.");
    public async Task<ReviewAuditCompletionEvidence> ReadReviewerOwnTurnAsync(ReviewSnapshot snapshot,string expectedFullVerdict,IReadOnlyList<StoredArtifact> receipts,CancellationToken cancellationToken=default)
    {
        Calls.Add(new("ReadReviewerOwnTurn",snapshot,expectedFullVerdict,receipts.ToArray()));_=await runtime.ObserveAsync(snapshot,receipts,cancellationToken);
        if(fixture.RequiresCumulative)_=await cumulative.ObserveAsync(snapshot,receipts,cancellationToken);
        _=await purpose.ReadReviewerTurnsPageAsync(new(OfficialTransportFixture.Workspace,snapshot.RunId,OfficialTransportFixture.Source,0,100),cancellationToken);
        if(fixture.ExpectedStatus!="accepted")throw new ReviewException(fixture.ExpectedStatus,"Scripted cumulative refusal.");return fixture.Completion;
    }
}
public sealed class ScriptedCumulativeAuditCases:IReviewCumulativeAuditCaseSystem
{
    public async Task<CumulativeAuditObservation> ExecuteAsync(CumulativeAuditFixture fixture)
    {
        var runtime=new CumulativeRuntimeSource(fixture);var cumulative=new CumulativeVerdictSource(fixture);var purpose=new CumulativePurposeSource(fixture);
        var adapter=new ScriptedCumulativeAuditAdapter(fixture,runtime,cumulative,purpose);ReviewAuditCompletionEvidence? completion=null;string status;
        try{completion=await adapter.ReadReviewerOwnTurnAsync(fixture.Snapshot,fixture.FullReport,fixture.Receipts);status="accepted";}
        catch(ReviewException error){status=error.Code;}
        return new(status,adapter.Calls,runtime.Calls,cumulative.Calls,purpose.Calls,fixture.Runtime,fixture.Cumulative,purpose.Returned,completion,fixture.Proofs,[],[]);
    }
}
public static class ReviewCumulativeAuditCases
{
    public static readonly string[] Names=["single-call-existing-contract","two-call-cumulative","repeated-boundaries-cumulative","missing-cumulative-turn",
        "call-turn-missing-cumulative-section","cumulative-report-omission","cumulative-coverage-omission","final-call-report-omission","recovery-read-only","denied-read-no-retry"];
    public static IEnumerable<object[]> Data=>Names.Select(x=>new object[]{x});
    public static JsonNode Json(CumulativeAuditObservation value)=>JsonSerializer.SerializeToNode(value)!;
    public static void AssertContract(CumulativeAuditFixture fixture,JsonNode actual)=>Assert.Equal(
        new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(fixture.Expected())),new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(actual)));
}
[Trait("Gate","Mock")]
public sealed class ReviewCumulativeAuditMockTests
{
    [Theory,MemberData(nameof(ReviewCumulativeAuditCases.Data),MemberType=typeof(ReviewCumulativeAuditCases))]
    public async Task CorrectTypedCumulativeMock(string name)
    {
        var fixture=new CumulativeAuditFixture(name);var actual=await new ScriptedCumulativeAuditCases().ExecuteAsync(fixture);
        ReviewCumulativeAuditCases.AssertContract(fixture,ReviewCumulativeAuditCases.Json(actual));
    }
    public static IEnumerable<object[]> NegativeControls
    {
        get{foreach(var name in ReviewCumulativeAuditCases.Names)
            foreach(var leaf in ReviewOfficialTransportCases.Leaves(ReviewCumulativeAuditCases.Json(new CumulativeAuditFixture(name).Expected())))yield return[name,leaf.Label,leaf.Path];}
    }
    [Theory,MemberData(nameof(NegativeControls))]
    public async Task EveryCumulativeIdentityReportCoverageAndInteractionFieldRejectsMutation(string name,string field,string[] path)
    {
        Assert.False(string.IsNullOrWhiteSpace(field));var fixture=new CumulativeAuditFixture(name);
        var actual=ReviewCumulativeAuditCases.Json(await new ScriptedCumulativeAuditCases().ExecuteAsync(fixture));
        ReviewOfficialTransportCases.Mutate(actual,path);
        Assert.NotNull(Record.Exception(()=>ReviewCumulativeAuditCases.AssertContract(fixture,actual)));
    }
}
