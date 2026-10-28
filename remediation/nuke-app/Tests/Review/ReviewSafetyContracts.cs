using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review;

public sealed record ReviewSafetyCase(string Name, string Status, int Calls, string Detail);
public sealed record ReviewSafetyObservation(string Status, int Calls, string Detail);
public interface IReviewSafetySystem { Task<ReviewSafetyObservation> ExecuteAsync(string name); }
public static class ReviewSafetyCases
{
    public static readonly ReviewSafetyCase[] All =
    [
        new("tampered-frozen-evidence", "EVIDENCE_HASH_MISMATCH", 0, "no-launch"),
        new("unverified-capability", "CAPABILITY_UNVERIFIED", 0, "no-launch"),
        new("missing-model", "MODEL_MISMATCH", 1, "no-retry"),
        new("missing-effort", "EFFORT_MISMATCH", 1, "no-retry"),
        new("duplicate-launch", "OUTCOME_UNCERTAIN", 1, "no-retry"),
        new("hidden-excluded", "complete", 0, "visible-fields-retained"),
        new("malformed-stream", "outcome-uncertain", 0, "no-acceptance"),
        new("unicode-partition", "partitioned", 0, "exact-runes-and-coverage"),
        new("context-unified", "complete", 4, "all-inputs-one-synthesis"),
        new("process-exception", "outcome-uncertain", 1, "no-retry"),
        new("recovery-head-mismatch", "HEAD_MISMATCH", 0, "no-launch"),
        new("changed-ready-head", "HEAD_MISMATCH", 0, "no-launch")
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
public sealed class ScriptedReviewSafety : IReviewSafetySystem
{
    public Task<ReviewSafetyObservation> ExecuteAsync(string name) => Task.FromResult(name switch
    {
        "tampered-frozen-evidence" => new ReviewSafetyObservation("EVIDENCE_HASH_MISMATCH", 0, "no-launch"),
        "unverified-capability" => new("CAPABILITY_UNVERIFIED", 0, "no-launch"),
        "missing-model" => new("MODEL_MISMATCH", 1, "no-retry"),
        "missing-effort" => new("EFFORT_MISMATCH", 1, "no-retry"),
        "duplicate-launch" => new("OUTCOME_UNCERTAIN", 1, "no-retry"),
        "hidden-excluded" => new("complete", 0, "visible-fields-retained"),
        "malformed-stream" => new("outcome-uncertain", 0, "no-acceptance"),
        "unicode-partition" => new("partitioned", 0, "exact-runes-and-coverage"),
        "context-unified" => new("complete", 4, "all-inputs-one-synthesis"),
        "process-exception" => new("outcome-uncertain", 1, "no-retry"),
        "recovery-head-mismatch" => new("HEAD_MISMATCH", 0, "no-launch"),
        "changed-ready-head" => new("HEAD_MISMATCH", 0, "no-launch"),
        _ => throw new InvalidOperationException("Unknown safety fixture")
    });
}
[Trait("Gate", "Mock")]
public sealed class ReviewSafetyMockGate
{
    [Theory, MemberData(nameof(ReviewSafetyCases.Data), MemberType = typeof(ReviewSafetyCases))]
    public async Task CorrectMock(string name) => ReviewSafetyCases.AssertContract(name, await new ScriptedReviewSafety().ExecuteAsync(name));
    [Theory, MemberData(nameof(ReviewSafetyCases.Data), MemberType = typeof(ReviewSafetyCases))]
    public async Task EachIndividualStatusControl(string name)
    {
        var actual = await new ScriptedReviewSafety().ExecuteAsync(name);
        Assert.NotNull(Record.Exception(() => ReviewSafetyCases.AssertContract(name, actual with { Status = "wrong" })));
    }
    [Theory, MemberData(nameof(ReviewSafetyCases.Data), MemberType = typeof(ReviewSafetyCases))]
    public async Task EachIndividualInteractionControl(string name)
    {
        var actual = await new ScriptedReviewSafety().ExecuteAsync(name);
        Assert.NotNull(Record.Exception(() => ReviewSafetyCases.AssertContract(name, actual with { Calls = actual.Calls + 1 })));
    }
    [Theory, MemberData(nameof(ReviewSafetyCases.Data), MemberType = typeof(ReviewSafetyCases))]
    public async Task EachIndividualDetailControl(string name)
    {
        var actual = await new ScriptedReviewSafety().ExecuteAsync(name);
        Assert.NotNull(Record.Exception(() => ReviewSafetyCases.AssertContract(name, actual with { Detail = "wrong" })));
    }
}
