using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review;

public static class ReviewRecoveryCases
{
    public static readonly ReviewSafetyCase[] All =
    [
        new("terminal-continuation", "complete", 2, "original-report-retained"),
        new("completed-adoption", "complete", 1, "originals-unchanged"),
        new("uncertain-prefix", "OUTCOME_UNCERTAIN", 1, "no-duplicate"),
        new("context-resume", "complete", 4, "all-passes-retained"),
        new("manifest-tamper", "EVIDENCE_HASH_MISMATCH", 1, "no-duplicate"),
        new("unknown-terminal-error", "outcome-uncertain", 1, "no-retry-spin")
    ];
    public static IEnumerable<object[]> Data => All.Select(c => new object[] { c.Name });
    public static void AssertContract(string name, ReviewSafetyObservation observation)
    {
        var expected = All.Single(x => x.Name == name);
        Assert.Equal(expected.Status, observation.Status);
        Assert.Equal(expected.Calls, observation.Calls);
        Assert.Equal(expected.Detail, observation.Detail);
    }
}
public sealed class ScriptedReviewRecovery : IReviewSafetySystem
{
    public Task<ReviewSafetyObservation> ExecuteAsync(string name) => Task.FromResult(name switch
    {
        "terminal-continuation" => new ReviewSafetyObservation("complete", 2, "original-report-retained"),
        "completed-adoption" => new("complete", 1, "originals-unchanged"),
        "uncertain-prefix" => new("OUTCOME_UNCERTAIN", 1, "no-duplicate"),
        "context-resume" => new("complete", 4, "all-passes-retained"),
        "manifest-tamper" => new("EVIDENCE_HASH_MISMATCH", 1, "no-duplicate"),
        "unknown-terminal-error" => new("outcome-uncertain", 1, "no-retry-spin"),
        _ => throw new InvalidOperationException("Unknown recovery fixture")
    });
}
[Trait("Gate", "Mock")]
public sealed class ReviewRecoveryMockGate
{
    [Theory, MemberData(nameof(ReviewRecoveryCases.Data), MemberType = typeof(ReviewRecoveryCases))]
    public async Task CorrectMock(string name) => ReviewRecoveryCases.AssertContract(name, await new ScriptedReviewRecovery().ExecuteAsync(name));
    [Theory, MemberData(nameof(ReviewRecoveryCases.Data), MemberType = typeof(ReviewRecoveryCases))]
    public async Task EachIndividualStatusControl(string name)
    {
        var actual = await new ScriptedReviewRecovery().ExecuteAsync(name);
        Assert.NotNull(Record.Exception(() => ReviewRecoveryCases.AssertContract(name, actual with { Status = "wrong" })));
    }
    [Theory, MemberData(nameof(ReviewRecoveryCases.Data), MemberType = typeof(ReviewRecoveryCases))]
    public async Task EachIndividualInteractionControl(string name)
    {
        var actual = await new ScriptedReviewRecovery().ExecuteAsync(name);
        Assert.NotNull(Record.Exception(() => ReviewRecoveryCases.AssertContract(name, actual with { Calls = actual.Calls + 1 })));
    }
    [Theory, MemberData(nameof(ReviewRecoveryCases.Data), MemberType = typeof(ReviewRecoveryCases))]
    public async Task EachIndividualDetailControl(string name)
    {
        var actual = await new ScriptedReviewRecovery().ExecuteAsync(name);
        Assert.NotNull(Record.Exception(() => ReviewRecoveryCases.AssertContract(name, actual with { Detail = "wrong" })));
    }
}
