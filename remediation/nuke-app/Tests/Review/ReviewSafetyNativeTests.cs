using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Modules.Review;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review;

public sealed class ReviewSafetyNativeTests
{
    [Theory, MemberData(nameof(ReviewSafetyCases.Data), MemberType = typeof(ReviewSafetyCases))]
    public async Task SameSafetyCasesBindToNativeServices(string name)
        => ReviewSafetyCases.AssertContract(name, await new NativeReviewSafety().ExecuteAsync(name));
}
public sealed class NativeReviewSafety : IReviewSafetySystem
{
    public async Task<ReviewSafetyObservation> ExecuteAsync(string name)
    {
        var f = new ReviewFixture();
        var detail = name is "missing-model" or "missing-effort" or "duplicate-launch" or "process-exception" ? "no-retry" : "no-launch";
        try
        {
            if (name == "unicode-partition")
            {
                var parts = ReviewLaunchService.Partition([new("unicode", "unicode.md", "whole-hash", 7, 11, "A😀éZ")]);
                Assert.Equal("A😀éZ", string.Concat(parts.SelectMany(x => x).Select(x => x.Text)));
                Assert.Equal(new[] { 7, 9 }, parts.SelectMany(x => x).Select(x => x.Start));
                Assert.Equal(new[] { 9, 11 }, parts.SelectMany(x => x).Select(x => x.End));
                Assert.All(parts.SelectMany(x => x), p => Assert.Equal("whole-hash", p.FullSha256));
                return new("partitioned", 0, "exact-runes-and-coverage");
            }
            if (name == "hidden-excluded")
            {
                var assistant = JsonSerializer.Serialize(new { type = "assistant", visible_custom = "preserve-this-field",
                    message = new { model = ReviewLaunchService.Model, effort = ReviewLaunchService.Effort,
                        content = new[] { new { type = "thinking", text = "private-hidden-reason" }, new { type = "text", text = "visible report END" } } } });
                var terminal = JsonSerializer.Serialize(new { type = "result", is_error = false, stop_reason = "end_turn", visible_custom = "preserve-result" });
                var state = f.Assessor.Assess(Encoding.UTF8.GetBytes(assistant + "\n" + terminal + "\n"), new(true, 0), "END");
                Assert.True(state.ContainsHiddenReasoning);
                Assert.Equal("visible report END", state.Report);
                Assert.DoesNotContain("private-hidden-reason", string.Join('\n', state.VisibleLines));
                Assert.Contains("preserve-this-field", string.Join('\n', state.VisibleLines));
                Assert.Contains("preserve-result", string.Join('\n', state.VisibleLines));
                return new(state.Status, 0, "visible-fields-retained");
            }
            if (name == "malformed-stream")
                return new(f.Assessor.Assess(Encoding.UTF8.GetBytes("not-json\n"), new(true, 0), "END").Status, 0, "no-acceptance");
            if (name == "recovery-head-mismatch")
                _ = new ReviewRecoverService(f.Assessor).Recover(new(false, true, ReviewFixture.Response("END"), 0, "END", ["design.md"], "head", "other-head"));
            var prepared = await f.PrepareAsync("ready");
            if (name == "tampered-frozen-evidence")
                f.Store.Values["reviews/fixture/input/design.md"] = Encoding.UTF8.GetBytes("changed");
            if (name == "unverified-capability") f.Store.Values["capability.json"] = ReviewFixture.Json(new { supported = false });
            if (name == "changed-ready-head") f.Ready.Value = f.Ready.Value with { Head = "other-head" };
            if (name == "process-exception") f.Runner.ThrowOnCall = true;
            if (name == "context-unified")
            {
                f.Runner.Replies.Enqueue(ReviewFixture.Response("", error: true, terminalReason: "prompt_too_long", apiStatus: 400));
                f.Runner.Replies.Enqueue(ReviewFixture.Response("left material finding END_REVIEW_fixture"));
                f.Runner.Replies.Enqueue(ReviewFixture.Response("right material finding END_REVIEW_fixture"));
                f.Runner.Replies.Enqueue(ReviewFixture.Response("unified material verdict END_REVIEW_fixture"));
            }
            else f.Runner.Replies.Enqueue(ReviewFixture.Response("complete END_REVIEW_fixture",
                model: name == "missing-model" ? "" : ReviewLaunchService.Model,
                effort: name == "missing-effort" ? "" : ReviewLaunchService.Effort));
            var result = await f.Launcher.LaunchAsync(prepared, f.Capability, "/tmp/review-artifacts", "/tmp/review-work");
            if (name == "duplicate-launch")
                _ = await f.Launcher.LaunchAsync(prepared, f.Capability, "/tmp/review-artifacts", "/tmp/review-work");
            if (name == "context-unified")
            {
                Assert.True(result.Unified);
                Assert.Equal(prepared.Snapshot.Evidence.Select(x => x.Path), result.Coverage);
                var finalPrompt = Encoding.UTF8.GetString(f.Store.Values["reviews/fixture/calls/00000004/prompt.txt"]);
                Assert.Contains("left material finding", finalPrompt);
                Assert.Contains("right material finding", finalPrompt);
                Assert.Equal(result.Report, f.Audit.FullReport);
                Assert.Contains(result.Receipts, x => x.RelativePath.EndsWith("/00000001/response.visible.jsonl", StringComparison.Ordinal));
                Assert.Contains(result.Receipts, x => x.RelativePath.EndsWith("/00000004/response.visible.jsonl", StringComparison.Ordinal));
                detail = "all-inputs-one-synthesis";
            }
            return new(result.Status, f.Runner.Calls.Count, detail);
        }
        catch (ReviewException error) { return new(error.Code, f.Runner.Calls.Count, detail); }
    }
}
