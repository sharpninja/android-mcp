using System.Text;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review;

// The same independently pinned cases bind to the mock first and the native adapter later.
public sealed record ReviewCase(string Name, string Operation, string Input, string Expected, int Calls);
public sealed record CaseObservation(string Status, int Calls);
public interface IReviewCaseSystem { Task<CaseObservation> ExecuteAsync(ReviewCase test); }

public static class ReviewCases
{
    public static readonly ReviewCase[] All =
    [
        new("ready", "prepare", "ready", "prepared", 0),
        new("draft", "prepare", "draft", "PR_NOT_READY", 0),
        new("wrong-head", "prepare", "wrong-head", "HEAD_MISMATCH", 0),
        new("missing-unit-trx", "prepare", "missing-unit-trx", "TEST_EVIDENCE_INCOMPLETE", 0),
        new("failed-trx", "prepare", "failed-trx", "TEST_EVIDENCE_FAILED", 0),
        new("skipped-trx", "prepare", "skipped-trx", "TEST_EVIDENCE_FAILED", 0),
        new("requirements-open", "prepare", "requirements-open", "REQUIREMENTS_INCOMPLETE", 0),
        new("scope-leak", "prepare", "scope-leak", "SCOPE_VIOLATION", 0),
        new("secret", "prepare", "secret", "PRIVATE_PAYLOAD", 0),
        new("coverage-omission", "prepare", "coverage-omission", "COVERAGE_INCOMPLETE", 0),
        new("complete", "launch", "complete", "complete", 1),
        new("automatic-output-continuation", "launch", "output-boundary", "complete", 2),
        new("authentication-pause", "launch", "authentication", "paused-provider", 1),
        new("model-mismatch", "launch", "model-mismatch", "MODEL_MISMATCH", 1),
        new("effort-mismatch", "launch", "effort-mismatch", "EFFORT_MISMATCH", 1),
        new("uncertain", "recover", "uncertain", "outcome-uncertain", 0),
        new("active", "recover", "active", "outcome-uncertain", 0),
        new("complete-adoption", "recover", "complete", "complete", 0),
        new("split-json", "assess", "split-json", "complete", 0),
        new("partial-json", "assess", "partial-json", "continue", 0),
        new("context-boundary", "assess", "context", "context", 0),
        new("credit-pause", "assess", "credits", "paused-provider", 0),
        new("low-score", "assess", "low-score", "rejected", 0),
        new("missing-score", "assess", "missing-score", "rejected", 0),
        new("acceptance", "assess", "acceptance", "accepted", 0)
    ];
    public static IEnumerable<object[]> Data => All.Select(c => new object[] { c.Name });
    public static ReviewCase Find(string name) => All.Single(c => c.Name == name);
    public static void AssertContract(ReviewCase test, CaseObservation actual)
    {
        Assert.Equal(test.Expected, actual.Status);
        Assert.Equal(test.Calls, actual.Calls);
    }
}

// Scripted responses are contract fixtures, deliberately independent of the production algorithm.
public sealed class ScriptedReviewSystem : IReviewCaseSystem
{
    public Task<CaseObservation> ExecuteAsync(ReviewCase test) => Task.FromResult(test.Name switch
    {
        "ready" => new CaseObservation("prepared", 0),
        "draft" => new("PR_NOT_READY", 0),
        "wrong-head" => new("HEAD_MISMATCH", 0),
        "missing-unit-trx" => new("TEST_EVIDENCE_INCOMPLETE", 0),
        "failed-trx" or "skipped-trx" => new("TEST_EVIDENCE_FAILED", 0),
        "requirements-open" => new("REQUIREMENTS_INCOMPLETE", 0),
        "scope-leak" => new("SCOPE_VIOLATION", 0),
        "secret" => new("PRIVATE_PAYLOAD", 0),
        "coverage-omission" => new("COVERAGE_INCOMPLETE", 0),
        "complete" => new("complete", 1),
        "automatic-output-continuation" => new("complete", 2),
        "authentication-pause" => new("paused-provider", 1),
        "model-mismatch" => new("MODEL_MISMATCH", 1),
        "effort-mismatch" => new("EFFORT_MISMATCH", 1),
        "uncertain" or "active" => new("outcome-uncertain", 0),
        "complete-adoption" or "split-json" => new("complete", 0),
        "partial-json" => new("continue", 0),
        "context-boundary" => new("context", 0),
        "credit-pause" => new("paused-provider", 0),
        "low-score" or "missing-score" => new("rejected", 0),
        "acceptance" => new("accepted", 0),
        _ => throw new InvalidOperationException("Unscripted contract fixture")
    });
}

[Trait("Gate", "Mock")]
public sealed class ReviewMockGate
{
    [Theory, MemberData(nameof(ReviewCases.Data), MemberType = typeof(ReviewCases))]
    public async Task CorrectMockExercisesSameAssertions(string name)
    {
        var test = ReviewCases.Find(name);
        ReviewCases.AssertContract(test, await new ScriptedReviewSystem().ExecuteAsync(test));
    }

    [Theory, MemberData(nameof(ReviewCases.Data), MemberType = typeof(ReviewCases))]
    public async Task IndividualStatusNegativeControlIsRejected(string name)
    {
        var test = ReviewCases.Find(name);
        var correct = await new ScriptedReviewSystem().ExecuteAsync(test);
        Assert.NotNull(Record.Exception(() => ReviewCases.AssertContract(test, correct with { Status = "wrong" })));
    }

    [Theory, MemberData(nameof(ReviewCases.Data), MemberType = typeof(ReviewCases))]
    public async Task IndividualInteractionNegativeControlIsRejected(string name)
    {
        var test = ReviewCases.Find(name);
        var correct = await new ScriptedReviewSystem().ExecuteAsync(test);
        Assert.NotNull(Record.Exception(() => ReviewCases.AssertContract(test, correct with { Calls = correct.Calls + 1 })));
    }
}
