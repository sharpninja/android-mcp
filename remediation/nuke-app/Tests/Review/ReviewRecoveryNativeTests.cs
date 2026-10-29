using System.Text;
using AndroidMcp.BuildTool.Modules.Review;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review;

public sealed class ReviewRecoveryNativeTests
{
    [Theory, MemberData(nameof(ReviewRecoveryCases.Data), MemberType = typeof(ReviewRecoveryCases))]
    public async Task SameRecoveryCasesBindToNativeServices(string name)
        => ReviewRecoveryCases.AssertContract(name, await new NativeReviewRecovery().ExecuteAsync(name));
}
public sealed class NativeReviewRecovery : IReviewSafetySystem
{
    public async Task<ReviewSafetyObservation> ExecuteAsync(string name)
    {
        var f = new ReviewFixture();
        var prepared = await f.PrepareAsync("ready");
        var detail = name switch
        {
            "terminal-continuation" => "original-report-retained",
            "completed-adoption" => "originals-unchanged",
            "context-resume" => "all-passes-retained",
            "unknown-terminal-error" => "no-retry-spin",
            _ => "no-duplicate"
        };
        try
        {
            if (name == "terminal-continuation")
            {
                f.Runner.Replies.Enqueue(ReviewFixture.Response("first immutable material finding", "max_tokens"));
                f.Store.ThrowBeforePath = "reviews/fixture/calls/00000002/prompt.txt";
            }
            else if (name == "context-resume")
            {
                f.Runner.Replies.Enqueue(ReviewFixture.Response("", error: true, terminalReason: "prompt_too_long", apiStatus: 400));
                f.Runner.Replies.Enqueue(ReviewFixture.Response("left complete material finding END_REVIEW_fixture"));
                f.Store.ThrowBeforePath = "reviews/fixture/calls/00000003/prompt.txt";
            }
            else if (name == "uncertain-prefix") f.Runner.ThrowOnCall = true;
            else if (name == "unknown-terminal-error")
                f.Runner.Replies.Enqueue(ReviewFixture.Response("unknown terminal provider response", error: true, terminalReason: "unknown_provider_failure"));
            else f.Runner.Replies.Enqueue(ReviewFixture.Response("original completed report END_REVIEW_fixture"));
            ReviewRunResult? initial = null;
            try { initial = await f.Launcher.LaunchAsync(prepared, f.Capability, "/tmp/review-artifacts", "/tmp/review-work"); }
            catch (IOException) when (f.Store.ThrowBeforePath is not null) { }
            if (name == "unknown-terminal-error") return new(initial!.Status, f.Runner.Calls.Count, detail);
            f.Store.ThrowBeforePath = null;
            f.Runner.ThrowOnCall = false;
            if (name == "manifest-tamper") f.Store.Values["reviews/fixture/manifest.json"] = Encoding.UTF8.GetBytes("changed");
            if (name == "terminal-continuation") f.Runner.Replies.Enqueue(ReviewFixture.Response("continued second finding END_REVIEW_fixture"));
            if (name == "context-resume")
            {
                f.Runner.Replies.Enqueue(ReviewFixture.Response("right complete material finding END_REVIEW_fixture"));
                f.Runner.Replies.Enqueue(ReviewFixture.Response("one unified report END_REVIEW_fixture"));
            }
            var original = f.Store.Values.ToDictionary(x => x.Key, x => x.Value.ToArray(), StringComparer.Ordinal);
            var result = await f.Launcher.ResumeAsync(prepared, f.Capability, "/tmp/review-artifacts", "/tmp/review-work");
            foreach (var item in original) Assert.Equal(item.Value, f.Store.Values[item.Key]);
            if (name == "completed-adoption") Assert.Equal(original.Count, f.Store.Values.Count);
            if (name == "terminal-continuation")
            {
                Assert.Contains("first immutable material finding", result.Report);
                Assert.Contains("continued second finding", result.Report);
            }
            if (name == "context-resume")
            {
                var finalPrompt = Encoding.UTF8.GetString(f.Store.Values["reviews/fixture/calls/00000004/prompt.txt"]);
                Assert.Contains("left complete material finding", finalPrompt);
                Assert.Contains("right complete material finding", finalPrompt);
                Assert.True(result.Unified);
                Assert.Equal(prepared.Snapshot.Evidence.Select(x => x.Path), result.Coverage);
            }
            return new(result.Status, f.Runner.Calls.Count, detail);
        }
        catch (ReviewException error) { return new(error.Code, f.Runner.Calls.Count, detail); }
    }
}
