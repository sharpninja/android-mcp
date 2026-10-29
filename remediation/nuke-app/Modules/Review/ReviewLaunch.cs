using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Modules.Review;

public sealed record ReviewEvidenceUnit(string Key, string File, string FullSha256, int Start, int End, string Text);
public sealed record ReviewCoverageSignature(string? Key, int Start, int End, string BodySha256);

/// <summary>One owner, write-ahead receipts, no assistant budgets and no retry of uncertain execution.</summary>
public sealed class ReviewLaunchService(IArtifactStore store, IArtifactLeaseProvider leases,
    IClock clock, ICanonicalJson canonical, Func<ReadOnlyMemory<byte>, string> sha256,
    IReviewReadinessSource readiness, IReviewPrerequisiteSource prerequisites, IProcessRunner runner, IProcessCaptureReader captures,
    ReviewAssessService assessor, IReviewAudit audit)
{
    public const string Model = "claude-opus-5-5";
    public const string Effort = "xhigh";

    public Task<ReviewRunResult> LaunchAsync(PreparedReview prepared, ReviewCapability capability,
        string artifactRoot, string workingDirectory, CancellationToken cancellationToken = default)
        => ExecuteAsync(prepared, capability, artifactRoot, workingDirectory, false, cancellationToken);

    public Task<ReviewRunResult> ResumeAsync(PreparedReview prepared, ReviewCapability capability,
        string artifactRoot, string workingDirectory, CancellationToken cancellationToken = default)
        => ExecuteAsync(prepared, capability, artifactRoot, workingDirectory, true, cancellationToken);

    private async Task<ReviewRunResult> ExecuteAsync(PreparedReview prepared, ReviewCapability capability,
        string artifactRoot, string workingDirectory, bool resume, CancellationToken cancellationToken)
    {
        var snapshot = prepared.Snapshot;
        await using var lease = await leases.AcquireAsync("review-run/" + snapshot.RunId, cancellationToken);
        var ready = await readiness.ObserveAsync(snapshot.PullRequestUrl, cancellationToken);
        ReviewPrepareService.Require(ready.IsOpen && !ready.IsDraft, "PR_NOT_READY", "PR readiness changed; review is paused.");
        ReviewPrepareService.Require(ready.Head == snapshot.Head && ready.PullRequestUrl == snapshot.PullRequestUrl,
            "HEAD_MISMATCH", "PR head changed after preparation.");
        ReviewPrepareService.Require(prepared.Prerequisites is not null, "PREREQUISITE_UNVERIFIED", "Prepared review has no trusted prerequisite evidence.");
        var trusted = await prerequisites.ObserveAsync(snapshot, cancellationToken);
        ReviewPrepareService.VerifyPrerequisites(snapshot, trusted);
        ReviewPrepareService.Require(canonical.Hash(JsonSerializer.SerializeToElement(trusted)) == canonical.Hash(JsonSerializer.SerializeToElement(prepared.Prerequisites)),
            "PREREQUISITE_UNVERIFIED", "Trusted prerequisites changed after preparation.");
        ReviewPrepareService.Require(capability.Model == Model && capability.Effort == Effort,
            "CAPABILITY_UNVERIFIED", "Exact Opus 5.5 XHigh capability is required with no fallback.");
        var verification = await store.ReadAsync(capability.VerificationReceiptPath, cancellationToken);
        ReviewPrepareService.Require(sha256(verification) == capability.VerificationReceiptSha256,
            "CAPABILITY_UNVERIFIED", "Capability receipt hash differs.");
        using (var doc = JsonDocument.Parse(verification))
        {
            var r = doc.RootElement;
            ReviewPrepareService.Require(r.GetProperty("supported").GetBoolean() && r.GetProperty("model").GetString() == Model &&
                r.GetProperty("effort").GetString() == Effort && r.GetProperty("toolSha256").GetString() == capability.Tool.Sha256,
                "CAPABILITY_UNVERIFIED", "Pinned runtime does not prove exact model/effort support.");
        }
        var frozenPrompt = await store.ReadAsync(prepared.PromptPath, cancellationToken);
        ReviewPrepareService.Require(sha256(frozenPrompt) == prepared.PromptSha256,
            "EVIDENCE_HASH_MISMATCH", "Prepared prompt changed.");
        var manifestPath = $"reviews/{snapshot.RunId}/manifest.json";
        var manifest = await store.ReadAsync(manifestPath, cancellationToken);
        ReviewPrepareService.Require(sha256(manifest) == prepared.ManifestSha256, "EVIDENCE_HASH_MISMATCH", "Prepared manifest changed.");
        using (var doc = JsonDocument.Parse(manifest))
            ReviewPrepareService.Require(doc.RootElement.GetProperty("snapshot").GetRawText() ==
                JsonSerializer.SerializeToElement(snapshot).GetRawText() ||
                canonical.Hash(doc.RootElement.GetProperty("snapshot")) == canonical.Hash(JsonSerializer.SerializeToElement(snapshot)),
                "EVIDENCE_HASH_MISMATCH", "Prepared snapshot differs from the frozen manifest.");
        var bootstrap = await audit.VerifyReadyAsync(snapshot, cancellationToken);
        ReviewAuditPolicy.VerifyBootstrap(snapshot, bootstrap.Evidence);
        await VerifyAuditProofAsync(bootstrap, cancellationToken);
        var intent = $"reviews/{snapshot.RunId}/launch-intent.json";
        var receipts = new List<StoredArtifact>(prepared.Artifacts) { bootstrap.Proof };
        async Task<ReadOnlyMemory<byte>?> OptionalAsync(string path)
        {
            try { return await store.ReadAsync(path, cancellationToken); }
            catch (FileNotFoundException) { return null; }
        }
        void Retain(string path, ReadOnlyMemory<byte> bytes, string mediaType = "application/json")
            => receipts.Add(new(path, sha256(bytes), bytes.Length, mediaType));
        async Task<ReadOnlyMemory<byte>> SaveAsync<T>(string path, T value)
        {
            var bytes = canonical.Serialize(ReviewPrepareService.Json(value));
            receipts.Add(await store.WriteAsync(new(path, bytes, "application/json"), cancellationToken));
            return bytes;
        }
        var existingIntent = await OptionalAsync(intent);
        ReviewPrepareService.Require(resume == existingIntent.HasValue, "OUTCOME_UNCERTAIN",
            resume ? "No existing launch intent to recover." : "Launch already has an intent; recover instead of duplicating.");
        if (existingIntent is { } intentBytes)
        {
            using var parsed = JsonDocument.Parse(intentBytes);
            var root = parsed.RootElement;
            ReviewPrepareService.Require(root.GetProperty("Head").GetString() == snapshot.Head &&
                root.GetProperty("ManifestSha256").GetString() == prepared.ManifestSha256 &&
                root.GetProperty("PromptSha256").GetString() == prepared.PromptSha256,
                "EVIDENCE_HASH_MISMATCH", "Recovery intent differs from the reviewed snapshot.");
            Retain(intent, intentBytes);
        }
        else await SaveAsync(intent, new { snapshot.RunId, snapshot.Head, prepared.PromptSha256, prepared.ManifestSha256,
            snapshot.ParentSessionId, snapshot.ParentRequestId, auditBootstrapSha256 = bootstrap.Proof.Sha256, startedAt = clock.Now });
        var resultPath = $"reviews/{snapshot.RunId}/result.json";
        var existingResult = await OptionalAsync(resultPath);
        var units = new List<ReviewEvidenceUnit>();
        foreach (var item in snapshot.Evidence)
        {
            var bytes = await store.ReadAsync($"reviews/{snapshot.RunId}/input/{item.Path}", cancellationToken);
            ReviewPrepareService.Require(sha256(bytes) == item.Sha256, "EVIDENCE_HASH_MISMATCH", item.Path);
            var text = ReviewPrepareService.Utf8.GetString(bytes.Span);
            units.Add(new(item.Path, item.Path, item.Sha256, 0, text.EnumerateRunes().Count(), text));
        }
        long call = 0;
        var marker = "END_REVIEW_" + snapshot.RunId;
        var originalPrompt = ReviewPrepareService.Utf8.GetString(frozenPrompt.Span);
        var headerAt = originalPrompt.IndexOf("\nBEGIN EXACT EVIDENCE ", StringComparison.Ordinal);
        var header = headerAt < 0 ? originalPrompt : originalPrompt[..headerAt];
        async Task<ReviewRunResult> RunStageAsync(IReadOnlyList<ReviewEvidenceUnit> assigned, string prior, bool unified)
        {
            var accumulated = prior;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var callId = (++call).ToString("D8", System.Globalization.CultureInfo.InvariantCulture);
                var relative = $"reviews/{snapshot.RunId}/calls/{callId}";
                var body = new StringBuilder(header).Append("\nROOT HEAD: ").Append(snapshot.Head)
                    .Append("\nROOT MANIFEST SHA256: ").Append(prepared.ManifestSha256)
                    .Append("\nROOT RUN ID: ").Append(snapshot.RunId).Append("\nCALL ID: ").Append(callId)
                    .Append("\nPARENT SESSION: ").Append(snapshot.ParentSessionId).Append("\nPARENT REQUEST: ").Append(snapshot.ParentRequestId)
                    .Append("\nUse only the authorized scoped MCP session-log tools to author your own Claude turn. Include these parent/head/run/call IDs and the entire visible verdict, then complete it for read-back. Never impersonate another agent.\n")
                    .Append(unified ? "\nONE UNIFIED ASSESSMENT.\n" : "\nPARTIAL EVIDENCE PASS: no whole-design score. Preserve every finding and boundary question.\n")
                    .Append("Emit the final result as a standalone JSON object with report_complete=true and end_marker=\"")
                    .Append(marker).Append("\" only when the assigned assessment is complete. Keep the marker inside JSON, never append text outside it. Progress messages remain separate and must be retained.\n");
                foreach (var unit in assigned)
                    body.Append("\nBEGIN EXACT RANGE ").Append(JsonSerializer.Serialize(unit with { Text = "" }))
                        .Append('\n').Append(unit.Text).Append("\nEND EXACT RANGE ").Append(unit.Key).Append('\n');
                if (accumulated.Length > 0)
                    body.Append("\nContinue the same immutable stage. Preserve every prior finding and remaining coverage.\nFULL VISIBLE PARTIAL OUTPUT, UNMODIFIED:\n").Append(accumulated);
                var promptBytes = ReviewPrepareService.Utf8.GetBytes(body.ToString());
                ReviewPrepareService.PublicPayload("prompt.txt", body.ToString());
                var promptPath = relative + "/prompt.txt";
                var absolutePrompt = ConfinedPath(artifactRoot, promptPath);
                var args = new[] { "--safe-mode", "--strict-mcp-config", "--mcp-config", bootstrap.Evidence.PrivateMcpConfigPath,
                    "--allowedTools", string.Join(',', bootstrap.Evidence.AllowedMcpTools), "--tools", "",
                    "--permission-mode", "dontAsk", "--no-session-persistence", "--print", "--output-format", "stream-json", "--verbose",
                    "--model", Model, "--effort", Effort };
                var requestPath = relative + "/request.jsonl";
                var priorRequest = resume ? await OptionalAsync(requestPath) : null;
                ReviewAssessment assessment;
                if (priorRequest is { } requestBytes)
                {
                    using var requestDocument = JsonDocument.Parse(requestBytes);
                    var request = requestDocument.RootElement;
                    ReviewPrepareService.Require(request.GetProperty("Head").GetString() == snapshot.Head &&
                        request.GetProperty("ManifestSha256").GetString() == prepared.ManifestSha256 &&
                        request.GetProperty("promptSha256").GetString() == sha256(promptBytes) &&
                        request.GetProperty("requestedModel").GetString() == Model && request.GetProperty("requestedEffort").GetString() == Effort &&
                        request.GetProperty("Tool").GetProperty("Sha256").GetString() == capability.Tool.Sha256,
                        "EVIDENCE_HASH_MISMATCH", "Recovered request differs from deterministic stage replay.");
                    var priorCompletion = await OptionalAsync(relative + "/completion.json");
                    ReviewPrepareService.Require(priorCompletion.HasValue, "OUTCOME_UNCERTAIN", "An existing request has no terminal completion; never duplicate it.");
                    using var completed = JsonDocument.Parse(priorCompletion!.Value);
                    var completion = completed.RootElement;
                    ReviewPrepareService.Require(completion.TryGetProperty("knownTerminal", out var known) && known.ValueKind == JsonValueKind.True &&
                        completion.GetProperty("requestSha256").GetString() == sha256(requestBytes),
                        "OUTCOME_UNCERTAIN", "The original terminal request binding is unproven.");
                    var priorVisible = await store.ReadAsync(relative + "/response.visible.jsonl", cancellationToken);
                    var priorPrompt = await store.ReadAsync(promptPath, cancellationToken);
                    ReviewPrepareService.Require(sha256(priorVisible) == completion.GetProperty("responseSha256").GetString() &&
                        sha256(priorPrompt) == sha256(promptBytes), "EVIDENCE_HASH_MISMATCH", "Recovered response or prompt bytes changed.");
                    Retain(requestPath, requestBytes, "application/x-ndjson");
                    Retain(promptPath, priorPrompt, "text/plain");
                    Retain(relative + "/completion.json", priorCompletion.Value);
                    Retain(relative + "/response.visible.jsonl", priorVisible, "application/x-ndjson");
                    assessment = assessor.Assess(priorVisible,
                        new(true, completion.GetProperty("ExitCode").GetInt32(), completion.GetProperty("Cancelled").GetBoolean()), marker);
                }
                else
                {
                ReviewPrepareService.Require(!existingResult.HasValue && !(await OptionalAsync(relative + "/completion.json")).HasValue,
                    "OUTCOME_UNCERTAIN", "Final or orphaned completion evidence cannot authorize a new provider call.");
                receipts.Add(await store.WriteAsync(new(promptPath, promptBytes, "text/plain"), cancellationToken));
                var savedRequest = await SaveAsync(requestPath, new { type = "request", at = clock.Now, snapshot.RunId,
                    snapshot.ParentSessionId, snapshot.ParentRequestId, snapshot.Head, prepared.ManifestSha256,
                    requestedModel = Model, requestedEffort = Effort, capability.Tool, arguments = args,
                    prompt = body.ToString(), promptSha256 = sha256(promptBytes), units = assigned.Select(x => x with { Text = "" }),
                    standardInputPath = absolutePrompt, unified, assistantCaps = (object?)null });
                ProcessResult process;
                try
                {
                    process = await runner.RunAsync(new(capability.Tool, args, workingDirectory,
                        new Dictionary<string, string> { ["CLAUDE_CODE_EFFORT_LEVEL"] = Effort },
                        ConfinedPath(artifactRoot, relative + "/private-raw"), absolutePrompt), cancellationToken);
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    await SaveAsync(relative + "/uncertain.json", new { type = "outcome-uncertain", error = error.GetType().Name,
                        action = "review launch", target = relative, noRetry = true });
                    return new("outcome-uncertain", accumulated, receipts, assigned.Select(x => x.Key).ToArray(), unified);
                }
                var stdout = await captures.ReadAsync(process, false, cancellationToken);
                var stderr = await captures.ReadAsync(process, true, cancellationToken);
                assessment = assessor.Assess(stdout, new(true, process.ExitCode, process.Cancelled), marker, stderr);
                // All event fields except hidden reasoning are retained. Private raw captures remain untouched.
                var visibleBytes = ReviewPrepareService.Utf8.GetBytes(string.Join('\n', assessment.VisibleLines) + "\n");
                receipts.Add(await store.WriteAsync(new(relative + "/response.visible.jsonl", visibleBytes, "application/x-ndjson"), cancellationToken));
                await SaveAsync(relative + "/completion.json", new { process.ExitCode, process.Cancelled, process.StartedAt, process.EndedAt,
                    assessment.Status, assessment.Reason, assessment.Models, assessment.Efforts,
                    knownTerminal = !process.Cancelled, requestSha256 = sha256(savedRequest),
                    privateStdoutPath = process.StandardOutputPath, privateStderrPath = process.StandardErrorPath,
                    rawStdoutSha256 = sha256(stdout), rawStderrSha256 = sha256(stderr),
                    responseSha256 = sha256(visibleBytes), hiddenExcluded = assessment.ContainsHiddenReasoning,
                    actualCoverage = assigned.Select(x => new { x.Key, x.File, x.FullSha256, x.Start, x.End }) });
                }
                if (assessment.Status is not ("paused-provider" or "outcome-uncertain"))
                {
                    ReviewPrepareService.Require(assessment.Models.Count > 0 && assessment.Models.All(x => x == Model),
                        "MODEL_MISMATCH", "Exact actual Opus 5.5 metadata is absent or differs; no fallback or retry.");
                    ReviewPrepareService.Require(assessment.Efforts.Count > 0 && assessment.Efforts.All(x => x == Effort),
                        "EFFORT_MISMATCH", "Actual XHigh effort metadata is absent or differs; no fallback or retry.");
                }
                if (assessment.Report.Length > 0) accumulated += (accumulated.Length > 0 ? "\n\n" : "") + assessment.Report;
                if (assessment.Status is "paused-provider" or "outcome-uncertain")
                {
                    await SaveAsync(relative + "/blocker.json", new { action = "review continuation", target = relative,
                        reason = assessment.Reason, status = assessment.Status, completed = accumulated, noRetrySpin = true });
                    return new(assessment.Status, accumulated, receipts, assigned.Select(x => x.Key).ToArray(), unified);
                }
                if (assessment.Status == "context")
                {
                    var reports = new List<string>();
                    foreach (var part in Partition(assigned))
                    {
                        var child = await RunStageAsync(part, accumulated, false);
                        if (child.Status is "paused-provider" or "outcome-uncertain") return child;
                        reports.Add(child.Report);
                    }
                    var combined = string.Join("\n\n", reports);
                    if (!unified) return new("complete", combined, receipts, assigned.Select(x => x.Key).ToArray(), false);
                    var bytes = ReviewPrepareService.Utf8.GetBytes(combined);
                    return await RunStageAsync([new("all-complete-context-findings", "derived/complete-partition-reports",
                        sha256(bytes), 0, combined.EnumerateRunes().Count(), combined)], "", true);
                }
                if (assessment.Status is "complete" or "accepted" or "rejected")
                    return new(assessment.Status, accumulated, receipts, assigned.Select(x => x.Key).ToArray(), unified);
                // Only an authoritative terminal output boundary reaches this continuation.
            }
        }
        var result = await RunStageAsync(units, "", true);
        await SaveAsync($"reviews/{snapshot.RunId}/result.awaiting-audit.json", new { reviewClassification = result.Status,
            result.Report, Coverage = snapshot.Evidence.Select(x => x.Path), result.Unified, auditVerified = false });
        var auditResult = await audit.RecordAsync(snapshot, result.Report, receipts, cancellationToken);
        await VerifyAuditProofAsync(auditResult, cancellationToken);
        ReviewAuditPolicy.VerifyCompletion(snapshot, result.Report, receipts, auditResult.Evidence);
        receipts.Add(auditResult.Proof);
        if (existingResult is { } finalBytes)
        {
            using var parsed = JsonDocument.Parse(finalBytes);
            ReviewPrepareService.Require(parsed.RootElement.GetProperty("Status").GetString() == result.Status &&
                parsed.RootElement.GetProperty("Report").GetString() == result.Report,
                "EVIDENCE_HASH_MISMATCH", "Replayed final result differs from the immutable original.");
            Retain(resultPath, finalBytes);
        }
        else await SaveAsync(resultPath, new { result.Status, result.Report,
            Coverage = snapshot.Evidence.Select(x => x.Path), result.Unified, endedAt = clock.Now });
        return result with { Receipts = receipts, Coverage = snapshot.Evidence.Select(x => x.Path).ToArray() };
    }

    private async Task VerifyAuditProofAsync<T>(ReviewAuditObservation<T> observation, CancellationToken cancellationToken)
    {
        var bytes = await store.ReadAsync(observation.Proof.RelativePath, cancellationToken);
        ReviewPrepareService.Require(sha256(bytes) == observation.Proof.Sha256 &&
            canonical.Hash(bytes) == canonical.Hash(JsonSerializer.SerializeToElement(observation.Evidence)),
            "REVIEWER_AUDIT_UNAVAILABLE", "Audit proof bytes differ from the observed bootstrap/read-back evidence.");
    }

    /// <summary>Exact legacy coverage identity: a set of key, codepoint offsets and SHA256 of range UTF8.
    /// File/full-file pins are deliberately outside this signature. Defaults retain len(text), not start+len(text).</summary>
    public static ReviewCoverageSignature CoverageSignature(string text, string? key = null, int start = 0, int? end = null)
        => new(key, start, end ?? text.EnumerateRunes().Count(), ContentHash.Sha256(ReviewPrepareService.Utf8.GetBytes(text)));

    public static IReadOnlySet<ReviewCoverageSignature> CoverageSignature(IEnumerable<ReviewEvidenceUnit> units)
        => units.Select(unit => CoverageSignature(unit.Text, unit.Key, unit.Start, unit.End)).ToHashSet();

    public static IReadOnlyList<IReadOnlyList<ReviewEvidenceUnit>> Partition(IReadOnlyList<ReviewEvidenceUnit> units)
    {
        if (units.Count > 1)
        {
            var middle = units.Count / 2;
            return [units.Take(middle).ToArray(), units.Skip(middle).ToArray()];
        }
        ReviewPrepareService.Require(units.Count == 1, "PROVIDER_CAPABILITY_BLOCKED", "No evidence remains to partition.");
        var unit = units[0];
        var runes = unit.Text.EnumerateRunes().ToArray();
        ReviewPrepareService.Require(runes.Length > 1, "PROVIDER_CAPABILITY_BLOCKED", "Provider rejected one indivisible Unicode evidence unit.");
        var at = runes.Length / 2;
        var left = string.Concat(runes.Take(at).Select(x => x.ToString()));
        var right = string.Concat(runes.Skip(at).Select(x => x.ToString()));
        return [new[] { unit with { Text = left, End = unit.Start + at } },
            new[] { unit with { Text = right, Start = unit.Start + at } }];
    }

    private static string ConfinedPath(string root, string path)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(fullRoot, path));
        ReviewPrepareService.Require(full.StartsWith(fullRoot, StringComparison.Ordinal), "INVALID_PATH", "Review artifact escaped its root.");
        return full;
    }
}
