using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review;

public static class ReviewVerdictCases
{
    public static IEnumerable<object[]> Data => new[] { "numeric-100", "numeric-101", "numeric-string", "overflow-number", "fail-finding", "unknown-finding", "empty-finding-arrays" }.Select(x => new object[] { x });
    public static void AssertContract(string name, CaseObservation actual)
    {
        Assert.Equal(name is "numeric-100" or "empty-finding-arrays" ? "accepted" : "rejected", actual.Status);
        Assert.Equal(0, actual.Calls);
    }
}
public sealed class ScriptedReviewVerdicts : IReviewPayloadSystem
{
    public CaseObservation Execute(string name) => name switch
    {
        "numeric-100" or "empty-finding-arrays" => new("accepted", 0),
        "numeric-101" or "numeric-string" or "overflow-number" or "fail-finding" or "unknown-finding" => new("rejected", 0),
        _ => throw new InvalidOperationException("Unknown verdict fixture")
    };
}
[Trait("Gate", "Mock")]
public sealed class ReviewVerdictMockGate
{
    [Theory, MemberData(nameof(ReviewVerdictCases.Data), MemberType = typeof(ReviewVerdictCases))]
    public void CorrectMock(string name) => ReviewVerdictCases.AssertContract(name, new ScriptedReviewVerdicts().Execute(name));
    [Theory, MemberData(nameof(ReviewVerdictCases.Data), MemberType = typeof(ReviewVerdictCases))]
    public void IndividualStatusControl(string name)
    {
        var actual = new ScriptedReviewVerdicts().Execute(name);
        Assert.NotNull(Record.Exception(() => ReviewVerdictCases.AssertContract(name, actual with { Status = "wrong" })));
    }
    [Theory, MemberData(nameof(ReviewVerdictCases.Data), MemberType = typeof(ReviewVerdictCases))]
    public void IndividualInteractionControl(string name)
    {
        var actual = new ScriptedReviewVerdicts().Execute(name);
        Assert.NotNull(Record.Exception(() => ReviewVerdictCases.AssertContract(name, actual with { Calls = 1 })));
    }
}
