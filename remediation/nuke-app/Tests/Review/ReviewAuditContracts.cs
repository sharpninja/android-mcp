using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review;

public sealed record AuditObservation(string Status, int ProviderCalls, bool FinalAccepted);
public interface IReviewAuditCaseSystem { Task<AuditObservation> ExecuteAsync(string name); }
public static class ReviewAuditCases
{
    public static IEnumerable<object[]> Data => new[] { "ready", "no-bootstrap-config", "wrong-tool-policy", "wrong-agent", "wrong-head", "unpersisted-own-turn", "wrong-post-agent", "altered-verdict" }.Select(x => new object[] { x });
    public static void AssertContract(string name, AuditObservation actual)
    {
        Assert.Equal(name == "ready" ? "accepted" : name == "wrong-head" ? "HEAD_MISMATCH" : "REVIEWER_AUDIT_UNAVAILABLE", actual.Status);
        Assert.Equal(name is "no-bootstrap-config" or "wrong-tool-policy" or "wrong-agent" or "wrong-head" ? 0 : 1, actual.ProviderCalls);
        Assert.Equal(name == "ready", actual.FinalAccepted);
    }
}
public sealed class ScriptedReviewAuditCases : IReviewAuditCaseSystem
{
    public Task<AuditObservation> ExecuteAsync(string name) => Task.FromResult(name switch
    {
        "ready" => new AuditObservation("accepted", 1, true),
        "no-bootstrap-config" or "wrong-tool-policy" or "wrong-agent" => new("REVIEWER_AUDIT_UNAVAILABLE", 0, false),
        "wrong-head" => new("HEAD_MISMATCH", 0, false),
        "unpersisted-own-turn" or "wrong-post-agent" or "altered-verdict" => new("REVIEWER_AUDIT_UNAVAILABLE", 1, false),
        _ => throw new InvalidOperationException("Unknown audit fixture")
    });
}
[Trait("Gate", "Mock")]
public sealed class ReviewAuditMockGate
{
    [Theory, MemberData(nameof(ReviewAuditCases.Data), MemberType = typeof(ReviewAuditCases))]
    public async Task CorrectMock(string name) => ReviewAuditCases.AssertContract(name, await new ScriptedReviewAuditCases().ExecuteAsync(name));
    [Theory, MemberData(nameof(ReviewAuditCases.Data), MemberType = typeof(ReviewAuditCases))]
    public async Task IndividualStatusControl(string name)
    {
        var actual = await new ScriptedReviewAuditCases().ExecuteAsync(name);
        Assert.NotNull(Record.Exception(() => ReviewAuditCases.AssertContract(name, actual with { Status = "wrong" })));
    }
    [Theory, MemberData(nameof(ReviewAuditCases.Data), MemberType = typeof(ReviewAuditCases))]
    public async Task IndividualInteractionControl(string name)
    {
        var actual = await new ScriptedReviewAuditCases().ExecuteAsync(name);
        Assert.NotNull(Record.Exception(() => ReviewAuditCases.AssertContract(name, actual with { ProviderCalls = actual.ProviderCalls + 1 })));
    }
    [Theory, MemberData(nameof(ReviewAuditCases.Data), MemberType = typeof(ReviewAuditCases))]
    public async Task IndividualAcceptanceControl(string name)
    {
        var actual = await new ScriptedReviewAuditCases().ExecuteAsync(name);
        Assert.NotNull(Record.Exception(() => ReviewAuditCases.AssertContract(name, actual with { FinalAccepted = !actual.FinalAccepted })));
    }
}
