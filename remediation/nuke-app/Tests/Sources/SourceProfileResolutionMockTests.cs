using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Mock")]
[Trait("Requirement", "TEST-BUILD-004")]
public sealed class SourceProfileResolutionMockTests
{
    public static IEnumerable<object[]> WrongResponses => SourceProfileResolutionContractCases.All.SelectMany(test => new[] { "value", "exception" }.Select(wrong => new object[] { test.Id, wrong }));
    public static IEnumerable<object[]> WrongMessages => SourceProfileResolutionContractCases.All.Where(test => test.Expected is null).Select(test => new object[] { test.Id });
    [Theory, MemberData(nameof(SourceProfileResolutionContractCases.Cases), MemberType = typeof(SourceProfileResolutionContractCases))]
    public void CorrectMocksProveExactExistingCatalogProfileBehavior(string id)
    {
        var test = SourceProfileResolutionContractCases.Get(id);
        SourceProfileResolutionContractCases.AssertContract(test, input =>
        { Assert.Equal(test.Input, input); return test.Expected ?? throw new InvalidOperationException("Unknown source extraction profile"); });
    }
    [Theory, MemberData(nameof(WrongResponses))]
    public void EveryWrongProfileOrExceptionResponseIsRejected(string id, string wrong)
    {
        var test = SourceProfileResolutionContractCases.Get(id);
        Assert.NotNull(Record.Exception(() => SourceProfileResolutionContractCases.AssertContract(test, _ =>
        {
            if (wrong == "exception") throw new IOException("Wrong profile error");
            return ReferenceEquals(test.Expected, SourceExtractionProfile.CurrentCandidate) ? SourceExtractionProfile.RELEASE011 : SourceExtractionProfile.CurrentCandidate;
        })));
    }
    [Theory, MemberData(nameof(WrongMessages))]
    public void EveryChangedLegacyUnknownProfileMessageIsRejected(string id) => Assert.NotNull(Record.Exception(() =>
        SourceProfileResolutionContractCases.AssertContract(SourceProfileResolutionContractCases.Get(id), _ => throw new InvalidOperationException("Changed unknown-profile message"))));
}
