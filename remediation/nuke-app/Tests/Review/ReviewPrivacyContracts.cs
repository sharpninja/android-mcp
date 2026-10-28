using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review;

public sealed record PrivacyObservation(string Status, bool HiddenAbsent, bool VisibleRetained, int Events);
public interface IReviewPrivacySystem { PrivacyObservation Execute(string name); }
public static class ReviewPrivacyCases
{
    public static IEnumerable<object[]> Data => new[] { "malformed-hidden-fragment", "nested-hidden-delta" }.Select(x => new object[] { x });
    public static void AssertContract(string name, PrivacyObservation actual)
    {
        Assert.Equal(name == "malformed-hidden-fragment" ? "outcome-uncertain" : "complete", actual.Status);
        Assert.True(actual.HiddenAbsent);
        Assert.True(actual.VisibleRetained);
        Assert.Equal(name == "malformed-hidden-fragment" ? 2 : 3, actual.Events);
    }
}
public sealed class ScriptedReviewPrivacy : IReviewPrivacySystem
{
    public PrivacyObservation Execute(string name) => name switch
    {
        "malformed-hidden-fragment" => new("outcome-uncertain", true, true, 2),
        "nested-hidden-delta" => new("complete", true, true, 3),
        _ => throw new InvalidOperationException("Unknown privacy fixture")
    };
}
[Trait("Gate", "Mock")]
public sealed class ReviewPrivacyMockGate
{
    [Theory, MemberData(nameof(ReviewPrivacyCases.Data), MemberType = typeof(ReviewPrivacyCases))]
    public void CorrectMock(string name) => ReviewPrivacyCases.AssertContract(name, new ScriptedReviewPrivacy().Execute(name));
    [Theory, MemberData(nameof(ReviewPrivacyCases.Data), MemberType = typeof(ReviewPrivacyCases))]
    public void IndividualStatusControl(string name)
    {
        var actual = new ScriptedReviewPrivacy().Execute(name);
        Assert.NotNull(Record.Exception(() => ReviewPrivacyCases.AssertContract(name, actual with { Status = "wrong" })));
    }
    [Theory, MemberData(nameof(ReviewPrivacyCases.Data), MemberType = typeof(ReviewPrivacyCases))]
    public void IndividualHiddenControl(string name)
    {
        var actual = new ScriptedReviewPrivacy().Execute(name);
        Assert.NotNull(Record.Exception(() => ReviewPrivacyCases.AssertContract(name, actual with { HiddenAbsent = false })));
    }
    [Theory, MemberData(nameof(ReviewPrivacyCases.Data), MemberType = typeof(ReviewPrivacyCases))]
    public void IndividualVisibleControl(string name)
    {
        var actual = new ScriptedReviewPrivacy().Execute(name);
        Assert.NotNull(Record.Exception(() => ReviewPrivacyCases.AssertContract(name, actual with { VisibleRetained = false })));
    }
    [Theory, MemberData(nameof(ReviewPrivacyCases.Data), MemberType = typeof(ReviewPrivacyCases))]
    public void IndividualEventControl(string name)
    {
        var actual = new ScriptedReviewPrivacy().Execute(name);
        Assert.NotNull(Record.Exception(() => ReviewPrivacyCases.AssertContract(name, actual with { Events = actual.Events + 1 })));
    }
}
