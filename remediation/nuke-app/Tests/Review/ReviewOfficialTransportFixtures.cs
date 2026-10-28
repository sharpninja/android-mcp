using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Review;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed record RuntimePurposeCall(ReviewSnapshot Snapshot, IReadOnlyList<StoredArtifact> Receipts);
public sealed record AuditAdapterCall(string Operation, ReviewSnapshot Snapshot, string? FullVisibleVerdict,
    IReadOnlyList<StoredArtifact> Receipts);
public sealed record OfficialTransportObservation(string Status, IReadOnlyList<AuditAdapterCall> AdapterCalls,
    IReadOnlyList<ReviewOfficialInstallationRequest> InstallationCalls,
    IReadOnlyList<ReviewOfficialTurnPageRequest> PageCalls, IReadOnlyList<RuntimePurposeCall> RuntimeCalls,
    ReviewOfficialInstallationObservation? Installation, IReadOnlyList<ReviewOfficialTurnPage> Pages,
    ReviewProviderRuntimeObservation? ProviderRuntime, ReviewAuditBootstrapEvidence? Bootstrap,
    ReviewAuditCompletionEvidence? Completion, IReadOnlyDictionary<string,byte[]> FullProofBytes,
    IReadOnlyList<string> PrivilegedMutationCalls, IReadOnlyList<string> ProviderStartCalls);
public interface IOfficialTransportCaseSystem
{
    Task<OfficialTransportObservation> ExecuteAsync(OfficialTransportFixture fixture);
}

// Immutable fixtures are the SAME inputs for scripted mocks and the future real transport binding.
// Native objects and the existing shared canonical/hash implementations build every JSON proof.
public sealed class OfficialTransportFixture
{
    public const string Workspace = "/tmp/review-official-workspace";
    public const string Head = "0123456789abcdef0123456789abcdef01234567";
    public const string Run = "official-review-fixture";
    public const string ParentSession = "Codex-20261028T000000Z-parent";
    public const string ParentRequest = "req-20261028T000001Z-parent";
    public const string Source = "ClaudeCode";
    public const string Model = "claude-opus-5-5";
    public const string Effort = "xhigh";
    public const string FullReport = "Finding one: all visible details.\nFinding two: Unicode Δ and café.\nAGREE with every visible finding retained.";
    public string Name { get; }
    public bool IsBootstrap { get; }
    public string ExpectedStatus { get; }
    public ReviewSnapshot Snapshot { get; } = new(Run,ParentSession,ParentRequest,Head,
        "https://github.com/example/project/pull/1",["tests/Unit.csproj"],["tests/Scope.csproj"],["FR-BUILD-001"],[]);
    public Dictionary<string,byte[]> Proofs { get; } = new(StringComparer.Ordinal);
    public List<StoredArtifact> Receipts { get; } = [];
    public ReviewOfficialInstallationObservation Installation { get; private set; }
    public List<ReviewOfficialTurnPage> Pages { get; } = [];
    public ReviewProviderRuntimeObservation ProviderRuntime { get; private set; }
    public ReviewAuditBootstrapEvidence Bootstrap { get; }
    public ReviewAuditCompletionEvidence Completion { get; }
    public string? PurposeError { get; private set; }
    public int? ErrorPageOffset { get; private set; }

