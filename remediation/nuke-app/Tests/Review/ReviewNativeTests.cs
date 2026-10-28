using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Review;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review;

public sealed class ReviewNativeTests
{
    [Theory, MemberData(nameof(ReviewCases.Data), MemberType = typeof(ReviewCases))]
    public async Task SameCasesBindToNativeServices(string name)
    {
        var test = ReviewCases.Find(name);
        ReviewCases.AssertContract(test, await new NativeReviewCaseSystem().ExecuteAsync(test));
    }
}

public sealed class NativeReviewCaseSystem : IReviewCaseSystem
{
    public async Task<CaseObservation> ExecuteAsync(ReviewCase test)
    {
        var f = new ReviewFixture();
        try
        {
            if (test.Operation == "prepare")
            {
                await f.PrepareAsync(test.Input);
                return new("prepared", f.Runner.Calls.Count);
            }
            if (test.Operation == "assess")
                return new(f.Assessor.Assess(ReviewFixture.AssessmentStream(test.Input), new(true, 0), "END").Status, 0);
            if (test.Operation == "recover")
            {
                var result = new ReviewRecoverService(f.Assessor).Recover(new(test.Input == "active", test.Input == "complete",
                    ReviewFixture.Response("full result END"), 0, "END", ["design.md"], "head", "head"));
                return new(result.Status, 0);
            }
            var prepared = await f.PrepareAsync("ready");
            f.Runner.Replies.Enqueue(test.Input switch
            {
                "output-boundary" => ReviewFixture.Response("first material finding", "max_tokens"),
                "authentication" => ReviewFixture.Response("", "end_turn", true, "authentication_error", 401),
                "model-mismatch" => ReviewFixture.Response("full result END_REVIEW_fixture", model: "claude-other"),
                "effort-mismatch" => ReviewFixture.Response("full result END_REVIEW_fixture", effort: "high"),
                _ => ReviewFixture.Response("full result END_REVIEW_fixture")
            });
            if (test.Input == "output-boundary")
                f.Runner.Replies.Enqueue(ReviewFixture.Response("second material finding END_REVIEW_fixture"));
            var launched = await f.Launcher.LaunchAsync(prepared, f.Capability, "/tmp/review-artifacts", "/tmp/review-work");
            if (test.Input == "output-boundary")
            {
                Assert.Contains("first material finding", launched.Report);
                Assert.Contains("second material finding", launched.Report);
                var second = Encoding.UTF8.GetString(f.Store.Values["reviews/fixture/calls/00000002/prompt.txt"]);
                Assert.Contains("first material finding", second);
            }
            foreach (var call in f.Runner.Calls)
            {
                Assert.NotNull(call.StandardInputPath);
                Assert.Contains("--safe-mode", call.Arguments);
                Assert.Contains("--strict-mcp-config", call.Arguments);
                Assert.Equal("", call.Arguments[call.Arguments.ToList().IndexOf("--tools") + 1]);
                Assert.DoesNotContain(call.Arguments, x => x.Contains("skip-permission", StringComparison.Ordinal) ||
                    x.StartsWith("--max-", StringComparison.Ordinal) || x == "--timeout" || x == "--fallback-model");
                Assert.Equal(ReviewLaunchService.Model, call.Arguments[call.Arguments.ToList().IndexOf("--model") + 1]);
                Assert.Equal(ReviewLaunchService.Effort, call.Arguments[call.Arguments.ToList().IndexOf("--effort") + 1]);
            }
            return new(launched.Status, f.Runner.Calls.Count);
        }
        catch (ReviewException error) { return new(error.Code, f.Runner.Calls.Count); }
    }
}

