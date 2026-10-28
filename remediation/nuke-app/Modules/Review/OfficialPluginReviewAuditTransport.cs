using System.Text.Json;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Modules.Review;

/// <summary>Read-only normalization over a trusted official-purpose binding and captured runtime source.
/// Neither collaborator is replaced by caller assertions. No process, shell, network or mutation API.</summary>
public sealed class OfficialPluginReviewAuditTransport(IReviewOfficialPurposeInvoker purpose,
    IReviewProviderRuntimeEvidenceSource runtime, IArtifactStore store, ICanonicalJson canonical,
    string workspacePath) : IReviewAuditTransport
{
    private const string Source = ReviewAuditPolicy.ReviewerAgent;
    private const int PageSize = 100;
    private const string Package = ReviewAuditPolicy.PluginName;
    private const string Repository = "https://github.com/sharpninja/mcpserver-claude-code-plugin";

    public async Task<ReviewAuditBootstrapEvidence> ObserveBootstrapAsync(ReviewSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        Require(Path.IsPathRooted(workspacePath), "Explicit workspace is required.");
        var observed = await purpose.ObserveInstallationAsync(new(workspacePath,snapshot.Head,snapshot.RunId,
            snapshot.ParentSessionId,snapshot.ParentRequestId),cancellationToken);
        Require(observed.WorkspacePath == workspacePath && observed.Head == snapshot.Head,
            "Official installation scope or head differs.");
        Require(observed.PackageIdentity == Package && observed.ManifestName == "mcpserver" &&
            !string.IsNullOrWhiteSpace(observed.Version) && observed.ReviewerSessionSource == Source &&
            observed.RequestedModel == ReviewLaunchService.Model && observed.RequestedEffort == ReviewLaunchService.Effort,
            "Official installation identity or exact requested capability differs.");
        Require(Path.IsPathRooted(observed.PrivateMcpConfigurationPath) && HashShape(observed.PrivateMcpConfigurationSha256),
            "Approved private configuration pin is absent.");
        Require(observed.ReviewerAllowedTools.Count > 0 && observed.ReviewerAllowedTools.Distinct(StringComparer.Ordinal).Count() == observed.ReviewerAllowedTools.Count &&
            observed.ReviewerAllowedTools.All(x => Regex.IsMatch(x,"^mcp__[A-Za-z0-9_-]+__sessionlog_[A-Za-z0-9_]+$")),
            "Reviewer tool policy is not limited to scoped session-log tools.");

        using var manifest = await JsonProofAsync(observed.ManifestProof,cancellationToken);
        Require(Text(manifest.RootElement,"name") == observed.ManifestName && Text(manifest.RootElement,"version") == observed.Version &&
            Text(manifest.RootElement,"repository") == Repository,"Installed manifest does not prove the official package identity.");
        using var executable = await JsonProofAsync(observed.RuntimeIdentityProof,cancellationToken);
        Require(Text(executable.RootElement,"package") == "SharpNinja.McpServer.Repl" && !string.IsNullOrWhiteSpace(Text(executable.RootElement,"version")) &&
            Text(executable.RootElement,"pluginCaller") == "Codex" && Text(executable.RootElement,"workspace") == workspacePath &&
            Text(executable.RootElement,"head") == snapshot.Head,"Official observer runtime identity or scope is absent.");
        using var config = await JsonProofAsync(observed.ScopedConfigurationProof,cancellationToken);
        Require(Text(config.RootElement,"privatePath") == observed.PrivateMcpConfigurationPath &&
            StringComparer.OrdinalIgnoreCase.Equals(Text(config.RootElement,"approvedSha256"),observed.PrivateMcpConfigurationSha256) &&
            Text(config.RootElement,"workspace") == workspacePath && Text(config.RootElement,"head") == snapshot.Head &&
            Boolean(config.RootElement,"projectFilesReadOnly"),"Private configuration approval or read-only project policy differs.");
        using var discovery = await JsonProofAsync(observed.ToolDiscoveryProof,cancellationToken);
        Require(Text(discovery.RootElement,"scope") == workspacePath && !Boolean(discovery.RootElement,"arbitraryShellOrNetwork") &&
            Strings(discovery.RootElement,"tools").ToHashSet(StringComparer.Ordinal).SetEquals(observed.ReviewerAllowedTools),
            "Discovered official tool capability differs from the approved scoped policy.");
        using var noDrain = await JsonProofAsync(observed.NoQueuedMutationProof,cancellationToken);
        Require(Text(noDrain.RootElement,"observer") == "Codex" && !Boolean(noDrain.RootElement,"allowsQueuedMutation") &&
            !Boolean(noDrain.RootElement,"allowsBootstrap") && Text(noDrain.RootElement,"purpose") == "ReadReviewerTurnsPage",
            "Observer purpose does not prove mutation and bootstrap isolation.");
        return new(snapshot.Head,Source,ReviewLaunchService.Model,ReviewLaunchService.Effort,Package,observed.Version,
            observed.ManifestProof.Sha256,observed.PrivateMcpConfigurationPath,observed.PrivateMcpConfigurationSha256,
            observed.ReviewerAllowedTools.ToArray(),true,true,false);
    }

    public async Task<ReviewAuditCompletionEvidence> ReadReviewerOwnTurnAsync(ReviewSnapshot snapshot,
        string expectedFullVerdict,IReadOnlyList<StoredArtifact> receipts,CancellationToken cancellationToken = default)
    {
        // Collect actual observed metadata and paginated read-back. No retry follows denied/unavailable purposes.
        var provider = await runtime.ObserveAsync(snapshot,receipts,cancellationToken);
        var turns = new List<ReviewOfficialPersistedTurn>();
        var offset = 0;
        int? total = null;
        do
        {
            var page = await purpose.ReadReviewerTurnsPageAsync(new(workspacePath,snapshot.RunId,Source,offset,PageSize),cancellationToken);
            Require(page.Offset == offset && page.Limit == PageSize && page.TotalCount >= 0 &&
                page.ReturnedSessionCount == Math.Min(PageSize,Math.Max(0,page.TotalCount-offset)) &&
                (total is null || total == page.TotalCount),"Official query pagination is truncated or inconsistent.");
            total ??= page.TotalCount;
            using var proof = await JsonProofAsync(page.ReadbackProof,cancellationToken);
            var body = proof.RootElement;
            Require(Text(body,"workspace") == workspacePath && Text(body,"sourceType") == Source &&
                Integer(body,"offset") == offset && Integer(body,"limit") == PageSize && Integer(body,"totalCount") == total &&
                canonical.Hash(Property(body,"turns")) == canonical.Hash(JsonSerializer.SerializeToElement(page.Turns)),
                "Persisted page proof does not bind its full normalized turn projection.");
            turns.AddRange(page.Turns);
            if ((long)offset + PageSize >= total.GetValueOrDefault()) break;
            Require(offset <= int.MaxValue-PageSize,"Official query offset cannot be represented by its installed API.");
            offset += PageSize;
        } while(true);

        var calls = ReviewAuditPolicy.CallIds(receipts);
        Require(calls.Count > 0 && provider.Runtime.Count == calls.Count && provider.ReviewerTurns.Count == calls.Count &&
            provider.Runtime.Select(x=>x.ProviderCallId).ToHashSet(StringComparer.Ordinal).SetEquals(calls) &&
            provider.ReviewerTurns.Select(x=>x.ProviderCallId).ToHashSet(StringComparer.Ordinal).SetEquals(calls) &&
            provider.ReviewerTurns.Select(x=>(x.SessionId,x.RequestId)).Distinct().Count() == calls.Count,
            "Actual runtime or scoped tool-result call coverage is missing or duplicated.");
        var own = turns.Where(x=>x.RunId == snapshot.RunId).ToArray();
        Require(own.Length == calls.Count && own.Select(x=>x.ProviderCallId).ToHashSet(StringComparer.Ordinal).SetEquals(calls),
            "Persisted reviewer turns do not cover every exact provider call.");
        foreach(var call in calls)
        {
            var observed = provider.Runtime.Single(x=>x.ProviderCallId == call);
            var link = provider.ReviewerTurns.Single(x=>x.ProviderCallId == call);
            var turn = own.Single(x=>x.ProviderCallId == call);
            Require(observed.ActualModel == ReviewLaunchService.Model && observed.ActualEffort == ReviewLaunchService.Effort &&
                !string.IsNullOrWhiteSpace(observed.ProviderNativeSessionId),"Actual model, effort or provider-native session is unverified.");
            var responsePath = $"/calls/{call}/response.visible.jsonl";
            var response = receipts.Where(x=>x.RelativePath.EndsWith(responsePath,StringComparison.Ordinal)).ToArray();
            Require(response.Length == 1 && response[0] == observed.VisibleStreamProof,
                "Observed runtime metadata is not bound to the exact immutable response receipt.");
            _ = await ReadProofAsync(observed.VisibleStreamProof,cancellationToken);
            using var toolProof = await JsonProofAsync(link.ToolResultProof,cancellationToken);
            Require(link.ReviewerSessionSource == Source && ValidSession(link.SessionId) && ValidRequest(link.RequestId) &&
                Text(toolProof.RootElement,"sourceType") == Source && Text(toolProof.RootElement,"sessionId") == link.SessionId &&
                Text(toolProof.RootElement,"requestId") == link.RequestId && Text(toolProof.RootElement,"providerCallId") == call,
                "Actual scoped tool result does not bind the reviewer's own exact turn.");
            Require(turn.WorkspacePath == workspacePath && turn.Head == snapshot.Head && turn.RunId == snapshot.RunId &&
                turn.ParentSessionId == snapshot.ParentSessionId && turn.ParentRequestId == snapshot.ParentRequestId &&
                turn.ReviewerSessionSource == Source && turn.SessionId == link.SessionId && turn.RequestId == link.RequestId &&
                turn.ProviderNativeSessionId == observed.ProviderNativeSessionId && turn.ProviderCallId == call && turn.Status == "completed" &&
                turn.FullVisibleVerdict == observed.FullVisibleReport,"Persisted own-turn identity, terminal state or complete report differs.");
            Require(turn.Evidence.Count > 0,"Persisted own-turn evidence is absent.");
            foreach(var evidence in turn.Evidence)
            {
                var bytes = await ReadProofAsync(evidence,cancellationToken);
                if(evidence.MediaType == "application/json")
                {
                    using var document = ParseJson(bytes);
                    Require(!document.RootElement.TryGetProperty("importedTranscript",out var imported) || imported.ValueKind != JsonValueKind.True,
                        "Transcript import does not prove a reviewer-authored turn.");
                }
            }
        }
        // Monotonic call IDs are minted by the existing launch service. The final synthesis is last.
        var final = own.Single(x=>x.ProviderCallId == calls.Order(StringComparer.Ordinal).Last());
        Require(final.FullVisibleVerdict == expectedFullVerdict,"Entire final visible verdict differs from persisted read-back.");
        return new(snapshot.Head,snapshot.RunId,Source,ReviewLaunchService.Model,ReviewLaunchService.Effort,final.SessionId,final.RequestId,
            snapshot.ParentSessionId,snapshot.ParentRequestId,expectedFullVerdict,calls,true);
    }

    private async Task<ReadOnlyMemory<byte>> ReadProofAsync(StoredArtifact proof,CancellationToken cancellationToken)
    {
        Require(HashShape(proof.Sha256) && proof.Length >= 0,"Proof hash or length is invalid.");
        ReadOnlyMemory<byte> bytes;
        try { bytes = await store.ReadAsync(proof.RelativePath,cancellationToken); }
        catch(Exception error) when(error is IOException or KeyNotFoundException or UnauthorizedAccessException)
        { throw Unavailable("Immutable proof bytes are unavailable."); }
        Require(bytes.Length == proof.Length && StringComparer.OrdinalIgnoreCase.Equals(ContentHash.Sha256(bytes.Span),proof.Sha256),
            "Immutable proof hash or length differs.");
        return bytes;
    }
    private async Task<JsonDocument> JsonProofAsync(StoredArtifact proof,CancellationToken cancellationToken)
    {
        Require(proof.MediaType == "application/json","Official purpose projection has an unsupported media type.");
        return ParseJson(await ReadProofAsync(proof,cancellationToken));
    }
    private JsonDocument ParseJson(ReadOnlyMemory<byte> bytes)
    {
        try
        {
            var result = JsonDocument.Parse(canonical.Serialize(bytes));
            if(result.RootElement.ValueKind != JsonValueKind.Object) { result.Dispose(); throw Unavailable("Official purpose projection must be an object."); }
            return result;
        }
        catch(Exception error) when(error is JsonException or CanonicalJsonException)
        { throw Unavailable("Official purpose projection is malformed."); }
    }
    private static JsonElement Property(JsonElement body,string name)
    {
        Require(body.ValueKind == JsonValueKind.Object && body.TryGetProperty(name,out _),"Official purpose projection field is missing.");
        return body.GetProperty(name);
    }
    private static string Text(JsonElement body,string name)
    {
        var field = Property(body,name);Require(field.ValueKind == JsonValueKind.String,"Official purpose projection text has a wrong type.");
        return field.GetString()!;
    }
    private static bool Boolean(JsonElement body,string name)
    {
        var field = Property(body,name);Require(field.ValueKind is JsonValueKind.True or JsonValueKind.False,"Official purpose projection flag has a wrong type.");
        return field.GetBoolean();
    }
    private static int Integer(JsonElement body,string name)
    {
        var field = Property(body,name);Require(field.ValueKind == JsonValueKind.Number && field.TryGetInt32(out _),"Official purpose projection count has a wrong type.");
        return field.GetInt32();
    }
    private static IReadOnlyList<string> Strings(JsonElement body,string name)
    {
        var field = Property(body,name);Require(field.ValueKind == JsonValueKind.Array && field.EnumerateArray().All(x=>x.ValueKind == JsonValueKind.String),"Official tool discovery has a wrong type.");
        return field.EnumerateArray().Select(x=>x.GetString()!).ToArray();
    }
    private static bool HashShape(string value) => !string.IsNullOrEmpty(value) && Regex.IsMatch(value,"^[0-9a-fA-F]{64}$");
    private static bool ValidSession(string value) => !string.IsNullOrEmpty(value) && Regex.IsMatch(value,"^ClaudeCode-[0-9]{8}T[0-9]{6}Z-[a-z0-9-]+$");
    private static bool ValidRequest(string value) => !string.IsNullOrEmpty(value) && Regex.IsMatch(value,"^req-[0-9]{8}T[0-9]{6}Z-[a-z0-9-]+$");
    private static ReviewException Unavailable(string reason) => new("REVIEWER_AUDIT_UNAVAILABLE",reason);
    private static void Require(bool condition,string reason) { if(!condition) throw Unavailable(reason); }
}