    private StoredArtifact Proof(string path,object value)
    {
        var bytes=new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(value));
        Proofs.Add(path,bytes);
        return new(path,ContentHash.Sha256(bytes),bytes.Length,"application/json");
    }
    public OfficialTransportFixture(string name)
    {
        Name=name;
        IsBootstrap=name is "bootstrap-ready" or "missing-installation" or "manifest-hash-mismatch" or "package-identity-mismatch"
            or "claude-code-source" or "claude-alias-source" or "unrestricted-tool-policy" or "queued-write-drain"
            or "transport-unavailable" or "missing-tool-discovery" or "configured-model-only" or "configured-effort-only"
            or "installation-wrong-workspace" or "installation-wrong-head" or "config-hash-mismatch" or "runtime-receipt-hash-mismatch";
        ExpectedStatus=name is "bootstrap-ready" or "claude-code-source" ? "ready-to-launch"
            : name is "turn-ready" or "paginated-turn" ? "accepted" : name=="denied-write-not-retried" ? "audit-denied"
            : name=="unavailable-page" ? "audit-unavailable" : "audit-unverified";
        var manifest=Proof("official/manifest.json",new { name="mcpserver",version="1.121.0",repository="https://github.com/sharpninja/mcpserver-claude-code-plugin" });
        var runtime=Proof("official/runtime.json",new { package="SharpNinja.McpServer.Repl",version="1.4.41-recovery.b53ce6b8",pluginCaller="Codex",workspace=Workspace,head=Head });
        var config=Proof("official/config-projection.json",new { privatePath="/approved/private-review-mcp.json",approvedSha256=new string('a',64),workspace=Workspace,head=Head,projectFilesReadOnly=true });
        var discovery=Proof("official/tool-discovery.json",new { tools=new[]{"mcp__official__sessionlog_begin","mcp__official__sessionlog_append","mcp__official__sessionlog_complete"},scope=Workspace,arbitraryShellOrNetwork=false });
        var noDrain=Proof("official/read-only-purpose.json",new { observer="Codex",allowsQueuedMutation=false,allowsBootstrap=false,purpose="ReadReviewerTurnsPage" });
        Installation=new(Workspace,Head,"mcpserver-claude-code-plugin","mcpserver","1.121.0",manifest,runtime,config,discovery,noDrain,
            "/approved/private-review-mcp.json",new string('a',64),["mcp__official__sessionlog_begin","mcp__official__sessionlog_append","mcp__official__sessionlog_complete"],Source,Model,Effort);
        Bootstrap=new(Head,Source,Model,Effort,"mcpserver-claude-code-plugin","1.121.0",manifest.Sha256,
            Installation.PrivateMcpConfigurationPath,Installation.PrivateMcpConfigurationSha256,Installation.ReviewerAllowedTools,true,true,false);
        var observations=new List<ReviewObservedProviderRuntime>();
        var links=new List<ReviewObservedReviewerTurnLink>();
        var turns=new List<ReviewOfficialPersistedTurn>();
        for(var i=1;i<=3;i++)
        {
            var call=i.ToString("D8");
            var session=$"ClaudeCode-20261028T00000{i}Z-review";
            var request=$"req-20261028T00001{i}Z-review";
            var nativeSession=$"provider-native-{i}";
            var ask=Proof($"reviews/{Run}/calls/{call}/request.jsonl",new { runId=Run,callId=call,head=Head,parentSessionId=ParentSession,parentRequestId=ParentRequest });
            var report=i==3 ? FullReport : $"Partition {i}: every visible finding Δ.";
            var response=Proof($"reviews/{Run}/calls/{call}/response.visible.jsonl",new { type="system",model=Model,effort=Effort,session_id=nativeSession,visible=report,provider_call_id=call });
            var tool=Proof($"reviews/{Run}/calls/{call}/session-tool-result.json",new { sourceType=Source,sessionId=session,requestId=request,providerCallId=call });
            Receipts.Add(ask);Receipts.Add(response);
            observations.Add(new(call,nativeSession,Model,Effort,report,response));
            links.Add(new(call,Source,session,request,tool));
            turns.Add(new(Workspace,Source,session,request,"completed",Run,Head,ParentSession,ParentRequest,nativeSession,call,
                i==3 ? FullReport : $"Partition {i}: every visible finding Δ.",[ask,response,tool]));
        }
        ProviderRuntime=new(observations,links);
        Completion=new(Head,Run,Source,Model,Effort,turns[2].SessionId,turns[2].RequestId,ParentSession,ParentRequest,FullReport,["00000001","00000002","00000003"],true);
        var twoPages=name is "paginated-turn" or "unavailable-page";
        Pages.Add(new(0,100,twoPages ? 101 : 3,twoPages ? 100 : 3,twoPages ? turns.Take(2).ToArray() : turns.ToArray(),
            Proof("official/readback-page-0.json",new { workspace=Workspace,sourceType=Source,offset=0,limit=100,totalCount=twoPages ? 101 : 3,turns=twoPages ? turns.Take(2).ToArray() : turns.ToArray() })));
        if(twoPages) Pages.Add(new(100,100,101,1,turns.Skip(2).ToArray(),Proof("official/readback-page-100.json",
            new { workspace=Workspace,sourceType=Source,offset=100,limit=100,totalCount=101,turns=turns.Skip(2).ToArray() })));
        // Invalid source inputs are preserved verbatim as evidence. The mock scripts a refusal;
        // the future native binding must derive that SAME refusal from these exact typed inputs.
        switch(name)
        {
            case "missing-installation": case "transport-unavailable": PurposeError="audit-unverified";break;
            case "manifest-hash-mismatch": Installation=Installation with { ManifestProof=manifest with { Sha256=new string('b',64) } };break;
            case "installation-wrong-workspace": Installation=Installation with { WorkspacePath="/tmp/other-workspace" };break;
            case "installation-wrong-head": Installation=Installation with { Head=new string('f',40) };break;
            case "config-hash-mismatch": Installation=Installation with { PrivateMcpConfigurationSha256=new string('b',64) };break;
            case "runtime-receipt-hash-mismatch": Installation=Installation with { RuntimeIdentityProof=runtime with { Sha256=new string('b',64) } };break;
            case "package-identity-mismatch": Installation=Installation with { PackageIdentity="other-plugin" };break;
            case "claude-alias-source": Installation=Installation with { ReviewerSessionSource="Claude" };break;
            case "unrestricted-tool-policy": Installation=Installation with { ReviewerAllowedTools=["mcp__official__arbitrary_shell"] };break;
            case "queued-write-drain": Installation=Installation with { NoQueuedMutationProof=Proof("official/unsafe-drain.json",new { observer="Codex",allowsQueuedMutation=true }) };break;
            case "missing-tool-discovery": Installation=Installation with { ToolDiscoveryProof=discovery with { Length=0 } };break;
            case "configured-model-only": Installation=Installation with { RuntimeIdentityProof=Proof("official/config-model-only.json",new { requestedModel=Model,actualModel=(string?)null }) };break;
            case "configured-effort-only": Installation=Installation with { RuntimeIdentityProof=Proof("official/config-effort-only.json",new { requestedEffort=Effort,actualEffort=(string?)null }) };break;
            case "wrong-workspace": ChangeTurn(x=>x with { WorkspacePath="/tmp/other-workspace" });break;
            case "wrong-head": ChangeTurn(x=>x with { Head=new string('f',40) });break;
            case "wrong-run": ChangeTurn(x=>x with { RunId="other-run" });break;
            case "wrong-parent-session": ChangeTurn(x=>x with { ParentSessionId="Codex-20261028T000099Z-other" });break;
            case "wrong-parent-request": ChangeTurn(x=>x with { ParentRequestId="req-20261028T000099Z-other" });break;
            case "wrong-provider-native-session": ChangeTurn(x=>x with { ProviderNativeSessionId="other-provider-session" });break;
            case "wrong-provider-call": ChangeTurn(x=>x with { ProviderCallId="00000099" });break;
            case "wrong-session": ChangeTurn(x=>x with { SessionId="ClaudeCode-20261028T000099Z-other" });break;
            case "wrong-request": ChangeTurn(x=>x with { RequestId="req-20261028T000099Z-other" });break;
            case "pending-turn": ChangeTurn(x=>x with { Status="in_progress" });break;
            case "transcript-import": ChangeTurn(x=>x with { Evidence=[Proof("official/import-receipt.json",new { importedTranscript=true })] });break;
            case "missing-actual-effort": ProviderRuntime=ProviderRuntime with { Runtime=observations.Select((x,i)=>i==0 ? x with { ActualEffort="" } : x).ToArray() };break;
            case "wrong-actual-effort": ProviderRuntime=ProviderRuntime with { Runtime=observations.Select((x,i)=>i==0 ? x with { ActualEffort="high" } : x).ToArray() };break;
            case "response-receipt-hash-mismatch": ProviderRuntime=ProviderRuntime with { Runtime=observations.Select((x,i)=>i==0 ? x with { VisibleStreamProof=x.VisibleStreamProof with { Sha256=new string('b',64) } } : x).ToArray() };break;
            case "page-receipt-hash-mismatch": Pages[0]=Pages[0] with { ReadbackProof=Pages[0].ReadbackProof with { Sha256=new string('b',64) } };break;
            case "wrong-page-offset": Pages[0]=Pages[0] with { Offset=1 };break;
            case "wrong-page-limit": Pages[0]=Pages[0] with { Limit=99 };break;
            case "changed-verdict": ChangeTurn(x=>x with { FullVisibleVerdict="altered report" });break;
            case "missing-partition-turn": Pages[0]=Pages[0] with { Turns=turns.Skip(1).ToArray() };break;
            case "duplicate-partition-turn": Pages[0]=Pages[0] with { Turns=[..turns,turns[0]] };break;
            case "unavailable-page": ErrorPageOffset=100;break;
            case "conflicting-runtime": ProviderRuntime=ProviderRuntime with { Runtime=observations.Select((x,i)=>i==0 ? x with { ActualModel="claude-other" } : x).ToArray() };break;
            case "denied-write-not-retried": PurposeError="audit-denied";break;
        }
    }
    private void ChangeTurn(Func<ReviewOfficialPersistedTurn,ReviewOfficialPersistedTurn> change)
        => Pages[0]=Pages[0] with { Turns=Pages[0].Turns.Select((x,i)=>i==Pages[0].Turns.Count-1 ? change(x) : x).ToArray() };
    public OfficialTransportObservation Expected()
    {
        ReviewOfficialInstallationRequest[] installationCalls=IsBootstrap ? [new(Workspace,Head,Run,ParentSession,ParentRequest)] : [];
        ReviewOfficialTurnPageRequest[] pageCalls=IsBootstrap ? [] : Pages.Select((x,i)=>new ReviewOfficialTurnPageRequest(Workspace,Run,Source,i*100,100)).ToArray();
        if(!IsBootstrap && PurposeError is not null) pageCalls=[new(Workspace,Run,Source,0,100)];
        return new(ExpectedStatus,[new(IsBootstrap ? "ObserveBootstrap" : "ReadReviewerOwnTurn",Snapshot,IsBootstrap ? null : FullReport,IsBootstrap ? [] : Receipts.ToArray())],
            installationCalls,pageCalls,IsBootstrap ? [] : [new(Snapshot,Receipts.ToArray())],IsBootstrap && PurposeError is null ? Installation : null,
            IsBootstrap || PurposeError is not null ? [] : ErrorPageOffset.HasValue ? Pages.Take(1).ToArray() : Pages.ToArray(),IsBootstrap ? null : ProviderRuntime,
            ExpectedStatus=="ready-to-launch" ? Bootstrap : null,ExpectedStatus=="accepted" ? Completion : null,Proofs,[],[]);
    }
}