public sealed class ReviewFixture
{
    public static string Hash(ReadOnlyMemory<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes.Span)).ToLowerInvariant();
    public static byte[] Json<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value);
    public ReviewMemoryStore Store { get; } = new();
    public ReviewLease Leases { get; } = new();
    public ReviewReadySource Ready { get; } = new();
    public ReviewFakePrerequisites Prerequisites { get; } = new();
    public ReviewFakeRunner Runner { get; } = new();
    public ReviewAssessService Assessor { get; } = new(Hash);
    public ReviewAudit Audit { get; } = new();
    public ReviewCapability Capability { get; }
    public ReviewLaunchService Launcher { get; }
    public ReviewFixture()
    {
        var receipt = Json(new { supported = true, model = ReviewLaunchService.Model, effort = ReviewLaunchService.Effort, toolSha256 = "pinned-tool" });
        Store.Values["capability.json"] = receipt;
        Capability = new(new("/approved/claude", "pinned-tool", "native-fixture"), ReviewLaunchService.Model,
            ReviewLaunchService.Effort, "capability.json", Hash(receipt));
        Launcher = new(Store, Leases, new ReviewClock(), new CanonicalJson(Hash), Hash, Ready, Prerequisites, Runner,
            new ReviewCaptureReader(Runner), Assessor, new RequiredPluginReviewAudit(Audit, Store, new CanonicalJson(Hash)));
    }
    public async Task<PreparedReview> PrepareAsync(string mode)
    {
        Ready.Value = new(mode == "wrong-head" ? "other-head" : "head", mode == "draft", true,
            "https://github.com/owner/repo/pull/1", Json(new { headRefOid = "head", isDraft = mode == "draft", state = "OPEN" }));
        var evidence = new List<ReviewEvidence>();
        void Add(string path, byte[] bytes, string kind, string? project = null)
        {
            Store.Values[path] = bytes;
            evidence.Add(new(path, Hash(bytes), kind, project, "test-run", "fixture-host", "head", "NUKE Validate Tests"));
        }
        var designBody = mode switch
        {
            "secret" => "apiKey: real-secret-value",
            "json-credential" => JsonSerializer.Serialize(new { apiKey = "real-secret-value" }),
            "bearer-credential" => "Authorization: Bearer real-private-credential",
            "public-auth-schema" => JsonSerializer.Serialize(new { properties = new { apiKey = new { type = "string" } }, example = "Bearer <redacted>" }),
            _ => "Complete design body"
        };
        Add(mode == "private-profile-path" ? "private/PROFILE-MERGED.md" : "design.md", Encoding.UTF8.GetBytes(designBody), mode == "scope-leak" ? "dataset" : "design");
        var failed = mode == "failed-trx" ? 1 : 0;
        var skipped = mode == "skipped-trx" ? 1 : 0;
        byte[] Trx() => Encoding.UTF8.GetBytes($"<TestRun><ResultSummary><Counters total='1' passed='{1 - failed - skipped}' failed='{failed}' notExecuted='{skipped}'/></ResultSummary><Results><UnitTestResult outcome='{(failed == 0 && skipped == 0 ? "Passed" : "Failed")}'/></Results></TestRun>");
        if (mode != "missing-unit-trx") Add("unit.trx", Trx(), "unit-trx", "Repo.Tests");
        Add("scope.trx", Trx(), "integration-trx", "Scope.Integration");
        Add("requirements.json", Json(new { head = "head", complete = mode != "requirements-open", requirementIds = new[] { "FR-BUILD-001", "TR-BUILD-TOOL-001", "TEST-BUILD-001" } }), "requirements");
        if (mode == "coverage-omission") evidence.Add(evidence[0]);
        var req = evidence.Single(x => x.Kind == "requirements");
        var stub = new StoredArtifact("trusted-fixture.json", "fixture", 0, "application/json");
        Prerequisites.Value = new("head", ["Repo.Tests"], ["Scope.Integration"], ["FR-BUILD-001", "TR-BUILD-TOOL-001", "TEST-BUILD-001"],
            stub, new(req.Path, req.Sha256, Store.Values[req.Path].Length, "application/json"), stub, stub);
        Prerequisites.RejectRequirements = mode == "requirements-open";
        var snapshot = new ReviewSnapshot("fixture", "Codex-parent", "req-parent", "head", "https://github.com/owner/repo/pull/1",
            ["Repo.Tests"], ["Scope.Integration"], ["FR-BUILD-001", "TR-BUILD-TOOL-001", "TEST-BUILD-001"], evidence);
        return await new ReviewPrepareService(Store, Leases, new ReviewClock(), new CanonicalJson(Hash), Hash, Ready, Prerequisites).PrepareAsync(snapshot);
    }
    public static byte[] Response(string text, string reason = "end_turn", bool error = false,
        string? terminalReason = null, int? apiStatus = null, string model = ReviewLaunchService.Model, string effort = ReviewLaunchService.Effort)
        => Encoding.UTF8.GetBytes(string.Join('\n', new[]
        {
            JsonSerializer.Serialize(new { type = "assistant", message = new { model, effort, content = new[] { new { type = "text", text } } } }),
            JsonSerializer.Serialize(new { type = "result", stop_reason = reason, is_error = error, terminal_reason = terminalReason, api_error_status = apiStatus })
        }) + "\n");
    public static byte[] AssessmentStream(string mode)
    {
        if (mode == "context") return Response("", error: true, terminalReason: "prompt_too_long", apiStatus: 400);
        if (mode == "credits") return Response("", error: true, terminalReason: "model_requires_usage_credits", apiStatus: 429);
        var json = mode switch
        {
            "split-json" => JsonSerializer.Serialize(new { findings = new[] { "keep all" }, report_complete = true }),
            "partial-json" => "{\"findings\":[\"keep all\"],",
            "missing-score" => JsonSerializer.Serialize(new { report_complete = true, OverallVerdict = "AGREE", FAIL = 0, UNKNOWN = 0 }),
            _ => JsonSerializer.Serialize(new { report_complete = true, OverallVerdict = "AGREE", accuracy = mode == "low-score" ? 97 : 98, completeness = 98, FAIL = 0, UNKNOWN = 0 })
        };
        if (mode != "split-json") return Response(json, mode == "partial-json" ? "max_tokens" : "end_turn");
        var at = json.Length / 2;
        var first = Encoding.UTF8.GetString(Response(json[..at]));
        var second = Encoding.UTF8.GetString(Response(json[at..]));
        return Encoding.UTF8.GetBytes(first[..first.IndexOf('\n')] + "\n" + second);
    }
}

