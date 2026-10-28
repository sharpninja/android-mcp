using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Review;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed class RecordingOfficialPurposeInvoker(OfficialTransportFixture fixture) : IReviewOfficialPurposeInvoker
{
    public List<ReviewOfficialInstallationRequest> InstallationCalls { get; }=[];
    public List<ReviewOfficialTurnPageRequest> PageCalls { get; }=[];
    public ReviewOfficialInstallationObservation? ReturnedInstallation { get; private set; }
    public List<ReviewOfficialTurnPage> ReturnedPages { get; }=[];
    public List<string> PrivilegedMutationCalls { get; }=[];
    public Task<ReviewOfficialInstallationObservation> ObserveInstallationAsync(ReviewOfficialInstallationRequest request,CancellationToken cancellationToken=default)
    {
        InstallationCalls.Add(request);
        if(fixture.PurposeError is not null) throw new ReviewException(fixture.PurposeError,"Scripted official purpose failure.");
        ReturnedInstallation=fixture.Installation;return Task.FromResult(fixture.Installation);
    }
    public Task<ReviewOfficialTurnPage> ReadReviewerTurnsPageAsync(ReviewOfficialTurnPageRequest request,CancellationToken cancellationToken=default)
    {
        PageCalls.Add(request);
        if(fixture.PurposeError is not null) throw new ReviewException(fixture.PurposeError,"Scripted denied readback.");
        if(request.Offset==fixture.ErrorPageOffset) throw new ReviewException("audit-unavailable","Scripted unavailable page.");
        var page=fixture.Pages[request.Offset/100];ReturnedPages.Add(page);return Task.FromResult(page);
    }
}
public sealed class RecordingRuntimeEvidenceSource(OfficialTransportFixture fixture) : IReviewProviderRuntimeEvidenceSource
{
    public List<RuntimePurposeCall> Calls { get; }=[];
    public ReviewProviderRuntimeObservation? Returned { get; private set; }
    public Task<ReviewProviderRuntimeObservation> ObserveAsync(ReviewSnapshot snapshot,IReadOnlyList<StoredArtifact> providerReceipts,CancellationToken cancellationToken=default)
    {
        Calls.Add(new(snapshot,providerReceipts.ToArray()));Returned=fixture.ProviderRuntime;return Task.FromResult(fixture.ProviderRuntime);
    }
}

