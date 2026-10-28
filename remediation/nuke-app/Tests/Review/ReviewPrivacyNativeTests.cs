using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Modules.Review;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review;

public sealed class ReviewPrivacyNativeTests
{
    [Theory, MemberData(nameof(ReviewPrivacyCases.Data), MemberType = typeof(ReviewPrivacyCases))]
    public void SamePrivacyCasesBindToNativeServices(string name)
        => ReviewPrivacyCases.AssertContract(name, new NativeReviewPrivacy().Execute(name));
}
public sealed class NativeReviewPrivacy : IReviewPrivacySystem
{
    public PrivacyObservation Execute(string name)
    {
        var assessor = new ReviewAssessService(ReviewFixture.Hash);
        if (name == "malformed-hidden-fragment")
        {
            const string fragment = "{\"type\":\"assistant\",\"content\":[{\"type\":\"thinking\",\"text\":\"private-cut-fragment";
            var terminal = JsonSerializer.Serialize(new { type = "result", stop_reason = "end_turn", is_error = false, custom_visible = "retained" });
            var state = assessor.Assess(Encoding.UTF8.GetBytes(fragment + "\n" + terminal + "\n"), new(true, 0), "END");
            using var metadata = JsonDocument.Parse(state.VisibleLines[0]);
            Assert.Equal(Encoding.UTF8.GetByteCount(fragment), metadata.RootElement.GetProperty("utf8Bytes").GetInt32());
            Assert.Equal(ReviewFixture.Hash(Encoding.UTF8.GetBytes(fragment)), metadata.RootElement.GetProperty("sha256").GetString());
            Assert.False(metadata.RootElement.TryGetProperty("text", out _));
            var all = string.Join('\n', state.VisibleLines);
            Assert.Equal("outcome-uncertain", assessor.Assess(Encoding.UTF8.GetBytes(all), new(true, 0), "END").Status);
            return new(state.Status, !all.Contains("private-cut-fragment", StringComparison.Ordinal),
                all.Contains("retained", StringComparison.Ordinal), state.VisibleLines.Count);
        }
        var assistant = JsonSerializer.Serialize(new { type = "assistant", signature = "public-signature-retained", custom_visible = "assistant-retained",
            message = new { model = ReviewLaunchService.Model, effort = ReviewLaunchService.Effort,
                content = new[] { new { type = "thinking", text = "private-thought" }, new { type = "text", text = "visible END" } } } });
        var delta = JsonSerializer.Serialize(new { type = "stream_event", custom_visible = "delta-envelope-retained",
            @event = new { type = "content_block_delta", delta = new { type = "thinking_delta", thinking = "private-delta" } } });
        var result = JsonSerializer.Serialize(new { type = "result", stop_reason = "end_turn", is_error = false, custom_visible = "result-retained" });
        var observation = assessor.Assess(Encoding.UTF8.GetBytes(assistant + "\n" + delta + "\n" + result), new(true, 0), "END");
        var visible = string.Join('\n', observation.VisibleLines);
        return new(observation.Status, !visible.Contains("private-thought", StringComparison.Ordinal) && !visible.Contains("private-delta", StringComparison.Ordinal),
            new[] { "public-signature-retained", "assistant-retained", "delta-envelope-retained", "result-retained" }.All(x => visible.Contains(x, StringComparison.Ordinal)), observation.VisibleLines.Count);
    }
}