public sealed class ReviewMemoryStore : IArtifactStore
{
    public string? ThrowBeforePath { get; set; }
    public Dictionary<string, byte[]> Values { get; } = new(StringComparer.Ordinal);
    public Task<StoredArtifact> WriteAsync(Artifact artifact, CancellationToken cancellationToken = default)
    {
        if (artifact.RelativePath == ThrowBeforePath) throw new IOException("Synthetic crash before immutable request intent.");
        var bytes = artifact.Bytes.ToArray();
        if (Values.TryGetValue(artifact.RelativePath, out var old) && !old.SequenceEqual(bytes))
            throw new IOException("Immutable artifact conflict");
        Values[artifact.RelativePath] = bytes;
        return Task.FromResult(new StoredArtifact(artifact.RelativePath, ReviewFixture.Hash(bytes), bytes.Length, artifact.MediaType));
    }
    public Task<ReadOnlyMemory<byte>> ReadAsync(string path, CancellationToken cancellationToken = default)
        => Values.TryGetValue(path, out var bytes) ? Task.FromResult<ReadOnlyMemory<byte>>(bytes) : throw new FileNotFoundException(path);
}
public sealed class ReviewLease : IArtifactLeaseProvider
{
    public ValueTask<IAsyncDisposable> AcquireAsync(string scope, CancellationToken cancellationToken = default) => ValueTask.FromResult<IAsyncDisposable>(new Lease());
    private sealed class Lease : IAsyncDisposable { public ValueTask DisposeAsync() => ValueTask.CompletedTask; }
}
public sealed class ReviewClock : IClock { public DateTimeOffset Now => DateTimeOffset.UnixEpoch; }
public sealed class ReviewReadySource : IReviewReadinessSource
{
    public ReviewReadiness Value { get; set; } = null!;
    public Task<ReviewReadiness> ObserveAsync(string url, CancellationToken cancellationToken = default) => Task.FromResult(Value);
}
public sealed class ReviewFakePrerequisites : IReviewPrerequisiteSource
{
    public ReviewPrerequisites Value { get; set; } = null!;
    public bool RejectRequirements { get; set; }
    public Task<ReviewPrerequisites> ObserveAsync(ReviewSnapshot snapshot, CancellationToken cancellationToken = default)
        => RejectRequirements ? throw new ReviewException("REQUIREMENTS_INCOMPLETE", "Independent requirements source rejected incomplete records.") : Task.FromResult(Value);
}
public sealed class ReviewFakeRunner : IProcessRunner
{
    public bool ThrowOnCall { get; set; }
    public Queue<byte[]> Replies { get; } = new();
    public Queue<byte[]> Diagnostics { get; } = new();
    public Queue<int> ExitCodes { get; } = new();
    public List<ProcessRequest> Calls { get; } = [];
    public Dictionary<string, byte[]> Captures { get; } = [];
    public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
    {
        Calls.Add(request);
        if (ThrowOnCall) throw new IOException("Synthetic uncertain capture failure");
        var path = request.ReceiptDirectory + "/stdout.bin";
        Captures[path] = Replies.Dequeue();
        var stderr = request.ReceiptDirectory + "/stderr.bin";
        Captures[stderr] = Diagnostics.Count > 0 ? Diagnostics.Dequeue() : [];
        return Task.FromResult(new ProcessResult(ExitCodes.Count > 0 ? ExitCodes.Dequeue() : 0, path, stderr, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, false));
    }
}
public sealed class ReviewCaptureReader(ReviewFakeRunner runner) : IProcessCaptureReader
{
    public Task<ReadOnlyMemory<byte>> ReadAsync(ProcessResult result, bool standardError, CancellationToken cancellationToken = default)
        => Task.FromResult<ReadOnlyMemory<byte>>(runner.Captures[standardError ? result.StandardErrorPath : result.StandardOutputPath]);
}
public sealed class ReviewAudit : IReviewAuditTransport
{
    public string? FullReport { get; private set; }
    public string? Fault { get; set; }
    public Task<ReviewAuditBootstrapEvidence> ObserveBootstrapAsync(ReviewSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (Fault == "no-bootstrap-config") throw new ReviewException("REVIEWER_AUDIT_UNAVAILABLE", "Synthetic missing official transport.");
        return Task.FromResult(new ReviewAuditBootstrapEvidence(Fault == "wrong-head" ? "other-head" : snapshot.Head,
            Fault == "wrong-agent" ? "Codex" : ReviewAuditPolicy.ReviewerAgent, ReviewLaunchService.Model, ReviewLaunchService.Effort,
            ReviewAuditPolicy.PluginName, "fixture-version", new string('a', 64), "/approved/private-mcp.json", new string('b', 64),
            ["mcp__mcpserver__sessionlog_submit"], true, true, Fault == "wrong-tool-policy"));
    }
    public Task<ReviewAuditCompletionEvidence> ReadReviewerOwnTurnAsync(ReviewSnapshot snapshot, string expectedFullVerdict,
        IReadOnlyList<StoredArtifact> receipts, CancellationToken cancellationToken = default)
    {
        FullReport = expectedFullVerdict;
        return Task.FromResult(new ReviewAuditCompletionEvidence(snapshot.Head, snapshot.RunId,
            Fault == "wrong-post-agent" ? "Codex" : ReviewAuditPolicy.ReviewerAgent, ReviewLaunchService.Model, ReviewLaunchService.Effort,
            "Claude-20261028T000000Z-review", "req-20261028T000001Z-review", snapshot.ParentSessionId, snapshot.ParentRequestId,
            Fault == "altered-verdict" ? expectedFullVerdict + " altered" : expectedFullVerdict,
            ReviewAuditPolicy.CallIds(receipts), Fault != "unpersisted-own-turn"));
    }
}
