using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review;

public sealed record StartupObservation(string Status, string Reason, int Calls);
public interface IReviewStartupSystem { Task<StartupObservation> ExecuteAsync(string name); }
public static class ReviewStartupCases
{
    public static IEnumerable<object[]> Data => new[] { "stderr-authentication", "stderr-capacity", "stderr-capability", "empty-unclassified" }.Select(x => new object[] { x });
    public static void AssertContract(string name, StartupObservation actual)
    {
        Assert.Equal(name == "empty-unclassified" ? "outcome-uncertain" : "paused-provider", actual.Status);
        Assert.Equal(name == "empty-unclassified" ? "unclassified" : name[7..], actual.Reason);
        Assert.Equal(1, actual.Calls);
    }
}
public sealed class ScriptedReviewStartup : IReviewStartupSystem
{
    public Task<StartupObservation> ExecuteAsync(string name) => Task.FromResult(name switch
    {
        "stderr-authentication" => new StartupObservation("paused-provider", "authentication", 1),
        "stderr-capacity" => new("paused-provider", "capacity", 1),
        "stderr-capability" => new("paused-provider", "capability", 1),
        "empty-unclassified" => new("outcome-uncertain", "unclassified", 1),
        _ => throw new InvalidOperationException("Unknown startup fixture")
    });
}
[Trait("Gate", "Mock")]
public sealed class ReviewStartupMockGate
{
    [Theory, MemberData(nameof(ReviewStartupCases.Data), MemberType = typeof(ReviewStartupCases))]
    public async Task CorrectMock(string name) => ReviewStartupCases.AssertContract(name, await new ScriptedReviewStartup().ExecuteAsync(name));
    [Theory, MemberData(nameof(ReviewStartupCases.Data), MemberType = typeof(ReviewStartupCases))]
    public async Task IndividualStatusControl(string name)
    {
        var actual = await new ScriptedReviewStartup().ExecuteAsync(name);
        Assert.NotNull(Record.Exception(() => ReviewStartupCases.AssertContract(name, actual with { Status = "wrong" })));
    }
    [Theory, MemberData(nameof(ReviewStartupCases.Data), MemberType = typeof(ReviewStartupCases))]
    public async Task IndividualReasonControl(string name)
    {
        var actual = await new ScriptedReviewStartup().ExecuteAsync(name);
        Assert.NotNull(Record.Exception(() => ReviewStartupCases.AssertContract(name, actual with { Reason = "wrong" })));
    }
    [Theory, MemberData(nameof(ReviewStartupCases.Data), MemberType = typeof(ReviewStartupCases))]
    public async Task IndividualInteractionControl(string name)
    {
        var actual = await new ScriptedReviewStartup().ExecuteAsync(name);
        Assert.NotNull(Record.Exception(() => ReviewStartupCases.AssertContract(name, actual with { Calls = actual.Calls + 1 })));
    }
}
