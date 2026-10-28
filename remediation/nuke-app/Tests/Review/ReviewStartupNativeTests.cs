using System.Text;
using System.Text.Json;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review;

public sealed class ReviewStartupNativeTests
{
    [Theory, MemberData(nameof(ReviewStartupCases.Data), MemberType = typeof(ReviewStartupCases))]
    public async Task SameStartupCasesBindToNativeServices(string name)
        => ReviewStartupCases.AssertContract(name, await new NativeReviewStartup().ExecuteAsync(name));
}
public sealed class NativeReviewStartup : IReviewStartupSystem
{
    public async Task<StartupObservation> ExecuteAsync(string name)
    {
        var f = new ReviewFixture();
        var prepared = await f.PrepareAsync("ready");
        var diagnostics = name switch
        {
            "stderr-authentication" => "Authentication failed; private detail never-publish-this-value",
            "stderr-capacity" => "Capacity unavailable; private detail never-publish-this-value",
            "stderr-capability" => "Unsupported effort xhigh; private detail never-publish-this-value",
            _ => ""
        };
        f.Runner.Replies.Enqueue([]);
        f.Runner.Diagnostics.Enqueue(Encoding.UTF8.GetBytes(diagnostics));
        f.Runner.ExitCodes.Enqueue(1);
        var result = await f.Launcher.LaunchAsync(prepared, f.Capability, "/tmp/review-artifacts", "/tmp/review-work");
        var visible = Encoding.UTF8.GetString(f.Store.Values["reviews/fixture/calls/00000001/response.visible.jsonl"]);
        Assert.DoesNotContain("never-publish-this-value", visible);
        Assert.Equal(Encoding.UTF8.GetBytes(diagnostics), f.Runner.Captures[f.Runner.Calls[0].ReceiptDirectory + "/stderr.bin"]);
        using var completion = JsonDocument.Parse(f.Store.Values["reviews/fixture/calls/00000001/completion.json"]);
        Assert.Equal(ReviewFixture.Hash(Encoding.UTF8.GetBytes(diagnostics)), completion.RootElement.GetProperty("rawStderrSha256").GetString());
        var reason = result.Status == "outcome-uncertain" ? "unclassified" : completion.RootElement.GetProperty("Reason").GetString()!.Split(':')[0];
        var recovered = await f.Launcher.ResumeAsync(prepared, f.Capability, "/tmp/review-artifacts", "/tmp/review-work");
        Assert.Equal(result.Status, recovered.Status);
        return new(result.Status, reason, f.Runner.Calls.Count);
    }
}
