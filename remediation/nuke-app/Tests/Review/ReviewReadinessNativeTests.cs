using System.Text.Json;
using AndroidMcp.BuildTool.Modules.Review;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review;

public sealed class ReviewReadinessNativeTests
{
    [Theory, MemberData(nameof(ReviewReadinessCases.Data), MemberType = typeof(ReviewReadinessCases))]
    public async Task SameReadinessCasesBindToNativeAdapter(string name)
        => ReviewReadinessCases.AssertContract(name, await new NativeReadinessCases().ExecuteAsync(name));
}
public sealed class NativeReadinessCases : IReadinessCaseSystem
{
    public async Task<ReadinessObservation> ExecuteAsync(string name)
    {
        var runner = new ReviewFakeRunner();
        var head = new string('a', 40);
        const string url = "https://github.com/owner/repo/pull/1";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { headRefOid = head, isDraft = false, state = "OPEN", url });
        runner.Replies.Enqueue(bytes);
        if (name == "repeat-observation") runner.Replies.Enqueue(bytes);
        if (name == "failed-gh") runner.ExitCodes.Enqueue(1);
        var source = new GitHubReviewReadinessSource(runner, new ReviewCaptureReader(runner), new("/approved/gh", "pinned-gh", "fixture"), "/workspace", "/private/github");
        try
        {
            var observed = await source.ObserveAsync(name == "invalid-pr" ? "https://example.com/owner/repo/pull/1" : url);
            if (name == "repeat-observation") observed = await source.ObserveAsync(url);
            Assert.False(observed.IsDraft); Assert.True(observed.IsOpen); Assert.Equal(url, observed.PullRequestUrl);
            Assert.Equal(bytes, observed.Receipt.ToArray());
            Assert.All(runner.Calls, call => Assert.Equal(new[] { "pr", "view", url, "--json", "isDraft,state,headRefOid,url" }, call.Arguments));
            return new("observed", observed.Head, runner.Calls.Count, runner.Calls.Select(x => x.ReceiptDirectory).Distinct(StringComparer.Ordinal).Count());
        }
        catch (ReviewException error)
        { return new(error.Code, "", runner.Calls.Count, runner.Calls.Select(x => x.ReceiptDirectory).Distinct(StringComparer.Ordinal).Count()); }
    }
}
