using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Modules.Review;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review;

public sealed class ReviewFinalFragmentNativeTests
{
    [Theory, MemberData(nameof(ReviewFinalFragmentCases.Data), MemberType = typeof(ReviewFinalFragmentCases))]
    public void SameFinalFragmentCasesBindToNativeAssessment(string name)
        => ReviewFinalFragmentCases.AssertContract(name, new NativeFinalFragments().Execute(name));
}
public sealed class NativeFinalFragments : IFinalFragmentSystem
{
    public FinalFragmentObservation Execute(string name)
    {
        var agree = JsonSerializer.Serialize(new { report_complete = true, OverallVerdict = "AGREE", accuracy = 98, completeness = 98, FAIL = 0, UNKNOWN = 0 });
        var disagree = JsonSerializer.Serialize(new { report_complete = true, OverallVerdict = "DISAGREE", accuracy = 97, completeness = 97, FAIL = 1, UNKNOWN = 0 });
        var chunks = name switch
        {
            "progress-before-verdict" => new[] { "Progress 50 percent; keep this visible progress.", agree + "\nEND" },
            "split-json-after-progress" => new[] { "Progress 75 percent; keep this visible progress.", agree[..(agree.Length / 2)], agree[(agree.Length / 2)..] },
            "latest-disagree-dominates" => new[] { agree, disagree },
            _ => new[] { agree, "Further visible partial finding, incomplete" }
        };
        var lines = chunks.Select(text => JsonSerializer.Serialize(new { type = "assistant", message = new { model = ReviewLaunchService.Model, effort = ReviewLaunchService.Effort,
            content = new[] { new { type = "text", text } } } })).ToList();
        lines.Add(JsonSerializer.Serialize(new { type = "result", is_error = false, stop_reason = name == "cut-off-after-agree" ? "max_tokens" : "end_turn" }));
        var result = new ReviewAssessService(ReviewFixture.Hash).Assess(Encoding.UTF8.GetBytes(string.Join('\n', lines)), new(true, 0), "END");
        return new(result.Status, chunks.All(x => result.Report.Contains(x, StringComparison.Ordinal)), 0);
    }
}