// MOCK of the intended audit-transport adapter. It invokes the real typed collaborator interfaces,
// then scripts the fixture's terminal response. It implements no production validation policy.
public sealed class ScriptedOfficialAuditAdapter(IReviewOfficialPurposeInvoker purpose,
    IReviewProviderRuntimeEvidenceSource runtime,OfficialTransportFixture fixture) : IReviewAuditTransport
{
    public List<AuditAdapterCall> Calls { get; }=[];
    public async Task<ReviewAuditBootstrapEvidence> ObserveBootstrapAsync(ReviewSnapshot snapshot,CancellationToken cancellationToken=default)
    {
        Calls.Add(new("ObserveBootstrap",snapshot,null,[]));
        _=await purpose.ObserveInstallationAsync(new(OfficialTransportFixture.Workspace,snapshot.Head,snapshot.RunId,snapshot.ParentSessionId,snapshot.ParentRequestId),cancellationToken);
        if(fixture.ExpectedStatus!="ready-to-launch") throw new ReviewException(fixture.ExpectedStatus,"Scripted bootstrap refusal.");
        return fixture.Bootstrap;
    }
    public async Task<ReviewAuditCompletionEvidence> ReadReviewerOwnTurnAsync(ReviewSnapshot snapshot,string expectedFullVerdict,
        IReadOnlyList<StoredArtifact> receipts,CancellationToken cancellationToken=default)
    {
        Calls.Add(new("ReadReviewerOwnTurn",snapshot,expectedFullVerdict,receipts.ToArray()));
        _=await runtime.ObserveAsync(snapshot,receipts,cancellationToken);
        for(var i=0;i<fixture.Pages.Count;i++)
            _=await purpose.ReadReviewerTurnsPageAsync(new(OfficialTransportFixture.Workspace,snapshot.RunId,OfficialTransportFixture.Source,i*100,100),cancellationToken);
        if(fixture.ExpectedStatus!="accepted") throw new ReviewException(fixture.ExpectedStatus,"Scripted turn refusal.");
        return fixture.Completion;
    }
}
public sealed class ScriptedOfficialTransportCases : IOfficialTransportCaseSystem
{
    public async Task<OfficialTransportObservation> ExecuteAsync(OfficialTransportFixture fixture)
    {
        var purpose=new RecordingOfficialPurposeInvoker(fixture);var runtime=new RecordingRuntimeEvidenceSource(fixture);
        var adapter=new ScriptedOfficialAuditAdapter(purpose,runtime,fixture);
        IReviewAuditTransport intendedBoundary=adapter;
        ReviewAuditBootstrapEvidence? bootstrap=null;ReviewAuditCompletionEvidence? completion=null;string status;
        try
        {
            if(fixture.IsBootstrap){bootstrap=await intendedBoundary.ObserveBootstrapAsync(fixture.Snapshot);status="ready-to-launch";}
            else{completion=await intendedBoundary.ReadReviewerOwnTurnAsync(fixture.Snapshot,OfficialTransportFixture.FullReport,fixture.Receipts);status="accepted";}
        }
        catch(ReviewException error){status=error.Code;}
        return new(status,adapter.Calls,purpose.InstallationCalls,purpose.PageCalls,runtime.Calls,purpose.ReturnedInstallation,
            purpose.ReturnedPages,runtime.Returned,bootstrap,completion,fixture.Proofs,purpose.PrivilegedMutationCalls,[]);
    }
}
public static class ReviewOfficialTransportCases
{
    public static readonly string[] Names=
    [
        "bootstrap-ready","missing-installation","manifest-hash-mismatch","package-identity-mismatch",
        "claude-code-source","claude-alias-source","unrestricted-tool-policy","queued-write-drain",
        "transport-unavailable","missing-tool-discovery","configured-model-only","configured-effort-only",
        "installation-wrong-workspace","installation-wrong-head","config-hash-mismatch","runtime-receipt-hash-mismatch",
        "turn-ready","wrong-workspace","wrong-session","wrong-request","pending-turn","transcript-import",
        "wrong-head","wrong-run","wrong-parent-session","wrong-parent-request","wrong-provider-native-session","wrong-provider-call",
        "missing-actual-effort","changed-verdict","missing-partition-turn","duplicate-partition-turn",
        "wrong-actual-effort","response-receipt-hash-mismatch","page-receipt-hash-mismatch","wrong-page-offset","wrong-page-limit",
        "paginated-turn","unavailable-page","conflicting-runtime","denied-write-not-retried"
    ];
    public static IEnumerable<object[]> Data=>Names.Select(x=>new object[]{x});
    public static JsonNode Json(OfficialTransportObservation observation)=>JsonSerializer.SerializeToNode(observation)!;
    public static void AssertContract(OfficialTransportFixture fixture,JsonNode actual)
        =>Assert.Equal(new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(fixture.Expected())),
            new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(actual)));

    public static IEnumerable<(string Label,string[] Path)> Leaves(JsonNode node,string[]? prefix=null)
    {
        prefix??=[];
        if(node is JsonObject obj && obj.Count>0)
        {
            foreach(var(key,value) in obj)
                if(value is null) yield return(string.Join("/",prefix.Append(key)),[..prefix,key]);
                else foreach(var leaf in Leaves(value,[..prefix,key])) yield return leaf;
        }
        else if(node is JsonArray array && array.Count>0)
        {
            for(var i=0;i<array.Count;i++)
                if(array[i] is null) yield return(string.Join("/",prefix.Append(i.ToString())),[..prefix,i.ToString()]);
                else foreach(var leaf in Leaves(array[i]!,[..prefix,i.ToString()])) yield return leaf;
        }
        else yield return(string.Join("/",prefix),prefix);
    }
    public static void Mutate(JsonNode node,string[] path)
    {
        for(var i=0;i<path.Length-1;i++) node=node is JsonArray array ? array[int.Parse(path[i])]! : node[path[i]]!;
        var last=path[^1];
        // Every scalar/null/empty collection and every preserved proof-byte string changes separately.
        var wrong=JsonValue.Create("wrong-field");
        if(node is JsonArray items) items[int.Parse(last)]=wrong;else node[last]=wrong;
    }
}
[Trait("Gate","Mock")]
public sealed class ReviewOfficialTransportMockTests
{
    [Theory,MemberData(nameof(ReviewOfficialTransportCases.Data),MemberType=typeof(ReviewOfficialTransportCases))]
    public async Task CorrectTypedMock(string name)
    {
        var fixture=new OfficialTransportFixture(name);var actual=await new ScriptedOfficialTransportCases().ExecuteAsync(fixture);
        ReviewOfficialTransportCases.AssertContract(fixture,ReviewOfficialTransportCases.Json(actual));
        Assert.Empty(actual.PrivilegedMutationCalls);Assert.Empty(actual.ProviderStartCalls);
        for(var i=0;i<actual.PageCalls.Count;i++)
        {
            var call=actual.PageCalls[i];
            Assert.Equal(OfficialTransportFixture.Workspace,call.WorkspacePath);Assert.Equal(OfficialTransportFixture.Run,call.RunId);
            Assert.Equal("ClaudeCode",call.ReviewerSessionSource);Assert.Equal(100,call.Limit);Assert.Equal(i*100,call.Offset);
        }
        if(actual.Status=="accepted")
        {
            Assert.Equal(OfficialTransportFixture.FullReport,actual.Completion!.FullVisibleVerdict);
            Assert.Equal(new[]{"00000001","00000002","00000003"},actual.Completion.RelatedCallIds);
            Assert.Equal(actual.ProviderRuntime!.ReviewerTurns.Select(x=>(x.SessionId,x.RequestId)),
                actual.Pages.SelectMany(x=>x.Turns).Select(x=>(x.SessionId,x.RequestId)));
            Assert.Equal(actual.ProviderRuntime.Runtime.Select(x=>x.FullVisibleReport),actual.Pages.SelectMany(x=>x.Turns).Select(x=>x.FullVisibleVerdict));
            foreach(var receipt in actual.Pages.SelectMany(x=>x.Turns).SelectMany(x=>x.Evidence))
            {
                var bytes=actual.FullProofBytes[receipt.RelativePath];Assert.Equal(receipt.Sha256,ContentHash.Sha256(bytes));Assert.Equal(receipt.Length,bytes.LongLength);
            }
        }
    }
    public static IEnumerable<object[]> NegativeControls
    {
        get
        {
            foreach(var name in ReviewOfficialTransportCases.Names)
            foreach(var leaf in ReviewOfficialTransportCases.Leaves(ReviewOfficialTransportCases.Json(new OfficialTransportFixture(name).Expected())))
                yield return[name,leaf.Label,leaf.Path];
        }
    }
    [Theory,MemberData(nameof(NegativeControls))]
    public async Task EveryTypedProofAndInteractionFieldRejectsItsOwnMutation(string name,string field,string[] path)
    {
        Assert.False(string.IsNullOrWhiteSpace(field));var fixture=new OfficialTransportFixture(name);
        var actual=ReviewOfficialTransportCases.Json(await new ScriptedOfficialTransportCases().ExecuteAsync(fixture));
        ReviewOfficialTransportCases.Mutate(actual,path);
        Assert.NotNull(Record.Exception(()=>ReviewOfficialTransportCases.AssertContract(fixture,actual)));
    }
}
