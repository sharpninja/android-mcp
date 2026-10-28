using System.Text.Json;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review;

public sealed class ReviewAuditNativeTests
{
    [Theory, MemberData(nameof(ReviewAuditCases.Data), MemberType = typeof(ReviewAuditCases))]
    public async Task SameAuditCasesBindToNativeAdapterAndLaunch(string name)
        => ReviewAuditCases.AssertContract(name, await new NativeReviewAuditCases().ExecuteAsync(name));
}
public sealed class NativeReviewAuditCases : IReviewAuditCaseSystem
{
    public async Task<AuditObservation> ExecuteAsync(string name)
    {
        var f = new ReviewFixture();
        var prepared = await f.PrepareAsync("ready");
        f.Audit.Fault = name == "ready" ? null : name;
        f.Runner.Replies.Enqueue(ReviewFixture.AssessmentStream("acceptance"));
        bool FinalAccepted()
        {
            if (!f.Store.Values.TryGetValue("reviews/fixture/result.json", out var bytes)) return false;
            using var document = JsonDocument.Parse(bytes);
            return document.RootElement.GetProperty("Status").GetString() == "accepted";
        }
        try
        {
            var result = await f.Launcher.LaunchAsync(prepared, f.Capability, "/tmp/review-artifacts", "/tmp/review-work");
            Assert.Equal(result.Report, f.Audit.FullReport);
            return new(result.Status, f.Runner.Calls.Count, FinalAccepted());
        }
        catch (AndroidMcp.BuildTool.Modules.Review.ReviewException error)
        {
            if (f.Runner.Calls.Count > 0)
            {
                Assert.True(f.Store.Values.ContainsKey("reviews/fixture/result.awaiting-audit.json"));
                Assert.True(f.Store.Values.ContainsKey("reviews/fixture/calls/00000001/response.visible.jsonl"));
            }
            return new(error.Code, f.Runner.Calls.Count, FinalAccepted());
        }
    }
}
