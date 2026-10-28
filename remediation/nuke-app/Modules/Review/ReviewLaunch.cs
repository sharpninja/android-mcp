using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Modules.Review;

public sealed record ReviewEvidenceUnit(string Key, string File, string FullSha256, int Start, int End, string Text);

/// <summary>One owner, write-ahead receipts, no assistant budgets and no retry of uncertain execution.</summary>
public sealed class ReviewLaunchService(IArtifactStore store, IArtifactLeaseProvider leases,
    IClock clock, ICanonicalJson canonical, Func<ReadOnlyMemory<byte>, string> sha256,
    IReviewReadinessSource readiness, IProcessRunner runner, IProcessCaptureReader captures,
    ReviewAssessService assessor, IReviewAudit audit)
{
    public const string Model = "claude-opus-5-5";
    public const string Effort = "xhigh";

    public async Task<ReviewRunResult> LaunchAsync(PreparedReview prepared, ReviewCapability capability,
        string artifactRoot, string workingDirectory, CancellationToken cancellationToken = default)
    {
        var snapshot = prepared.Snapshot;
        await using var lease = await leases.AcquireAsync("review-run/" + snapshot.RunId, cancellationToken);
        var ready = await readiness.ObserveAsync(snapshot.PullRequestUrl, cancellationToken);
        ReviewPrepareService.Require(ready.IsOpen && !ready.IsDraft, "PR_NOT_READY", "PR readiness changed; review is paused.");
        ReviewPrepareService.Require(ready.Head == snapshot.Head && ready.PullRequestUrl == snapshot.PullRequestUrl,
            "HEAD_MISMATCH", "PR head changed after preparation.");
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
        var intent = $"reviews/{snapshot.RunId}/launch-intent.json";
        try
        {
            _ = await store.ReadAsync(intent, cancellationToken);
            throw new ReviewException("OUTCOME_UNCERTAIN", "Launch already has an execution intent. Recover it instead of duplicating.");
        }
        catch (FileNotFoundException) { }
        var receipts = new List<StoredArtifact>(prepared.Artifacts);
        async Task SaveAsync<T>(string path, T value)
        {
            var bytes = canonical.Serialize(ReviewPrepareService.Json(value));
            receipts.Add(await store.WriteAsync(new(path, bytes, "application/json"), cancellationToken));
        }
        await SaveAsync(intent, new { snapshot.RunId, snapshot.Head, prepared.PromptSha256, prepared.ManifestSha256,
            snapshot.ParentSessionId, snapshot.ParentRequestId, startedAt = clock.Now });
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
                    .Append(unified ? "\nONE UNIFIED ASSESSMENT.\n" : "\nPARTIAL EVIDENCE PASS: no whole-design score. Preserve every finding and boundary question.\n")
                    .Append("End with ").Append(marker).Append(" only when the assigned assessment is complete.\n");
                foreach (var unit in assigned)
                    body.Append("\nBEGIN EXACT RANGE ").Append(JsonSerializer.Serialize(unit with { Text = "" }))
                        .Append('\n').Append(unit.Text).Append("\nEND EXACT RANGE ").Append(unit.Key).Append('\n');
                if (accumulated.Length > 0)
                    body.Append("\nContinue the same immutable stage. Preserve every prior finding and remaining coverage.\nFULL VISIBLE PARTIAL OUTPUT, UNMODIFIED:\n").Append(accumulated);
                var promptBytes = ReviewPrepareService.Utf8.GetBytes(body.ToString());
                ReviewPrepareService.PublicPayload("prompt.txt", body.ToString());
                var promptPath = relative + "/prompt.txt";
                receipts.Add(await store.WriteAsync(new(promptPath, promptBytes, "text/plain"), cancellationToken));
                var absolutePrompt = ConfinedPath(artifactRoot, promptPath);
                var args = new[] { "--safe-mode", "--strict-mcp-config", "--mcp-config", "{\"mcpServers\":{}}", "--tools", "",
                    "--permission-mode", "dontAsk", "--no-session-persistence", "--print", "--output-format", "stream-json", "--verbose",
                    "--model", Model, "--effort", Effort };
                await SaveAsync(relative + "/request.jsonl", new { type = "request", at = clock.Now, snapshot.RunId,
                    snapshot.ParentSessionId, snapshot.ParentRequestId, snapshot.Head, prepared.ManifestSha256,
                    requestedModel = Model, requestedEffort = Effort, capability.Tool, arguments = args,
                    prompt = body.ToString(), promptSha256 = sha256(promptBytes), units = assigned.Select(x => x with { Text = "" }),
                    standardInputPath = absolutePrompt, assistantCaps = (object?)null });
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
                var assessment = assessor.Assess(stdout, new(true, process.ExitCode, process.Cancelled), marker);
                // All event fields except hidden reasoning are retained. Private raw captures remain untouched.
                var visibleBytes = ReviewPrepareService.Utf8.GetBytes(string.Join('\n', assessment.VisibleLines) + "\n");
                receipts.Add(await store.WriteAsync(new(relative + "/response.visible.jsonl", visibleBytes, "application/x-ndjson"), cancellationToken));
                await SaveAsync(relative + "/completion.json", new { process.ExitCode, process.Cancelled, process.StartedAt, process.EndedAt,
                    assessment.Status, assessment.Reason, assessment.Models, assessment.Efforts,
                    responseSha256 = sha256(visibleBytes), hiddenExcluded = assessment.ContainsHiddenReasoning,
                    actualCoverage = assigned.Select(x => new { x.Key, x.File, x.FullSha256, x.Start, x.End }) });
                if (assessment.Status != "paused-provider")
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
        await SaveAsync($"reviews/{snapshot.RunId}/result.json", new { result.Status, result.Report,
            Coverage = snapshot.Evidence.Select(x => x.Path), result.Unified, endedAt = clock.Now });
        await audit.RecordAsync(snapshot.ParentSessionId, snapshot.ParentRequestId, snapshot.RunId, result.Report, receipts, cancellationToken);
        return result with { Receipts = receipts, Coverage = snapshot.Evidence.Select(x => x.Path).ToArray() };
    }

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
