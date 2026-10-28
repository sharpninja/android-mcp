using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review;

public sealed record ReadinessObservation(string Status, string Head, int Calls, int DistinctReceiptPaths);
public interface IReadinessCaseSystem { Task<ReadinessObservation> ExecuteAsync(string name); }
public static class ReviewReadinessCases
{
    public static IEnumerable<object[]> Data => new[] { "live-ready", "repeat-observation", "invalid-pr", "failed-gh" }.Select(x => new object[] { x });
    public static void AssertContract(string name, ReadinessObservation actual)
    {
        Assert.Equal(name == "invalid-pr" ? "INVALID_PR" : name == "failed-gh" ? "READINESS_UNAVAILABLE" : "observed", actual.Status);
        Assert.Equal(name is "invalid-pr" or "failed-gh" ? "" : new string('a', 40), actual.Head);
        var calls = name == "invalid-pr" ? 0 : name == "repeat-observation" ? 2 : 1;
        Assert.Equal(calls, actual.Calls);
        Assert.Equal(calls, actual.DistinctReceiptPaths);
    }
}
public sealed class ScriptedReadinessCases : IReadinessCaseSystem
{
    public Task<ReadinessObservation> ExecuteAsync(string name) => Task.FromResult(name switch
    {
        "live-ready" => new ReadinessObservation("observed", new string('a', 40), 1, 1),
        "repeat-observation" => new("observed", new string('a', 40), 2, 2),
        "invalid-pr" => new("INVALID_PR", "", 0, 0),
        "failed-gh" => new("READINESS_UNAVAILABLE", "", 1, 1),
        _ => throw new InvalidOperationException("Unknown readiness fixture")
    });
}
[Trait("Gate", "Mock")]
public sealed class ReviewReadinessMockGate
{
    [Theory, MemberData(nameof(ReviewReadinessCases.Data), MemberType = typeof(ReviewReadinessCases))]
    public async Task CorrectMock(string name) => ReviewReadinessCases.AssertContract(name, await new ScriptedReadinessCases().ExecuteAsync(name));
    [Theory, MemberData(nameof(ReviewReadinessCases.Data), MemberType = typeof(ReviewReadinessCases))]
    public async Task IndividualStatusControl(string name)
    {
        var actual = await new ScriptedReadinessCases().ExecuteAsync(name);
        Assert.NotNull(Record.Exception(() => ReviewReadinessCases.AssertContract(name, actual with { Status = "wrong" })));
    }
    [Theory, MemberData(nameof(ReviewReadinessCases.Data), MemberType = typeof(ReviewReadinessCases))]
    public async Task IndividualHeadControl(string name)
    {
        var actual = await new ScriptedReadinessCases().ExecuteAsync(name);
        Assert.NotNull(Record.Exception(() => ReviewReadinessCases.AssertContract(name, actual with { Head = "wrong" })));
    }
    [Theory, MemberData(nameof(ReviewReadinessCases.Data), MemberType = typeof(ReviewReadinessCases))]
    public async Task IndividualInteractionControl(string name)
    {
        var actual = await new ScriptedReadinessCases().ExecuteAsync(name);
        Assert.NotNull(Record.Exception(() => ReviewReadinessCases.AssertContract(name, actual with { Calls = actual.Calls + 1 })));
    }
    [Theory, MemberData(nameof(ReviewReadinessCases.Data), MemberType = typeof(ReviewReadinessCases))]
    public async Task IndividualIsolationControl(string name)
    {
        var actual = await new ScriptedReadinessCases().ExecuteAsync(name);
        Assert.NotNull(Record.Exception(() => ReviewReadinessCases.AssertContract(name, actual with { DistinctReceiptPaths = actual.DistinctReceiptPaths + 1 })));
    }
}
