using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review;

public interface IReviewPayloadSystem { CaseObservation Execute(string name); }
public static class ReviewPayloadCases
{
    public static IEnumerable<object[]> Data => new[] { "json-credential", "bearer-credential", "private-profile-path", "public-auth-schema" }.Select(x => new object[] { x });
    public static void AssertContract(string name, CaseObservation actual)
    {
        Assert.Equal(name == "public-auth-schema" ? "public" : "PRIVATE_PAYLOAD", actual.Status);
        Assert.Equal(0, actual.Calls);
    }
}
public sealed class ScriptedReviewPayload : IReviewPayloadSystem
{
    public CaseObservation Execute(string name) => name switch
    {
        "json-credential" or "bearer-credential" or "private-profile-path" => new("PRIVATE_PAYLOAD", 0),
        "public-auth-schema" => new("public", 0),
        _ => throw new InvalidOperationException("Unknown payload fixture")
    };
}
[Trait("Gate", "Mock")]
public sealed class ReviewPayloadMockGate
{
    [Theory, MemberData(nameof(ReviewPayloadCases.Data), MemberType = typeof(ReviewPayloadCases))]
    public void CorrectMock(string name) => ReviewPayloadCases.AssertContract(name, new ScriptedReviewPayload().Execute(name));
    [Theory, MemberData(nameof(ReviewPayloadCases.Data), MemberType = typeof(ReviewPayloadCases))]
    public void IndividualStatusControl(string name)
    {
        var actual = new ScriptedReviewPayload().Execute(name);
        Assert.NotNull(Record.Exception(() => ReviewPayloadCases.AssertContract(name, actual with { Status = "wrong" })));
    }
    [Theory, MemberData(nameof(ReviewPayloadCases.Data), MemberType = typeof(ReviewPayloadCases))]
    public void IndividualInteractionControl(string name)
    {
        var actual = new ScriptedReviewPayload().Execute(name);
        Assert.NotNull(Record.Exception(() => ReviewPayloadCases.AssertContract(name, actual with { Calls = 1 })));
    }
}
