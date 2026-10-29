using System.Text.Json;
using AndroidMcp.BuildTool.Modules.Review;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review;

public sealed class ReviewVerdictNativeTests
{
    [Theory, MemberData(nameof(ReviewVerdictCases.Data), MemberType = typeof(ReviewVerdictCases))]
    public void SameVerdictCasesBindToNativeAssessment(string name)
    {
        object score = name switch
        {
            "numeric-101" => 101,
            "numeric-string" => "99",
            "overflow-number" => JsonDocument.Parse("1e999").RootElement.Clone(),
            _ => 100
        };
        object failures = name == "fail-finding" ? new[] { "material failure" } : name == "empty-finding-arrays" ? Array.Empty<string>() : 0;
        object unknowns = name == "unknown-finding" ? new[] { "unresolved question" } : name == "empty-finding-arrays" ? Array.Empty<string>() : 0;
        var verdict = JsonSerializer.Serialize(new { report_complete = true, OverallVerdict = "AGREE", accuracy = score, completeness = 100, FAIL = failures, UNKNOWN = unknowns });
        var result = new ReviewAssessService(ReviewFixture.Hash).Assess(ReviewFixture.Response(verdict), new(true, 0), "END");
        ReviewVerdictCases.AssertContract(name, new(result.Status, 0));
    }
}
