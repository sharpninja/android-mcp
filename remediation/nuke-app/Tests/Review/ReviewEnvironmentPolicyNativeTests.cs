using System.Text.Json;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Review;
public sealed class ReviewEnvironmentPolicyNativeTests
{
    [Fact]
    public async Task Same_policy_binds_to_existing_launcher_and_is_retained_without_environment_values()
    {
        var fixture=new ReviewFixture();var prepared=await fixture.PrepareAsync("ready");
        fixture.Runner.Replies.Enqueue(ReviewFixture.Response("done END_REVIEW_fixture"));
        await fixture.Launcher.LaunchAsync(prepared,fixture.Capability,"/tmp/review-artifacts","/tmp/review-work");
        var call=Assert.Single(fixture.Runner.Calls);var rule=Assert.Single(call.RemovedEnvironmentNameRules!);
        ReviewEnvironmentPolicyCases.AssertContract(new(string.Join(',',call.RemovedEnvironmentKeys!),rule.Prefix,string.Join(',',rule.ContainsAny),call.ClearEnvironment,fixture.Runner.Calls.Count));
        using var receipt=JsonDocument.Parse(fixture.Store.Values["reviews/fixture/calls/00000001/request.jsonl"]);
        Assert.Equal(call.RemovedEnvironmentKeys,receipt.RootElement.GetProperty("removedEnvironmentKeys").EnumerateArray().Select(x=>x.GetString()!).ToArray());
        Assert.Contains("unverified",receipt.RootElement.GetProperty("providerOutputRequestProfile").GetString());
        Assert.False(receipt.RootElement.TryGetProperty("inheritedEnvironmentValues",out _));
    }
}
