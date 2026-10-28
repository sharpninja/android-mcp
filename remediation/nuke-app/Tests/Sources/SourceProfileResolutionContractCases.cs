using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record SourceProfileResolutionCase(string Id, string? Input, SourceExtractionProfile? Expected);
public static class SourceProfileResolutionContractCases
{
    public static IReadOnlyList<SourceProfileResolutionCase> All { get; } =
    [new("null", null, SourceExtractionProfile.CurrentCandidate), new("current", "current-candidate", SourceExtractionProfile.CurrentCandidate),
     new("release", "release-011", SourceExtractionProfile.RELEASE011), new("empty", "", null), new("whitespace", " current-candidate", null),
     new("case", "CURRENT-CANDIDATE", null), new("alias", "release011", null), new("unknown", "unknown", null)];
    public static IEnumerable<object[]> Cases => All.Select(test => new object[] { test.Id });
    public static SourceProfileResolutionCase Get(string id) => All.Single(test => test.Id == id);
    public static void AssertContract(SourceProfileResolutionCase test, Func<string?, SourceExtractionProfile> resolve)
    {
        SourceExtractionProfile? result = null; var error = Record.Exception(() => result = resolve(test.Input));
        if (test.Expected is not null) { Assert.Null(error); Assert.Same(test.Expected, result); }
        else { Assert.Null(result); Assert.Equal("Unknown source extraction profile", Assert.IsType<InvalidOperationException>(error).Message); }
    }
}
