using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review;

public sealed class ReviewPayloadNativeTests
{
    [Theory, MemberData(nameof(ReviewPayloadCases.Data), MemberType = typeof(ReviewPayloadCases))]
    public async Task SamePayloadCasesBindToNativePreparation(string name)
    {
        var fixture = new ReviewFixture();
        CaseObservation result;
        try
        {
            await fixture.PrepareAsync(name);
            result = new("public", fixture.Runner.Calls.Count);
        }
        catch (AndroidMcp.BuildTool.Modules.Review.ReviewException error)
        { result = new(error.Code, fixture.Runner.Calls.Count); }
        ReviewPayloadCases.AssertContract(name, result);
    }
}
