using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review;

public sealed record FinalFragmentObservation(string Status, bool FullVisibleReportPreserved, int Calls);
public interface IFinalFragmentSystem { FinalFragmentObservation Execute(string name); }
public static class ReviewFinalFragmentCases
{
    public static IEnumerable<object[]> Data => new[] { "progress-before-verdict", "split-json-after-progress", "latest-disagree-dominates", "cut-off-after-agree" }.Select(x => new object[] { x });
    public static void AssertContract(string name, FinalFragmentObservation actual)
    {
        Assert.Equal(name == "latest-disagree-dominates" ? "rejected" : name == "cut-off-after-agree" ? "continue" : "accepted", actual.Status);
        Assert.True(actual.FullVisibleReportPreserved);
        Assert.Equal(0, actual.Calls);
    }
}
public sealed class ScriptedFinalFragments : IFinalFragmentSystem
{
    public FinalFragmentObservation Execute(string name) => name switch
    {
        "progress-before-verdict" or "split-json-after-progress" => new("accepted", true, 0),
        "latest-disagree-dominates" => new("rejected", true, 0),
        "cut-off-after-agree" => new("continue", true, 0),
        _ => throw new InvalidOperationException("Unknown final-fragment fixture")
    };
}
[Trait("Gate", "Mock")]
public sealed class ReviewFinalFragmentMockGate
{
    [Theory, MemberData(nameof(ReviewFinalFragmentCases.Data), MemberType = typeof(ReviewFinalFragmentCases))]
    public void CorrectMock(string name) => ReviewFinalFragmentCases.AssertContract(name, new ScriptedFinalFragments().Execute(name));
    [Theory, MemberData(nameof(ReviewFinalFragmentCases.Data), MemberType = typeof(ReviewFinalFragmentCases))]
    public void IndividualStatusControl(string name)
    {
        var actual = new ScriptedFinalFragments().Execute(name);
        Assert.NotNull(Record.Exception(() => ReviewFinalFragmentCases.AssertContract(name, actual with { Status = "wrong" })));
    }
    [Theory, MemberData(nameof(ReviewFinalFragmentCases.Data), MemberType = typeof(ReviewFinalFragmentCases))]
    public void IndividualVisibleRetentionControl(string name)
    {
        var actual = new ScriptedFinalFragments().Execute(name);
        Assert.NotNull(Record.Exception(() => ReviewFinalFragmentCases.AssertContract(name, actual with { FullVisibleReportPreserved = false })));
    }
    [Theory, MemberData(nameof(ReviewFinalFragmentCases.Data), MemberType = typeof(ReviewFinalFragmentCases))]
    public void IndividualInteractionControl(string name)
    {
        var actual = new ScriptedFinalFragments().Execute(name);
        Assert.NotNull(Record.Exception(() => ReviewFinalFragmentCases.AssertContract(name, actual with { Calls = 1 })));
    }
}
